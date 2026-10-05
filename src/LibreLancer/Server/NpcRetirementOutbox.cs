using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LancerNexus.Protocol;
using MessagePack;

namespace LibreLancer.Server;

/// <summary>Writes retirement intent before sending it; retries unknown outcomes with the same identity.</summary>
public sealed class NpcRetirementOutbox : IAsyncDisposable
{
    private sealed record Item(NpcRetirementRequestV1 Request, TaskCompletionSource<bool> Durable,
        TaskCompletionSource<NpcRetirementResponseV1> Completed);
    private readonly Channel<Item> intake = Channel.CreateBounded<Item>(1024);
    private readonly Channel<Item> delivery = Channel.CreateUnbounded<Item>();
    private readonly object pendingSync = new();
    private readonly Dictionary<(Guid Id, long Version), HashSet<Guid>> pending = new();
    private readonly ConcurrentDictionary<(Guid Id, long Version), byte> retired = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly HttpClient http;
    private readonly Uri endpoint;
    private readonly string directory;
    private readonly string instance;
    private readonly Task persistenceWorker;
    private readonly Task deliveryWorker;
    private readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Ready => ready.Task;

    public NpcRetirementOutbox(string directory, Uri coordinator, string key, string instance)
        : this(directory, coordinator, key, instance, new HttpClientHandler { AllowAutoRedirect = false }) { }

