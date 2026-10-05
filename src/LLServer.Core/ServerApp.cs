using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LibreLancer;
using LibreLancer.Data;
using LibreLancer.Data.IO;
using LibreLancer.Net;
using LibreLancer.Server;
using Microsoft.EntityFrameworkCore;

namespace LLServer;

public class ServerApp(ServerConfig config)
{
    public GameServer? Server;
    private readonly ServerConfig Config = config;
    private CancellationTokenSource? statusCancellation;
    private Task? statusWriter;

    public bool StartServer()
    {
        if (!string.IsNullOrWhiteSpace(Config.LoginUrl) &&
            !Config.LoginUrl.StartsWith("https://"))
        {
            FLLog.Error("Config", "Only HTTPS login servers are supported");
            return false;
        }
        if (!GameConfig.CheckFLDirectory(Config.FreelancerPath))
        {
            FLLog.Error("Config", $"'{Config.FreelancerPath ?? "NULL"}' is not a valid game folder");
            return false;
        }
        if (!string.IsNullOrWhiteSpace(Config.RuntimeStatusFile) &&
            (string.IsNullOrWhiteSpace(Config.InstanceId) || string.IsNullOrWhiteSpace(Config.SystemId) ||
             string.IsNullOrWhiteSpace(Config.InstanceEndpoint)))
            throw new InvalidOperationException("Runtime status requires InstanceId, SystemId and InstanceEndpoint.");
        var ctxFactory = new SqlDesignTimeFactory(Config.DatabasePath);
        using (var ctx = ctxFactory.CreateDbContext([]))
        {
            if (ctx.Database.GetPendingMigrations().Any())
            {
                FLLog.Info("Server", "Migrating database");
                ctx.Database.Migrate();
            }
        }

        Server = new GameServer(FileSystem.FromPath(Config.FreelancerPath))
        {
            DbContextFactory = ctxFactory,
            ServerName = Config.ServerName,
            ServerDescription = Config.ServerDescription,
            ScriptsFolder = Path.Combine(GetBasePath(), "scripts"),
            LoginUrl = Config.LoginUrl,
            Listener =
            {
                Port = Config.Port > 0 ? Config.Port : LNetConst.DEFAULT_PORT,
                MaxConnections = Config.MaxPlayers > 0 ? Config.MaxPlayers : 200
            }
        };
        if(Config.ThreadCount > 0)
            Server.ThreadCount = Config.ThreadCount;
        Server.Start();
        if (!string.IsNullOrWhiteSpace(Config.RuntimeStatusFile))
        {
            var statusPath = Path.GetFullPath(Config.RuntimeStatusFile, Platform.GetBasePath());
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
            File.Delete(statusPath);
            statusCancellation = new CancellationTokenSource();
            statusWriter = WriteRuntimeStatusAsync(statusCancellation.Token);
        }
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
        statusCancellation?.Cancel();
        Server?.Stop();
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
                IsReady = network?.IsRunning == true,
                CurrentPlayers = network?.ConnectedPeersCount ?? 0,
                MaxPlayers = listener?.MaxConnections ?? 0,
                Endpoint = Config.InstanceEndpoint!
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
}

public sealed class InstanceRuntimeStatus
{
    public DateTimeOffset WrittenAtUtc { get; init; }
    public string InstanceId { get; init; } = "";
    public string SystemId { get; init; } = "";
    public bool IsReady { get; init; }
    public int CurrentPlayers { get; init; }
    public int MaxPlayers { get; init; }
    public string Endpoint { get; init; } = "";
}
