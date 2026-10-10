#if LANCER_NEXUS_CLUSTER
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LancerNexus.Cluster;
using LibreLancer;

namespace LLServer;

/// <summary>Owns SQL snapshot synchronization and its Redis invalidation listener for one cluster instance.</summary>
internal sealed class ClusterPermissionSyncRuntime : IAsyncDisposable
{
    private readonly PermissionService permissions = new(new PermissionSnapshot(0, [], []));
    private readonly PermissionSyncCoordinator coordinator;
    private readonly RedisPermissionRevisionListener listener;
    private readonly HttpClient http;
    private readonly CancellationTokenSource cancellation = new();
    private Task? listenerTask;

    public ClusterPermissionSyncRuntime(string instanceId, string gatewayUrl, string? instanceKey, string? redisEndpoint)
    {
        if (!Uri.TryCreate(gatewayUrl, UriKind.Absolute, out var gatewayUri) ||
            gatewayUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(gatewayUri.UserInfo) ||
            !string.IsNullOrEmpty(gatewayUri.Query) || !string.IsNullOrEmpty(gatewayUri.Fragment))
            throw new InvalidOperationException("Cluster permission synchronization requires a private HTTPS Gateway URL.");
        if (string.IsNullOrWhiteSpace(instanceKey) || Encoding.UTF8.GetByteCount(instanceKey) < 32)
            throw new InvalidOperationException("Cluster permission synchronization requires the per-instance LANCER_NEXUS_GAME_INSTANCE_KEY.");
        if (!IsValidRedisEndpoint(redisEndpoint))
            throw new InvalidOperationException("Cluster permission synchronization requires LANCER_NEXUS_PERMISSION_REDIS_ENDPOINT in host:port form.");

        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        var gateway = new GatewayPermissionSyncClient(http, gatewayUri, instanceKey);
        coordinator = new PermissionSyncCoordinator(instanceId,
            permissions, gateway, gateway, TimeProvider.System);
        listener = new RedisPermissionRevisionListener(redisEndpoint!, permissions, coordinator,
            exception => FLLog.Error("Permissions", $"Revision synchronization unavailable: {exception.GetType().Name}: {exception.Message}"));
    }

    public bool IsReady => listener.IsReady && coordinator.IsReady && permissions.IsSynchronized;
    public long Revision => permissions.Revision;

    public void Start()
    {
        listenerTask = Task.Run(() => listener.RunAsync(cancellation.Token));
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        if (listenerTask is not null)
        {
            try { await listenerTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            listenerTask = null;
        }
        http.Dispose();
        cancellation.Dispose();
    }

    private static bool IsValidRedisEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return false;
        var separator = endpoint.LastIndexOf(':');
        return separator > 0 && endpoint[..separator].Trim().Length > 0 &&
               int.TryParse(endpoint[(separator + 1)..], out var port) && port is > 0 and <= 65535;
    }
}
#endif
