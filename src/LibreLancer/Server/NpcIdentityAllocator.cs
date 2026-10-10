using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using LancerNexus.Protocol;

namespace LibreLancer.Server;

/// <summary>
/// Keeps a small coordinator-issued NPC ID block ready. Spawns consume the local queue;
/// HTTP and MySQL latency is handled by one background worker per instance/system pair.
/// </summary>
public sealed class NpcIdentityAllocator : IDisposable
{
    private const ushort BatchSize = 128;
    private const int RefillThreshold = 32;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);
    private readonly object sync = new();
    private readonly Queue<NpcOwnershipLease> available = new();
    private readonly Queue<Action<NpcOwnershipLease>> waiting = new();
    private readonly Channel<byte> refill = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly HttpClient http;
    private readonly Uri endpoint;
    private readonly string instanceId;
    private readonly string systemId;
    private readonly CancellationTokenSource shutdown = new();
    private readonly Task worker;
    private bool warned;

    public NpcIdentityAllocator(Uri coordinatorBaseUri, string apiKey, string instanceId, string systemId)
        : this(coordinatorBaseUri, apiKey, instanceId, systemId,
            new HttpClientHandler { AllowAutoRedirect = false })
    {
    }

    internal NpcIdentityAllocator(
        Uri coordinatorBaseUri,
        string apiKey,
        string instanceId,
        string systemId,
        HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(coordinatorBaseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);
        if (!coordinatorBaseUri.IsAbsoluteUri ||
            (coordinatorBaseUri.Scheme != Uri.UriSchemeHttps &&
             !(coordinatorBaseUri.Scheme == Uri.UriSchemeHttp && coordinatorBaseUri.IsLoopback)))
            throw new ArgumentException("Coordinator URL must use HTTPS (HTTP is allowed for loopback development).", nameof(coordinatorBaseUri));

        endpoint = new Uri(coordinatorBaseUri, "/internal/v1/npcs/allocate");
        this.instanceId = instanceId;
        this.systemId = systemId;
        http = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        worker = Task.Run(AllocationWorkerAsync);
        SignalRefill();
    }

    public bool TryTake(out Guid npcId)
    {
        if (TryTakeLease(out var lease))
        {
            npcId = lease.NpcId;
            return true;
        }
        npcId = Guid.Empty;
        return false;
    }

    public bool TryTakeLease(out NpcOwnershipLease lease)
    {
        var shouldRefill = false;
        lock (sync)
        {
            if (available.TryDequeue(out lease!))
            {
                shouldRefill = available.Count <= RefillThreshold;
            }
            else
            {
                shouldRefill = true;
                lease = null!;
            }
        }
        if (shouldRefill)
            SignalRefill();
        return lease is not null;
    }

    /// <summary>Queues a non-blocking continuation for a spawn that arrived during pool refill.</summary>
    public void WhenAvailable(Action<Guid> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        WhenLeaseAvailable(lease => continuation(lease.NpcId));
    }

    public void WhenLeaseAvailable(Action<NpcOwnershipLease> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        NpcOwnershipLease? allocated = null;
        lock (sync)
        {
            if (!available.TryDequeue(out allocated))
                waiting.Enqueue(continuation);
        }
        if (allocated is not null)
        {
            continuation(allocated);
            SignalRefillIfLow();
        }
        else
        {
            SignalRefill();
        }
    }

    private async Task AllocationWorkerAsync()
    {
        try
        {
            await foreach (var _ in refill.Reader.ReadAllAsync(shutdown.Token))
            {
                lock (sync)
                {
                    if (available.Count > RefillThreshold && waiting.Count == 0)
                        continue;
                }
                while (!shutdown.IsCancellationRequested)
                {
                    ushort count;
                    lock (sync)
                    {
                        var needed = BatchSize - available.Count;
                        if (needed <= 0 && waiting.Count == 0)
                            break;
                        count = (ushort)Math.Clamp(needed, 1, BatchSize);
                    }

                    var leases = await RequestBatchWithRetryAsync(count, shutdown.Token);
                    List<(Action<NpcOwnershipLease> Continuation, NpcOwnershipLease Lease)> callbacks = [];
                    lock (sync)
                    {
                        foreach (var lease in leases)
                        {
                            if (waiting.TryDequeue(out var continuation))
                                callbacks.Add((continuation, lease));
                            else
                                available.Enqueue(lease);
                        }
                    }
                    foreach (var callback in callbacks)
                    {
                        try
                        {
                            callback.Continuation(callback.Lease);
                        }
                        catch (Exception exception)
                        {
                            FLLog.Error("NPC", $"Could not deliver allocated NPC identity: {exception.Message}");
                        }
                    }

                    lock (sync)
                    {
                        if (available.Count > RefillThreshold && waiting.Count == 0)
                            break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task<NpcOwnershipLease[]> RequestBatchWithRetryAsync(ushort count, CancellationToken cancellationToken)
    {
        var request = new NpcIdBatchAllocationRequest
        {
            RequestId = Guid.NewGuid(),
            InstanceId = instanceId,
            SystemId = systemId,
            Count = count
        };
        var retryDelay = TimeSpan.FromMilliseconds(250);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(RequestTimeout);
                using var response = await http.PostAsJsonAsync(endpoint, request, timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<NpcIdBatchAllocationResponse>(timeout.Token);
                    if (result is { Accepted: true } && result.RequestId == request.RequestId &&
                        result.Npcs is { Length: > 0 } && result.Npcs.Length == count &&
                        result.Npcs.All(lease => lease is not null && lease.NpcId != Guid.Empty &&
                                                 lease.OwnershipVersion == 1 && !lease.IsRetired &&
                                                 string.Equals(lease.InstanceId, instanceId, StringComparison.Ordinal)) &&
                        result.Npcs.Select(lease => lease.NpcId).Distinct().Count() == result.Npcs.Length)
                        return result.Npcs;

                    throw new InvalidDataException("Coordinator returned an invalid NPC identity batch.");
                }
                throw new HttpRequestException($"Coordinator rejected NPC ID allocation with HTTP {(int)response.StatusCode}.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or OperationCanceledException or InvalidDataException)
            {
                if (!warned)
                {
                    warned = true;
                    FLLog.Warning("NPC", $"Coordinator NPC identity allocation is unavailable; pending spawns are queued: {exception.Message}");
                }
                await Task.Delay(retryDelay, cancellationToken);
                retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 2, 10_000));
            }
        }
        throw new OperationCanceledException(cancellationToken);
    }

    private void SignalRefill() => refill.Writer.TryWrite(0);

    private void SignalRefillIfLow()
    {
        lock (sync)
        {
            if (available.Count > RefillThreshold && waiting.Count == 0)
                return;
        }
        SignalRefill();
    }

    public void Dispose()
    {
        shutdown.Cancel();
        refill.Writer.TryComplete();
        try
        {
            worker.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        http.Dispose();
        shutdown.Dispose();
    }
}
