using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using LibreLancer.Data.GameData;
using LibreLancer.Data.GameData.World;

namespace LibreLancer.Server;

public class WorldProvider
{
    private GameServer server;

    public WorldProvider(GameServer server)
    {
        this.server = server;
    }

    public void RemoveWorld(StarSystem system)
    {
        worlds.TryRemove(system, out _);
    }

    private struct WorldState
    {
        public bool Ready;
        public ServerWorld World;
    }

    private ConcurrentDictionary<StarSystem, WorldState> worlds = new();

    private async Task LoadWorldAsync(StarSystem system, PreloadObject[] preloads)
    {
        if (!worlds.TryAdd(system, new WorldState())) return;
        try
        {
            var world = new ServerWorld(system, server);
            server.GameData.PreloadObjects(preloads, server.Resources);
            await server.RestoreNpcCheckpointsForWorldAsync(world).ConfigureAwait(false);
            server.WorldReady(world);
            worlds[system] = new WorldState { Ready = true, World = world };
        }
        catch
        {
            worlds.TryRemove(system, out _);
            throw;
        }
    }
    public void RequestWorld(StarSystem system, Action<ServerWorld> spunUp, PreloadObject[] preloads)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                if (worlds.TryGetValue(system, out var ws) && ws.Ready)
                {
                    spunUp(ws.World);
                    return;
                }
                try { await LoadWorldAsync(system, preloads); }
                catch (HttpRequestException) { await Task.Delay(TimeSpan.FromSeconds(2)); }
                catch (TaskCanceledException) { await Task.Delay(TimeSpan.FromSeconds(2)); }
                await Task.Delay(33);
            }
        }).ContinueWith(x =>
        {
            if (x.Exception != null)
                throw x.Exception;
        });
    }
}
