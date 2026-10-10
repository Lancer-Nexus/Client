#if LANCER_NEXUS_FLHOOKCOMPAT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LancerNexus.FlhookCompat;
using LancerNexus.FlhookCompat.Hosting;
using LibreLancer;
using LibreLancer.Server;

namespace LLServer;

/// <summary>Optional bridge from authenticated local LLServer events to trusted managed plugins.</summary>
internal sealed class FlHookCompatRuntime : IAsyncDisposable
{
    private static readonly Version HostApiVersion = new(1, 2, 0);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ServerConfig config;
    private readonly string basePath;
    private readonly FlHookCompatServerEventHub eventHub;
    private CoordinatorFlHookEventPublisher? clusterEventPublisher;
    private readonly Channel<PendingEvent> pending = Channel.CreateBounded<PendingEvent>(new BoundedChannelOptions(4096)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly List<LoadedManagedPlugin> plugins = [];
    private Task? dispatcher;
    private int queueOverflowLogged;

    public bool ClusterEventCaptureReady => !config.FlHookCompatClusterEventsEnabled ||
        dispatcher is { IsCompleted: false } && Volatile.Read(ref queueOverflowLogged) == 0;

    public FlHookCompatRuntime(ServerConfig config, string basePath, string instanceId)
    {
        this.config = config;
        this.basePath = basePath;
        string? durableOutboxPath = null;
        if (config.FlHookCompatClusterEventsEnabled)
        {
            durableOutboxPath = Path.GetFullPath(config.FlHookCompatEventOutboxFile ??
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(config.DatabasePath, basePath))!,
                    "flhook-events.outbox.jsonl"), basePath);
        }
        eventHub = new FlHookCompatServerEventHub(instanceId, durableJournalPath: durableOutboxPath);
    }

    public async Task StartAsync()
    {
        if (config.FlHookCompatPluginIds is null || config.FlHookCompatApprovedCapabilities is null ||
            config.FlHookCompatPluginIds.Distinct(StringComparer.Ordinal).Count() != config.FlHookCompatPluginIds.Length ||
            config.FlHookCompatApprovedCapabilities.Distinct(StringComparer.Ordinal).Count() != config.FlHookCompatApprovedCapabilities.Length)
            throw new InvalidOperationException("FLHookCompat plugin assignments and capabilities must be unique lists.");

        if (config.FlHookCompatClusterEventsEnabled)
        {
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException("Coordinator FLHook event publishing currently requires Linux QUIC support.");
            if (config.CoordinatorQuicEndpoint is null ||
                !IPEndPoint.TryParse(config.CoordinatorQuicEndpoint, out var coordinatorEndPoint) ||
                string.IsNullOrWhiteSpace(config.CoordinatorQuicServerName) ||
                string.IsNullOrWhiteSpace(config.ClusterAgentNodeId) ||
                string.IsNullOrWhiteSpace(config.CoordinatorQuicClientCertificate) ||
                string.IsNullOrWhiteSpace(config.CoordinatorQuicCaCertificate))
                throw new InvalidOperationException("FLHook cluster events require CoordinatorQuicEndpoint, CoordinatorQuicServerName, ClusterAgentNodeId, CoordinatorQuicClientCertificate and CoordinatorQuicCaCertificate.");

            var certificatePath = Path.GetFullPath(config.CoordinatorQuicClientCertificate, basePath);
            var caPath = Path.GetFullPath(config.CoordinatorQuicCaCertificate, basePath);
            if (!File.Exists(certificatePath) || !File.Exists(caPath))
                throw new FileNotFoundException("FLHook cluster event certificate or Coordinator CA file was not found.");
            var cursorPath = Path.GetFullPath(config.FlHookCompatEventCursorFile ??
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(config.DatabasePath, basePath))!, "flhook-events.cursor.json"), basePath);
            clusterEventPublisher = new CoordinatorFlHookEventPublisher(eventHub,
                new CoordinatorFlHookEventPublisher.Settings(coordinatorEndPoint!, config.CoordinatorQuicServerName,
                    config.ClusterAgentNodeId, eventHub.InstanceId, certificatePath,
                    Environment.GetEnvironmentVariable("LANCER_NEXUS_COORDINATOR_QUIC_CLIENT_CERT_PASSWORD"),
                    caPath, cursorPath));
            clusterEventPublisher.Start();
        }

        var pluginRoot = Path.GetFullPath(config.FlHookCompatDirectory, basePath);
        var trustRootPath = Path.GetFullPath(config.FlHookCompatTrustRootFile ?? Path.Combine(config.FlHookCompatDirectory, "trust-root.json"), basePath);
        var trustRoot = JsonSerializer.Deserialize<FlHookCompatTrustRoot>(await File.ReadAllTextAsync(trustRootPath), JsonOptions)
            ?? throw new InvalidDataException("FLHookCompat trust root is empty.");
        var assignedPlugins = new HashSet<string>(config.FlHookCompatPluginIds, StringComparer.Ordinal);
        var approvedCapabilities = new HashSet<string>(config.FlHookCompatApprovedCapabilities, StringComparer.Ordinal);
        dispatcher = Task.Run(DispatchAsync);

        foreach (var pluginId in config.FlHookCompatPluginIds)
        {
            if (!IsSafeDirectoryName(pluginId))
                throw new InvalidDataException("FLHookCompat plugin assignment contains an unsafe plugin ID.");
            var directory = Path.GetFullPath(pluginId, pluginRoot);
            EnsureUnderDirectory(pluginRoot, directory);
            var envelopePath = Path.Combine(directory, "manifest-envelope.json");
            var envelope = JsonSerializer.Deserialize<FlHookCompatSignedManifest>(
                await File.ReadAllTextAsync(envelopePath), JsonOptions)
                ?? throw new InvalidDataException($"FLHookCompat manifest for {pluginId} is empty.");
            var manifest = ReadDeclaredManifest(envelope);
            if (!string.Equals(manifest.PluginId, pluginId, StringComparison.Ordinal))
                throw new InvalidDataException($"FLHookCompat manifest identity does not match its assigned directory: {pluginId}.");

            var packagePath = Path.Combine(directory, "plugin.dll");
            var configurationPath = Path.Combine(directory, "configuration.json");
            var configuration = File.Exists(configurationPath)
                ? await File.ReadAllTextAsync(configurationPath)
                : "{}";
            if (System.Text.Encoding.UTF8.GetByteCount(configuration) > 64 * 1024)
                throw new InvalidDataException($"FLHookCompat configuration for {pluginId} exceeds 64 KiB.");

            await using var package = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var loaded = await ManagedPluginLoader.LoadAsync(envelope, trustRoot, package, HostApiVersion,
                RuntimeInformation.RuntimeIdentifier, approvedCapabilities, assignedPlugins, pluginModeEnabled: true,
                api: new PluginApi(eventHub), configurationJson: configuration);
            plugins.Add(loaded);
            FLLog.Info("FLHookCompat", $"Loaded assigned managed plugin {loaded.Manifest.PluginId} ({loaded.Manifest.PluginVersion}).");
        }
    }

    /// <summary>Copies only stable identifiers into a bounded queue; plugin callbacks never run on the simulation thread.</summary>
    public void TryPublish(ServerEvent serverEvent)
    {
        var pendingEvent = ConvertEvent(serverEvent);
        if (pendingEvent is null) return;
        if (!pending.Writer.TryWrite(pendingEvent) && Interlocked.Exchange(ref queueOverflowLogged, 1) == 0)
            FLLog.Warning("FLHookCompat", "Plugin event queue is full; some local plugin events may be dropped.");
    }

    private static PendingEvent? ConvertEvent(ServerEvent serverEvent)
    {
        Player? player;
        FlHookCompatServerEventKind kind;
        string? systemId = null;
        long? characterId = null;
        switch (serverEvent.Type)
        {
            case ServerEventType.PlayerConnected:
                player = serverEvent.GetPayload<PlayerConnectedEventPayload>().ConnectedPlayer;
                kind = FlHookCompatServerEventKind.PlayerConnected;
                break;
            case ServerEventType.PlayerDisconnected:
                player = serverEvent.GetPayload<PlayerDisconnectedEventPayload>().DisconnectedPlayer;
                kind = FlHookCompatServerEventKind.PlayerDisconnected;
                break;
            case ServerEventType.CharacterConnected:
                player = serverEvent.GetPayload<CharacterConnectedEventPayload>().ConnectedCharacter;
                kind = FlHookCompatServerEventKind.CharacterConnected;
                characterId = player.Character?.ID;
                break;
            case ServerEventType.CharacterDisconnected:
                player = serverEvent.GetPayload<CharacterDisconnectedEventPayload>().DisconnectedCharacter;
                kind = FlHookCompatServerEventKind.CharacterDisconnected;
                characterId = player.Character?.ID;
                break;
            case ServerEventType.PlayerSystemChanged:
                var changed = serverEvent.GetPayload<PlayerSystemChangedEventPayload>();
                player = changed.ChangedPlayer;
                kind = FlHookCompatServerEventKind.PlayerSystemChanged;
                systemId = changed.SystemId.ToLowerInvariant();
                break;
            default:
                return null;
        }

        if (player.AccountId == Guid.Empty || player.GatewaySessionId == Guid.Empty ||
            (kind is FlHookCompatServerEventKind.CharacterConnected or FlHookCompatServerEventKind.CharacterDisconnected &&
                characterId is not > 0))
            return null;
        return new PendingEvent(kind, player.AccountId.ToString("N"), systemId, characterId);
    }

    private async Task DispatchAsync()
    {
        await foreach (var item in pending.Reader.ReadAllAsync())
        {
            var failures = eventHub.Publish(item.Kind, item.PlayerId, item.SystemId, item.CharacterId);
            foreach (var failure in failures)
                FLLog.Error("FLHookCompat", $"Plugin event handler failed: {failure.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        pending.Writer.TryComplete();
        if (dispatcher is not null)
        {
            try { await dispatcher; }
            catch (Exception exception) { FLLog.Error("FLHookCompat", $"Event dispatcher stopped: {exception.Message}"); }
            dispatcher = null;
        }
        if (clusterEventPublisher is not null)
        {
            try
            {
                if (OperatingSystem.IsLinux())
                    await clusterEventPublisher.DisposeAsync();
            }
            catch (Exception exception) { FLLog.Error("FLHookCompat", $"Coordinator event publisher stopped: {exception.Message}"); }
            clusterEventPublisher = null;
        }
        for (var i = plugins.Count - 1; i >= 0; i--)
        {
            try { await plugins[i].DisposeAsync(); }
            catch (Exception exception) { FLLog.Error("FLHookCompat", $"Plugin shutdown failed: {exception.Message}"); }
        }
        plugins.Clear();
        eventHub.Dispose();
    }

    private static FlHookCompatManifest ReadDeclaredManifest(FlHookCompatSignedManifest envelope)
    {
        try
        {
            var payload = Convert.FromBase64String(envelope.Payload);
            return JsonSerializer.Deserialize<FlHookCompatManifest>(payload, JsonOptions)
                ?? throw new InvalidDataException("FLHookCompat manifest payload is empty.");
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("FLHookCompat manifest payload is not base64.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("FLHookCompat manifest payload is invalid JSON.", exception);
        }
    }

    private static bool IsSafeDirectoryName(string value) => value is { Length: > 0 and <= 128 } &&
        value is not "." and not ".." && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static void EnsureUnderDirectory(string root, string path)
    {
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("FLHookCompat plugin path escaped its configured directory.");
    }

    private sealed record PendingEvent(FlHookCompatServerEventKind Kind, string PlayerId, string? SystemId, long? CharacterId);

    private sealed class PluginApi(FlHookCompatServerEventHub events) : IFlHookCompatApi
    {
        public IPluginLogger Logger { get; } = new PluginLogger();
        public JsonDocument EmptyConfiguration { get; } = JsonDocument.Parse("{}");
        public JsonElement Configuration => EmptyConfiguration.RootElement;
        public IFlHookCompatServerEvents? ServerEvents { get; } = events;
    }

    private sealed class PluginLogger : IPluginLogger
    {
        public void Information(string eventName, string message) => FLLog.Info("FLHookCompat/" + eventName, message);
        public void Warning(string eventName, string message) => FLLog.Warning("FLHookCompat/" + eventName, message);
        public void Error(string eventName, string message) => FLLog.Error("FLHookCompat/" + eventName, message);
    }
}
#endif