    internal NpcRetirementOutbox(string directory, Uri coordinator, string key, string instance,
        HttpMessageHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance);
        if (!coordinator.IsAbsoluteUri || coordinator.Scheme != "https" &&
            !(coordinator.Scheme == "http" && coordinator.IsLoopback))
            throw new ArgumentException("NPC retirement requires HTTPS or loopback development HTTP.");
        this.directory = directory;
        this.instance = instance;
        endpoint = new Uri(coordinator, "/internal/v1/npc-retirements");
        http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 128 * 1024 };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        persistenceWorker = Task.Run(PersistAsync);
        deliveryWorker = Task.Run(DeliverAsync);
    }

    public bool IsPending(Guid npcId, long version)
    {
        lock (pendingSync)
            return pending.ContainsKey((npcId, version));
    }

    private void MarkPending(NpcRetirementRequestV1 request, bool add)
    {
        lock (pendingSync)
        {
            foreach (var npc in request.Npcs)
            {
                var key = (npc.NpcId, npc.OwnershipVersion);
                if (add)
                {
                    if (!pending.TryGetValue(key, out var requests))
                        pending[key] = requests = new HashSet<Guid>();
                    requests.Add(request.RequestId);
                }
                else if (pending.TryGetValue(key, out var requests))
                {
                    requests.Remove(request.RequestId);
                    if (requests.Count == 0)
                        pending.Remove(key);
                }
            }
        }
    }

    public bool BlocksActivation(Guid npcId, long version) =>
        retired.ContainsKey((npcId, version)) || IsPending(npcId, version) || retired.ContainsKey((npcId, version));

    // Callers must await Durable before treating intent as restart-safe, and Completed
    // before applying a confirmed retirement. Neither operation blocks the simulation thread.
    public (Task Durable, Task<NpcRetirementResponseV1> Completed) Queue(NpcRetirementRequestV1 request)
    {
        if (!request.IsValid() || request.InstanceId != instance)
            throw new ArgumentException("NPC retirement request has invalid identity or owner.");
        var item = new Item(request with { Npcs = request.Npcs.ToArray() },
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously));
        if (!intake.Writer.TryWrite(item))
            throw new InvalidOperationException("NPC retirement persistence queue is full or closed.");
        return (item.Durable.Task, item.Completed.Task);
    }

    private async Task PersistAsync()
    {
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var path in Directory.EnumerateFiles(directory, "*.response").Order())
            {
                if (new FileInfo(path).Length > 128 * 1024)
                    throw new InvalidDataException("NPC retirement response exceeds its limit.");
                var response = MessagePackSerializer.Deserialize<NpcRetirementResponseV1>(File.ReadAllBytes(path),
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                if (response.RequestId == Guid.Empty || response.ReasonCode != "processed" ||
                    Path.GetFileName(path) != $"{response.RequestId:N}.response" || response.Npcs is null ||
                    response.Npcs.Any(result => result is null || result.NpcId == Guid.Empty ||
                        result.Accepted && (result.OwnershipVersion <= 1 || result.ReasonCode is not ("retired" or "already_retired"))))
                    throw new InvalidDataException("NPC retirement response has invalid identity or fence.");
                RememberRetirements(response);
            }
            foreach (var path in Directory.EnumerateFiles(directory, "*.pending").Order())
            {
                if (new FileInfo(path).Length > 128 * 1024)
                    throw new InvalidDataException("NPC retirement outbox record exceeds its limit.");
                var request = MessagePackSerializer.Deserialize<NpcRetirementRequestV1>(File.ReadAllBytes(path),
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                if (!request.IsValid() || request.InstanceId != instance ||
                    Path.GetFileName(path) != $"{request.RequestId:N}.pending")
                    throw new InvalidDataException("NPC retirement outbox record has invalid identity.");
                var item = new Item(request, new(TaskCreationOptions.RunContinuationsAsynchronously),
                    new(TaskCreationOptions.RunContinuationsAsynchronously));
                MarkPending(request, true);
                item.Durable.SetResult(true);
                await delivery.Writer.WriteAsync(item);
            }
            ready.TrySetResult(true);
            await foreach (var item in intake.Reader.ReadAllAsync())
            {
                try
                {
                    var path = Path.Combine(directory, $"{item.Request.RequestId:N}.pending");
                    var bytes = MessagePackSerializer.Serialize(item.Request);
                    if (File.Exists(path) && !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                        throw new InvalidDataException("NPC retirement request ID conflicts with durable intent.");
                    WriteAtomic(path, bytes);
                    MarkPending(item.Request, true);
                    item.Durable.TrySetResult(true);
                    await delivery.Writer.WriteAsync(item);
                }
                catch (Exception exception)
                {
                    item.Durable.TrySetException(exception);
                    item.Completed.TrySetException(exception);
                }
            }
        }
        catch (Exception exception)
        {
            ready.TrySetException(exception);
            intake.Writer.TryComplete(exception);
            while (intake.Reader.TryRead(out var item))
            {
                item.Durable.TrySetException(exception);
                item.Completed.TrySetException(exception);
            }
            throw;
        }
        finally { delivery.Writer.TryComplete(); }
    }

    private async Task DeliverAsync()
    {
        Item? active = null;
        try
        {
            await foreach (var item in delivery.Reader.ReadAllAsync(stopping.Token))
            {
                active = item;
                var delay = 100;
                while (!stopping.IsCancellationRequested)
                {
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                        timeout.CancelAfter(TimeSpan.FromSeconds(4));
                        using var reply = await http.PostAsJsonAsync(endpoint, item.Request, timeout.Token);
                        reply.EnsureSuccessStatusCode();
                        var response = await reply.Content.ReadFromJsonAsync<NpcRetirementResponseV1>(timeout.Token);
                        if (!Matches(item.Request, response))
                            throw new InvalidDataException("NPC retirement response does not match durable intent.");
                        WriteAtomic(Path.Combine(directory, $"{item.Request.RequestId:N}.response"),
                            MessagePackSerializer.Serialize(response!));
                        RememberRetirements(response!);
                        File.Delete(Path.Combine(directory, $"{item.Request.RequestId:N}.pending"));
                        MarkPending(item.Request, false);
                        item.Completed.TrySetResult(response!);
                        break;
                    }
                    catch (OperationCanceledException) when (stopping.IsCancellationRequested)
                    {
                        item.Completed.TrySetCanceled(stopping.Token);
                        return;
                    }
                    catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or
                                                     InvalidDataException or IOException or JsonException)
                    {
                        await Task.Delay(delay, stopping.Token);
                        delay = Math.Min(5000, delay * 2);
                    }
                }
                if (stopping.IsCancellationRequested)
                    item.Completed.TrySetCanceled(stopping.Token);
                active = null;
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        finally
        {
            active?.Completed.TrySetCanceled();
            while (delivery.Reader.TryRead(out var item))
                item.Completed.TrySetCanceled();
        }
    }

    private void RememberRetirements(NpcRetirementResponseV1 response)
    {
        foreach (var result in response.Npcs.Where(result => result.Accepted))
            retired[(result.NpcId, result.OwnershipVersion - 1)] = 0;
    }

    private static bool Matches(NpcRetirementRequestV1 request, NpcRetirementResponseV1? response) =>
        response is { ReasonCode: "processed" } && response.RequestId == request.RequestId &&
        response.Npcs is not null && response.Npcs.Length == request.Npcs.Length &&
        response.Npcs.All(result => result is not null) &&
        response.Npcs.Select(result => result.NpcId).Distinct().Count() == response.Npcs.Length &&
        response.Npcs.All(result => request.Npcs.Any(npc => npc.NpcId == result.NpcId &&
            (!result.Accepted || result.OwnershipVersion == npc.OwnershipVersion + 1 &&
                result.ReasonCode is "retired" or "already_retired")));

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                file.Write(bytes);
                file.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async ValueTask DisposeAsync()
    {
        intake.Writer.TryComplete();
        try { await persistenceWorker.ConfigureAwait(false); }
        finally
        {
            stopping.Cancel();
            await deliveryWorker.ConfigureAwait(false);
            http.Dispose();
            stopping.Dispose();
        }
    }
}
