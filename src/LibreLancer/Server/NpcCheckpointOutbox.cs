using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LancerNexus.Protocol;
using MessagePack;

namespace LibreLancer.Server;

/// <summary>Durable, single-writer checkpoint delivery. File and network I/O stay off the simulation thread.</summary>
internal sealed class NpcCheckpointOutbox : IAsyncDisposable
{
    private const int Capacity = 1024;
    private const int MaximumRecordBytes = NpcCheckpointContractValidator.MaximumPayloadBytes + 64 * 1024;
    private readonly string directory;
    private readonly string instance;
    private readonly HttpClient http;
    private readonly Channel<Item> intake = Channel.CreateBounded<Item>(new BoundedChannelOptions(Capacity)
        { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentDictionary<Guid, long> pendingNpcs = new();
    private readonly ConcurrentDictionary<Guid, (long Version, long Revision)> revisions = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task persistence;
    private readonly Task delivery;
    private readonly Channel<Item> outgoing = Channel.CreateBounded<Item>(new BoundedChannelOptions(Capacity)
        { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });
    private readonly Uri endpoint;
    private readonly string key;
    private readonly string instanceId;

    public Task Ready => ready.Task;

    public NpcCheckpointOutbox(string directory, Uri coordinator, string apiKey, string instance,
        HttpMessageHandler? handler = null)
    {
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true)
            { Timeout = Timeout.InfiniteTimeSpan };
        this.directory = directory;
        this.instance = instance;
        instanceId = instance;
        key = apiKey;
        endpoint = new Uri(coordinator, "/internal/v1/npc-checkpoints");
        persistence = Task.Run(PersistAsync);
        delivery = Task.Run(DeliverAsync);
    }

    public long GetRevision(Guid npcId, long ownershipVersion) =>
        revisions.TryGetValue(npcId, out var state) && state.Version == ownershipVersion ? state.Revision : 0;

    public bool IsPending(Guid npcId) => pendingNpcs.ContainsKey(npcId);

    public void ApplyRecoveredCheckpoint(NpcCheckpointRecoveryV1 recovery)
    {
        ValidateRecord(recovery, $"{recovery.Snapshot.RequestId:N}.response", response: true);
        if (!recovery.Result.Accepted)
            throw new InvalidDataException("Coordinator returned an uncommitted NPC checkpoint as recoverable.");
        ApplyResult(recovery);
    }

    public async Task WaitForPendingWritesAsync(CancellationToken cancellationToken = default)
    {
        await Ready.WaitAsync(cancellationToken).ConfigureAwait(false);
        while (!pendingNpcs.IsEmpty)
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
    }

    public async Task<NpcCheckpointRecoveryPageV1> GetRecoveryPageAsync(string systemId, Guid? after,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(systemId) || systemId.Length > 96)
            throw new ArgumentException("NPC checkpoint recovery system is invalid.", nameof(systemId));
        var canonicalSystemId = systemId.ToLowerInvariant();
        var url = $"{endpoint.AbsoluteUri.TrimEnd('/')}/recovery?instanceId={Uri.EscapeDataString(instance)}&systemId={Uri.EscapeDataString(canonicalSystemId)}&limit=128" +
                  (after.HasValue ? $"&afterCheckpointId={after.Value:D}" : "");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, new Uri(url));
        using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var page = await ReadBoundedJsonAsync<NpcCheckpointRecoveryPageV1>(response, timeout.Token)
            .ConfigureAwait(false) ?? throw new InvalidDataException("Coordinator returned an empty NPC recovery page.");
        if (page.CheckpointIds is not { Length: <= 128 } ||
            page.CheckpointIds.Any(id => id == Guid.Empty) ||
            !page.CheckpointIds.Select(id => id.ToString("D")).SequenceEqual(
                page.CheckpointIds.Select(id => id.ToString("D")).Order(StringComparer.Ordinal)) ||
            after.HasValue && page.CheckpointIds.Any(id => string.CompareOrdinal(id.ToString("D"), after.Value.ToString("D")) <= 0) ||
            page.NextAfterCheckpointId is { } next &&
                (page.CheckpointIds.Length == 0 || next != page.CheckpointIds[^1] || next == after))
            throw new InvalidDataException("Coordinator returned an invalid NPC checkpoint page.");
        return page;
    }

    public async Task<NpcCheckpointRecoveryV1?> GetRecoveryCheckpointAsync(Guid checkpointId, string systemId,
        CancellationToken cancellationToken = default)
    {
        if (checkpointId == Guid.Empty || string.IsNullOrWhiteSpace(systemId) || systemId.Length > 96)
            throw new ArgumentException("NPC checkpoint recovery identity is invalid.");
        var url = new Uri($"{endpoint.AbsoluteUri.TrimEnd('/')}/{checkpointId:D}/recovery?instanceId={Uri.EscapeDataString(instance)}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, url);
        using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null; // Ownership changed after page discovery; the Coordinator revalidates before disclosure.
        response.EnsureSuccessStatusCode();
        var recovery = await ReadBoundedJsonAsync<NpcCheckpointRecoveryV1>(response, timeout.Token)
            .ConfigureAwait(false) ?? throw new InvalidDataException("Coordinator returned an empty NPC checkpoint.");
        ValidateRecord(recovery, $"{checkpointId:N}.response", response: true);
        if (!recovery.Result.Accepted ||
            !string.Equals(recovery.Snapshot.SystemId, systemId, StringComparison.OrdinalIgnoreCase) ||
            recovery.Snapshot.Mission is not null || recovery.Snapshot.Npcs.Length == 0)
            throw new InvalidDataException("Coordinator returned an ineligible NPC checkpoint for recovery.");
        ApplyRecoveredCheckpoint(recovery);
        return recovery;
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private static async Task<T?> ReadBoundedJsonAsync<T>(HttpResponseMessage response,
        CancellationToken cancellationToken) where T : class
    {
        const int maximumResponseBytes = NpcCheckpointContractValidator.MaximumPayloadBytes * 2;
        if (response.Content.Headers.ContentLength is > maximumResponseBytes)
            throw new InvalidDataException("NPC checkpoint recovery response exceeds its size limit.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > maximumResponseBytes)
                throw new InvalidDataException("NPC checkpoint recovery response exceeds its size limit.");
            output.Write(buffer, 0, read);
        }
        return JsonSerializer.Deserialize<T>(output.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public (Task Durable, Task<NpcCheckpointWriteResponseV1> Completed) Queue(NpcCheckpointWriteRequestV1 request)
    {
        NpcCheckpointContractValidator.Validate(request);
        if (request.InstanceId != instance)
            throw new ArgumentException("NPC checkpoint belongs to a different instance.");
        foreach (var member in request.ExpectedRevisions)
        {
            var current = GetRevision(member.NpcId, member.OwnershipVersion);
            if (current != member.Revision || !pendingNpcs.TryAdd(member.NpcId, 0))
            {
                foreach (var reserved in request.ExpectedRevisions.TakeWhile(value => value.NpcId != member.NpcId))
                    pendingNpcs.TryRemove(reserved.NpcId, out _);
                throw new InvalidOperationException("NPC checkpoint is stale or another write is pending for this member.");
            }
        }
        var item = new Item(request, new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously));
        if (!intake.Writer.TryWrite(item))
        {
            foreach (var member in request.ExpectedRevisions) pendingNpcs.TryRemove(member.NpcId, out _);
            throw new InvalidOperationException("NPC checkpoint persistence queue is full or closed.");
        }
        return (item.Durable.Task, item.Completed.Task);
    }

    private async Task PersistAsync()
    {
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var path in Directory.EnumerateFiles(directory, "*.response").Order())
            {
                if (new FileInfo(path).Length > MaximumRecordBytes)
                    throw new InvalidDataException("NPC checkpoint response record exceeds its limit.");
                var record = MessagePackSerializer.Deserialize<NpcCheckpointRecoveryV1>(File.ReadAllBytes(path),
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                ValidateRecord(record, path, response: true);
                ApplyResult(record);
            }
            foreach (var path in Directory.EnumerateFiles(directory, "*.pending").Order())
            {
                if (new FileInfo(path).Length > NpcCheckpointContractValidator.MaximumPayloadBytes)
                    throw new InvalidDataException("NPC checkpoint request exceeds its limit.");
                var request = MessagePackSerializer.Deserialize<NpcCheckpointWriteRequestV1>(File.ReadAllBytes(path),
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                NpcCheckpointContractValidator.Validate(request);
                if (request.InstanceId != instance || Path.GetFileName(path) != $"{request.RequestId:N}.pending")
                    throw new InvalidDataException("NPC checkpoint request has invalid identity.");
                MarkPending(request, true);
                var item = new Item(request, new(TaskCreationOptions.RunContinuationsAsynchronously),
                    new(TaskCreationOptions.RunContinuationsAsynchronously));
                item.Durable.TrySetResult();
                await outgoing.Writer.WriteAsync(item, stopping.Token);
            }
            ready.TrySetResult();
            await foreach (var item in intake.Reader.ReadAllAsync(stopping.Token))
            {
                try
                {
                    var path = Path.Combine(directory, $"{item.Request.RequestId:N}.pending");
                    var bytes = MessagePackSerializer.Serialize(item.Request);
                    if (File.Exists(path) && !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                        throw new InvalidDataException("NPC checkpoint request ID conflicts with durable intent.");
                    WriteAtomic(path, bytes);
                    MarkPending(item.Request, true);
                    item.Durable.TrySetResult();
                    await outgoing.Writer.WriteAsync(item, stopping.Token);
                }
                catch (Exception exception)
                {
                    MarkPending(item.Request, false);
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
                MarkPending(item.Request, false);
                item.Durable.TrySetException(exception);
                item.Completed.TrySetException(exception);
            }
            throw;
        }
        finally { outgoing.Writer.TryComplete(); }
    }

    private async Task DeliverAsync()
    {
        try
        {
            await foreach (var item in outgoing.Reader.ReadAllAsync(stopping.Token))
            {
                var delay = 100;
                while (!stopping.IsCancellationRequested)
                {
                    try
                    {
                        using var request = CreateAuthenticatedRequest(HttpMethod.Post, endpoint);
                        request.Content = JsonContent.Create(item.Request);
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                        timeout.CancelAfter(TimeSpan.FromSeconds(4));
                        using var reply = await http.SendAsync(request, timeout.Token);
                        var response = await reply.Content.ReadFromJsonAsync<NpcCheckpointWriteResponseV1>(timeout.Token);
                        if (response is null || response.RequestId != item.Request.RequestId ||
                            response.Accepted && (response.Revisions is null ||
                                response.Revisions.Length != item.Request.ExpectedRevisions.Length ||
                                response.Revisions.Any(result => result is null ||
                                    !item.Request.ExpectedRevisions.Any(expected => expected.NpcId == result.NpcId &&
                                        result.Revision == expected.Revision + 1 &&
                                        result.OwnershipVersion == expected.OwnershipVersion +
                                            (item.Request.Retirements.Any(retired => retired.NpcId == result.NpcId) ? 1 : 0)))))
                            throw new InvalidDataException("NPC checkpoint response does not match its durable request.");
                        var record = new NpcCheckpointRecoveryV1 { Snapshot = item.Request, Result = response };
                        WriteAtomic(Path.Combine(directory, $"{item.Request.RequestId:N}.response"),
                            MessagePackSerializer.Serialize(record));
                        ApplyResult(record);
                        File.Delete(Path.Combine(directory, $"{item.Request.RequestId:N}.pending"));
                        MarkPending(item.Request, false);
                        item.Completed.TrySetResult(response);
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
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        finally { while (outgoing.Reader.TryRead(out var item)) item.Completed.TrySetCanceled(); }
    }

    private void ApplyResult(NpcCheckpointRecoveryV1 record)
    {
        if (!record.Result.Accepted) return;
        foreach (var revision in record.Result.Revisions)
        {
            if (record.Snapshot.Retirements.Any(entry => entry.NpcId == revision.NpcId))
                revisions.TryRemove(revision.NpcId, out _);
            else
                revisions.AddOrUpdate(revision.NpcId, (revision.OwnershipVersion, revision.Revision),
                    (_, current) => revision.OwnershipVersion > current.Version ||
                                    revision.OwnershipVersion == current.Version && revision.Revision > current.Revision
                        ? (revision.OwnershipVersion, revision.Revision) : current);
        }
    }

    private void MarkPending(NpcCheckpointWriteRequestV1 request, bool value)
    {
        foreach (var member in request.ExpectedRevisions)
            if (value) pendingNpcs[member.NpcId] = 0;
            else pendingNpcs.TryRemove(member.NpcId, out _);
    }

    private void ValidateRecord(NpcCheckpointRecoveryV1 record, string path, bool response)
    {
        NpcCheckpointContractValidator.Validate(record.Snapshot);
        if (record.Snapshot.InstanceId != instance || record.Result.RequestId != record.Snapshot.RequestId ||
            response && Path.GetFileName(path) != $"{record.Snapshot.RequestId:N}.response")
            throw new InvalidDataException("NPC checkpoint response record has invalid identity.");
        if (response && record.Result.Accepted &&
            (record.Result.Revisions is null || record.Result.Revisions.Length != record.Snapshot.ExpectedRevisions.Length ||
             record.Result.Revisions.Any(result => result is null ||
                 !record.Snapshot.ExpectedRevisions.Any(expected => expected.NpcId == result.NpcId &&
                     result.Revision == expected.Revision + 1 &&
                     result.OwnershipVersion == expected.OwnershipVersion +
                         (record.Snapshot.Retirements.Any(retired => retired.NpcId == result.NpcId) ? 1 : 0)))))
            throw new InvalidDataException("NPC checkpoint response revisions do not match its request.");
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            { file.Write(bytes); file.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async ValueTask DisposeAsync()
    {
        intake.Writer.TryComplete();
        try { await persistence.ConfigureAwait(false); } catch { }
        stopping.Cancel();
        try { await delivery.ConfigureAwait(false); } catch { }
        http.Dispose();
        stopping.Dispose();
    }

    private sealed record Item(NpcCheckpointWriteRequestV1 Request,
        TaskCompletionSource Durable, TaskCompletionSource<NpcCheckpointWriteResponseV1> Completed);
}
