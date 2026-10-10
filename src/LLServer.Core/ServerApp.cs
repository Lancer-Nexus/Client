using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LibreLancer;
using LibreLancer.Data;
using LibreLancer.Data.IO;
using LibreLancer.Net;
using LibreLancer.Server;
using Nexus.Assets;
using LancerNexus.Protocol;
using Microsoft.EntityFrameworkCore;

namespace LLServer;

public class ServerApp(ServerConfig config)
{
    public GameServer? Server;
    private readonly ServerConfig Config = config;
    private CancellationTokenSource? statusCancellation;
    private Task? statusWriter;
    private CancellationTokenSource? transferResolutionCancellation;
    private Task? transferResolver;
    private NpcTransferQuicReceiver? npcTransferReceiver;
    private X509Certificate2? npcTransferServerCertificate;
    private X509Certificate2? npcTransferClientCaCertificate;
#if LANCER_NEXUS_FLHOOKCOMPAT
    private FlHookCompatRuntime? flHookCompatRuntime;
#endif
#if LANCER_NEXUS_CLUSTER
    private ClusterPermissionSyncRuntime? permissionSyncRuntime;
#endif

    public bool StartServer()
    {
        if (!string.IsNullOrWhiteSpace(Config.LoginUrl) &&
            !Config.LoginUrl.StartsWith("https://"))
        {
            FLLog.Error("Config", "Only HTTPS login servers are supported");
            return false;
        }
        if (Config.TestNewCharacterRank is < 0 or > 100)
        {
            FLLog.Error("Config", "TestNewCharacterRank must be between 0 and 100");
            return false;
        }
        if (Config.TestMissionNickname is { Length: > 128 })
        {
            FLLog.Error("Config", "TestMissionNickname must be no longer than 128 characters");
            return false;
        }
#if !LANCER_NEXUS_CLUSTER
        if (!string.IsNullOrWhiteSpace(Config.InstanceId))
        {
            FLLog.Error("Permissions", "Cluster instances require a build with EnableClusterIntegration=true; refusing to start without permission revision synchronization.");
            return false;
        }
#endif
        if (!GameConfig.CheckFLDirectory(Config.FreelancerPath))
        {
            FLLog.Error("Config", $"'{Config.FreelancerPath ?? "NULL"}' is not a valid game folder");
            return false;
        }
        if (!string.IsNullOrWhiteSpace(Config.RuntimeStatusFile) &&
            (string.IsNullOrWhiteSpace(Config.InstanceId) || string.IsNullOrWhiteSpace(Config.SystemId) ||
             string.IsNullOrWhiteSpace(Config.InstanceEndpoint)))
            throw new InvalidOperationException("Runtime status requires InstanceId, SystemId and InstanceEndpoint.");
        if (Config.SystemIds is null || Config.SystemIds.Length > 128 ||
            Config.SystemIds.Any(string.IsNullOrWhiteSpace) ||
            Config.SystemIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Config.SystemIds.Length ||
            (Config.SystemIds.Length > 0 && !Config.SystemIds.Contains(Config.SystemId, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("SystemIds must be unique and include the primary SystemId.");
        var ctxFactory = new SqlDesignTimeFactory(Config.DatabasePath);
        using (var ctx = ctxFactory.CreateDbContext([]))
        {
            // Migration creates a new SQLite database for a freshly prepared instance.
            if (ctx.Database.GetPendingMigrations().Any())
            {
                FLLog.Info("Server", "Migrating database");
                ctx.Database.Migrate();
            }
            if (!ctx.Database.CanConnect())
            {
                FLLog.Error("Server", "Local SQLite database connection failed");
                return false;
            }
            FLLog.Info("Server", "Local SQLite database connection established");
        }

        var transferInstanceKey = Environment.GetEnvironmentVariable("LANCER_NEXUS_GAME_INSTANCE_KEY");
        var npcCoordinatorApiKey = Environment.GetEnvironmentVariable("LANCER_NEXUS_COORDINATOR_API_KEY");
        var validNpcCoordinatorUrl = Uri.TryCreate(Config.NpcCoordinatorUrl, UriKind.Absolute, out var npcCoordinatorUri) &&
                                     (npcCoordinatorUri.Scheme == Uri.UriSchemeHttps ||
                                      (npcCoordinatorUri.Scheme == Uri.UriSchemeHttp && npcCoordinatorUri.IsLoopback)) &&
                                     string.IsNullOrEmpty(npcCoordinatorUri.UserInfo) &&
                                     string.IsNullOrEmpty(npcCoordinatorUri.Query) &&
                                     string.IsNullOrEmpty(npcCoordinatorUri.Fragment);
        if (!string.IsNullOrWhiteSpace(Config.InstanceId) &&
            (!validNpcCoordinatorUrl || string.IsNullOrWhiteSpace(npcCoordinatorApiKey) ||
             Encoding.UTF8.GetByteCount(npcCoordinatorApiKey) < 32))
        {
            FLLog.Error("Config", "Cluster NPC IDs require a private HTTPS NpcCoordinatorUrl and a 32-byte LANCER_NEXUS_COORDINATOR_API_KEY.");
            return false;
        }
        var gameFiles = FileSystem.FromPath(Config.FreelancerPath);
        var npcDataOverlay = Path.Combine(GetBasePath(), "lib", "data");
        if (!Directory.Exists(npcDataOverlay))
            npcDataOverlay = Path.Combine(GetBasePath(), "data");
        if (Directory.Exists(npcDataOverlay))
            gameFiles.FileProviders.Add(new NpcDataOverlayFileProvider(npcDataOverlay));
        var packageProvider = NexusPackageFileProvider.LoadActive(GetBasePath());
        if (packageProvider is not null)
            gameFiles.FileProviders.Add(packageProvider);
        Server = new GameServer(gameFiles)
        {
            DbContextFactory = ctxFactory,
            ServerName = Config.ServerName,
            ServerDescription = Config.ServerDescription,
            ScriptsFolder = Path.Combine(GetBasePath(), "scripts"),
            LoginUrl = Config.LoginUrl,
            InstanceId = Config.InstanceId,
            NpcCoordinatorUrl = Config.NpcCoordinatorUrl,
            NpcTransferPort = Config.NpcTransferPort,
            SystemId = Config.SystemId,
            SystemIds = Config.SystemIds,
            TransferInstanceKey = transferInstanceKey,
            TestNewCharacterRank = Config.TestNewCharacterRank,
            TestMissionNickname = Config.TestMissionNickname
        };
        Server.ConfigureNpcIdentityAllocation(npcCoordinatorApiKey);
        if (Config.NpcTransferPort != 0)
        {
            if (!OperatingSystem.IsLinux() || !QuicListener.IsSupported)
                throw new PlatformNotSupportedException("Configured NPC transfer listener requires Linux with MsQuic support.");
            if (string.IsNullOrWhiteSpace(Config.InstanceId) || string.IsNullOrWhiteSpace(Config.NpcTransferListenAddress) ||
                Config.NpcTransferPort is < 1 or > 65535 || string.IsNullOrWhiteSpace(Config.NpcTransferServerCertificate) ||
                string.IsNullOrWhiteSpace(Config.NpcTransferClientCaCertificate) || string.IsNullOrWhiteSpace(Config.NpcTransferStagingDirectory) ||
                !IPAddress.TryParse(Config.NpcTransferListenAddress, out var npcTransferAddress) ||
                npcTransferAddress.Equals(IPAddress.Any) || npcTransferAddress.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("NPC transfer listener requires an instance ID, private bind address, valid port, certificates and a staging directory.");

            var certificatePassword = Environment.GetEnvironmentVariable("LANCER_NEXUS_NPC_TRANSFER_CERT_PASSWORD");
            npcTransferServerCertificate = new X509Certificate2(
                Path.GetFullPath(Config.NpcTransferServerCertificate, GetBasePath()), certificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);
            npcTransferClientCaCertificate = X509Certificate2.CreateFromPem(
                File.ReadAllText(Path.GetFullPath(Config.NpcTransferClientCaCertificate, GetBasePath())));
            Server.NpcTransferCertificate = npcTransferServerCertificate;
            Server.NpcTransferCaCertificate = npcTransferClientCaCertificate;
            Server.NpcTransferStagingDirectory = Path.GetFullPath(Config.NpcTransferStagingDirectory, GetBasePath());
            npcTransferReceiver = new NpcTransferQuicReceiver(
                Config.InstanceId!, npcTransferServerCertificate, npcTransferClientCaCertificate,
                npcCoordinatorUri!, npcCoordinatorApiKey!, Server.StageIncomingNpcTransferAsync);
            npcTransferReceiver.StartAsync(new IPEndPoint(npcTransferAddress, Config.NpcTransferPort)).GetAwaiter().GetResult();
            FLLog.Info("NPC Transfer", $"Private mTLS QUIC receiver listening on {npcTransferAddress}:{Config.NpcTransferPort}");
        }
        Server.ConfigureLocalOperators(Path.GetFullPath(Config.LocalOperatorsFile, GetBasePath()));
        if (Config.TestNewCharacterRank is int testRank)
            FLLog.Info("Server", $"Test new-character rank override enabled: {testRank}");
        if (!string.IsNullOrWhiteSpace(Config.TestMissionNickname))
            FLLog.Info("Server", $"Test mission runtime injection enabled: {Config.TestMissionNickname}");
        var listener = Server.Listener ?? throw new InvalidOperationException("Game server listener was not initialized.");
        listener.Port = Config.Port > 0 ? Config.Port : LNetConst.DEFAULT_PORT;
        listener.BindAddress = Config.BindAddress;
        listener.MaxConnections = Config.MaxPlayers > 0 ? Config.MaxPlayers : 200;
        if(Config.ThreadCount > 0)
            Server.ThreadCount = Config.ThreadCount;
#if LANCER_NEXUS_FLHOOKCOMPAT
        if (Config.FlHookCompatEnabled)
        {
            if (string.IsNullOrWhiteSpace(Config.InstanceId) || string.IsNullOrWhiteSpace(Config.LoginUrl))
                throw new InvalidOperationException("FLHookCompat requires a clustered instance and Gateway login.");
            flHookCompatRuntime = new FlHookCompatRuntime(Config, GetBasePath(), Config.InstanceId);
            try
            {
                flHookCompatRuntime.StartAsync().GetAwaiter().GetResult();
                Server.ServerEventObserver = flHookCompatRuntime.TryPublish;
            }
            catch
            {
                flHookCompatRuntime.DisposeAsync().AsTask().GetAwaiter().GetResult();
                flHookCompatRuntime = null;
                throw;
            }
        }
        else if (Config.FlHookCompatClusterEventsEnabled)
        {
            throw new InvalidOperationException("FLHook cluster event publishing requires FlHookCompatEnabled=true.");
        }
#else
        if (Config.FlHookCompatEnabled || Config.FlHookCompatClusterEventsEnabled)
            throw new InvalidOperationException("FLHookCompat is enabled in configuration, but this LLServer build has no FLHookCompat support. Build with EnableFlHookCompat=true.");
#endif
#if LANCER_NEXUS_CLUSTER
        if (!string.IsNullOrWhiteSpace(Config.InstanceId))
        {
            if (string.IsNullOrWhiteSpace(Config.LoginUrl))
                throw new InvalidOperationException("Cluster permission synchronization requires Gateway LoginUrl.");
            var permissionInstanceKey = Environment.GetEnvironmentVariable("LANCER_NEXUS_GAME_INSTANCE_KEY");
            var permissionRedisEndpoint = Environment.GetEnvironmentVariable("LANCER_NEXUS_PERMISSION_REDIS_ENDPOINT");
            permissionSyncRuntime = new ClusterPermissionSyncRuntime(Config.InstanceId!, Config.LoginUrl, permissionInstanceKey,
                permissionRedisEndpoint);
            permissionSyncRuntime.Start();
        }
#endif
        Server.Start();
        if ((!string.IsNullOrWhiteSpace(Config.InstanceId) && !string.IsNullOrWhiteSpace(transferInstanceKey)) ||
            npcTransferReceiver != null)
        {
            transferResolutionCancellation = new CancellationTokenSource();
            transferResolver = ResolveTransfersAsync(transferResolutionCancellation.Token);
            FLLog.Info("Server", "Automatic source transfer resolution enabled");
        }
        if (!string.IsNullOrWhiteSpace(Config.RuntimeStatusFile))
        {
            var statusPath = Path.GetFullPath(Config.RuntimeStatusFile, Platform.GetBasePath());
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
            File.Delete(statusPath);
            statusCancellation = new CancellationTokenSource();
            statusWriter = WriteRuntimeStatusAsync(statusCancellation.Token);
            FLLog.Info("Server", $"Instance status reporting enabled for {Config.InstanceId} on system {Config.SystemId}");
        }
        FLLog.Info("Server", $"Game server startup initiated on port {listener.Port} with capacity {listener.MaxConnections}");
        return true;
    }

    public void WaitExit()
    {
        Server?.JoinThread();
    }

    private static string GetBasePath()
    {
        using var processModule = Process.GetCurrentProcess().MainModule;
        return Path.GetDirectoryName(processModule?.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    public void StopServer()
    {
        transferResolutionCancellation?.Cancel();
        try { transferResolver?.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            FLLog.Error("Server", $"Transfer resolver stopped: {exception.Message}");
        }
        statusCancellation?.Cancel();
#if LANCER_NEXUS_CLUSTER
        if (permissionSyncRuntime is not null)
        {
            try { permissionSyncRuntime.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception exception) { FLLog.Error("Permissions", $"Revision listener stopped: {exception.Message}"); }
            permissionSyncRuntime = null;
        }
#endif
        if (npcTransferReceiver != null)
        {
            try { npcTransferReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception exception) { FLLog.Error("NPC Transfer", $"Receiver stopped: {exception.Message}"); }
            npcTransferReceiver = null;
        }
        npcTransferServerCertificate?.Dispose();
        npcTransferServerCertificate = null;
        npcTransferClientCaCertificate?.Dispose();
        npcTransferClientCaCertificate = null;
#if LANCER_NEXUS_FLHOOKCOMPAT
        if (Server is not null) Server.ServerEventObserver = null;
#endif
        Server?.Stop();
#if LANCER_NEXUS_FLHOOKCOMPAT
        if (flHookCompatRuntime is not null)
        {
            try { flHookCompatRuntime.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception exception) { FLLog.Error("FLHookCompat", $"Plugin host stopped: {exception.Message}"); }
            flHookCompatRuntime = null;
        }
#endif
        Server = null;
        try { statusWriter?.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            FLLog.Error("Server", $"Runtime status writer stopped: {exception.Message}");
        }
        if (!string.IsNullOrWhiteSpace(Config.RuntimeStatusFile))
        {
            var statusPath = Path.GetFullPath(Config.RuntimeStatusFile, Platform.GetBasePath());
            if (File.Exists(statusPath))
                File.Delete(statusPath);
        }
        statusCancellation?.Dispose();
        statusCancellation = null;
        statusWriter = null;
        transferResolutionCancellation?.Dispose();
        transferResolutionCancellation = null;
        transferResolver = null;
    }

    private async Task ResolveTransfersAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Config.InstanceId) &&
                    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LANCER_NEXUS_GAME_INSTANCE_KEY")))
                {
                    await Server!.InitializeNpcRetirementOutboxAsync();
                    await Server!.ResolvePendingSourceTransfersAsync(cancellationToken);
                    await Server.ResolvePendingSourceNpcTransfersAsync(cancellationToken);
                }
                await Server!.ResolvePendingNpcTransfersAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                FLLog.Error("Transfer", $"Automatic transfer resolution failed: {exception.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private async Task WriteRuntimeStatusAsync(CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(Config.RuntimeStatusFile!, Platform.GetBasePath());
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        while (!cancellationToken.IsCancellationRequested)
        {
            var listener = Server?.Listener;
            var network = listener?.Server;
            var snapshot = new InstanceRuntimeStatus
            {
                WrittenAtUtc = DateTimeOffset.UtcNow,
                InstanceId = Config.InstanceId!,
                SystemId = Config.SystemId!,
                SystemIds = Config.SystemIds,
                IsReady = network?.IsRunning == true && ClusterPermissionsReady && ClusterFlHookEventCaptureReady,
                PermissionsReady = ClusterPermissionsReady,
                FlHookEventCaptureReady = ClusterFlHookEventCaptureReady,
                PermissionRevision = CurrentPermissionRevision,
                IsDraining = !string.IsNullOrWhiteSpace(Config.DrainFlagFile) &&
                             File.Exists(Path.GetFullPath(Config.DrainFlagFile, Platform.GetBasePath())),
                CurrentPlayers = network?.ConnectedPeersCount ?? 0,
                MaxPlayers = listener?.MaxConnections ?? 0,
                Endpoint = Config.InstanceEndpoint!,
                NpcTransferEndpoint = npcTransferReceiver is null ? null :
                    new UriBuilder("quic", Config.NpcTransferListenAddress!, Config.NpcTransferPort).Uri.AbsoluteUri,
                Capabilities = npcTransferReceiver is null ? [] : [ClusterCapabilities.NpcTransferV1]
            };
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(snapshot), cancellationToken);
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private bool ClusterPermissionsReady
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Config.InstanceId)) return true;
#if LANCER_NEXUS_CLUSTER
            return permissionSyncRuntime?.IsReady == true;
#else
            return false;
#endif
        }
    }

    private bool ClusterFlHookEventCaptureReady => !Config.FlHookCompatClusterEventsEnabled ||
        flHookCompatRuntime?.ClusterEventCaptureReady == true;

    private long CurrentPermissionRevision
    {
        get
        {
#if LANCER_NEXUS_CLUSTER
            return permissionSyncRuntime?.Revision ?? 0;
#else
            return 0;
#endif
        }
    }
}

public sealed class InstanceRuntimeStatus
{
    public string? NpcTransferEndpoint { get; init; }
    public DateTimeOffset WrittenAtUtc { get; init; }
    public string InstanceId { get; init; } = "";
    public string SystemId { get; init; } = "";
    public string[] SystemIds { get; init; } = [];
    public bool IsReady { get; init; }
    public bool IsDraining { get; init; }
    public int CurrentPlayers { get; init; }
    public int MaxPlayers { get; init; }
    public bool PermissionsReady { get; init; }
    public bool FlHookEventCaptureReady { get; init; }
    public long PermissionRevision { get; init; }
    public string Endpoint { get; init; } = "";
    public string[] Capabilities { get; init; } = [];
}
