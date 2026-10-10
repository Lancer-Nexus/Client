// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using System.Numerics;
using LibreLancer.Data.GameData.World;
using LibreLancer.Data.Schema.Universe;
using LibreLancer.Server;
using LibreLancer.Server.Ai;
using LibreLancer.Server.Components;
using LibreLancer.World;
using LibreLancer.World.Components;
using LibreLancer.Net;
using LibreLancer.Net.Protocol;
using LibreLancer;
using LibreLancer.Data;
using LibreLancer.Options;

namespace LLServer
{
    internal class MainClass
	{
        public static async Task<int> Main(string[] args)
        {
                   
            bool printHelp = false;
            bool printVersion = false;
            string? configPath = null;
            var os = new OptionSet
            {
                { "h|?|help", "shows this message and exits", x => printHelp = x != null },
                { "v|version", "shows the version and exits", x => printVersion = x != null },
                { "c|config=", "set path for the configuration file", x => configPath = x }
            };

            var extra = os.Parse(args);
            if (printHelp)
            {
                Console.WriteLine($"LLServer {Platform.GetInformationalVersion<MainClass>()}");
                os.WriteOptionDescriptions(Console.Out);
                Console.WriteLine("Run with makeconfig to generate a config file.");
                return 0;
            }

            if (printVersion)
            {
                Console.WriteLine(Platform.GetInformationalVersion<MainClass>());
                return 0;
            }

            AppHandler.ConsoleInit();
            configPath ??= Path.Combine(Platform.GetBasePath(), "llserver.json");
            if (extra.Count > 0 && extra[0] == "makeconfig")
            {
                MakeConfig(configPath);
				return 0;
			}

            if (!File.Exists(configPath))
            {
                await Console.Error.WriteLineAsync($"Can't find {configPath}. Use the --config option to specify a file or run LLServer makeconfig");
                return 2;
            }

			var config = JSON.Deserialize<ServerConfig>(await File.ReadAllTextAsync(configPath));
            config.DatabasePath = Path.GetFullPath(config.DatabasePath, Platform.GetBasePath());

            var app = new ServerApp(config);
            if (!app.StartServer())
            {
                await Console.Error.WriteLineAsync("Server failed to start");
                return 1;
            }
            
            using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
            {
                Console.WriteLine("Server shutting down");
                app.StopServer();
                Environment.Exit(0);
            });

            var running = true;
			while (running)
            {
                var input = Console.ReadLine();
                if (input == null)
                {
                    app.WaitExit();
                    break;
                }
                var (cmd, cmdargs) = GetCommand(input);
                switch (cmd.ToLowerInvariant())
				{
					case "stop":
					case "quit":
					case "exit":
						running = false;
						break;
#if DEBUG
                    case "npc-state":
                    {
                        Guid? npcId = null;
                        if (!string.IsNullOrWhiteSpace(cmdargs))
                        {
                            if (!Guid.TryParse(cmdargs, out var parsed) || parsed == Guid.Empty)
                            {
                                Console.WriteLine("Usage: npc-state [NPC UUID]");
                                break;
                            }
                            npcId = parsed;
                        }
                        try
                        {
                            var state = await app.Server!.CaptureNpcDiagnosticsAsync(npcId)
                                .WaitAsync(TimeSpan.FromSeconds(5));
                            Console.WriteLine("NPC_STATE " + state);
                        }
                        catch (Exception exception) { Console.WriteLine("NPC state unavailable: " + exception.Message); }
                        break;
                    }
                    case "npc-load-world":
                    {
                        if (string.IsNullOrWhiteSpace(cmdargs))
                        {
                            Console.WriteLine("Usage: npc-load-world <system nickname>");
                            break;
                        }
                        var system = app.Server!.GameData.Items.Systems.Get(cmdargs);
                        if (system == null)
                        {
                            Console.WriteLine($"Unknown system '{cmdargs}'.");
                            break;
                        }
                        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        app.Server.Worlds.RequestWorld(system, _ => ready.TrySetResult(true), []);
                        try
                        {
                            await ready.Task.WaitAsync(TimeSpan.FromSeconds(60));
                            Console.WriteLine($"NPC_WORLD_READY {system.Nickname}");
                        }
                        catch (TimeoutException)
                        {
                            Console.WriteLine($"NPC_WORLD_TIMEOUT {system.Nickname}");
                        }
                        break;
                    }
                    case "npc-population-smoke":
                    {
                        if (string.IsNullOrWhiteSpace(cmdargs))
                        {
                            Console.WriteLine("Usage: npc-population-smoke <system nickname>");
                            break;
                        }
                        var system = app.Server!.GameData.Items.Systems.Get(cmdargs);
                        if (system == null)
                        {
                            Console.WriteLine($"Unknown system '{cmdargs}'.");
                            break;
                        }
                        var parameters = system.EncounterParameters.Select(x => x.Nickname)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var tradeZone = system.Zones
                            .Where(zone => zone.Density > 0 && zone.Encounters is { Length: > 0 } &&
                                zone.Encounters.Any(encounter => parameters.Contains(encounter.Archetype) &&
                                    (encounter.Archetype.Equals("area_trade_transport", StringComparison.OrdinalIgnoreCase) ||
                                     encounter.Archetype.Equals("area_trade_freighter", StringComparison.OrdinalIgnoreCase))))
                            .OrderByDescending(zone => zone.Density)
                            .FirstOrDefault();
                        if (tradeZone?.Encounters == null)
                        {
                            Console.WriteLine($"No populated area trade zone found in {system.Nickname}.");
                            break;
                        }
                        var tradeEncounter = tradeZone.Encounters.First(x =>
                            x.Archetype.Equals("area_trade_transport", StringComparison.OrdinalIgnoreCase) ||
                            x.Archetype.Equals("area_trade_freighter", StringComparison.OrdinalIgnoreCase));
                        var savedEncounters = tradeZone.Encounters;
                        var forcedTradeEncounter = new Encounter
                        {
                            Archetype = tradeEncounter.Archetype,
                            Difficulty = tradeEncounter.Difficulty,
                            Chance = 1,
                            FactionSpawns = tradeEncounter.FactionSpawns
                        };
                        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                        app.Server.Worlds.RequestWorld(system, world => world.EnqueueAction(() =>
                        {
                            var fakePlayer = new Player(new PopulationSmokePacketClient(), app.Server, Guid.NewGuid())
                            {
                                System = system.Nickname,
                                Character = new NetCharacter()
                            };
                            var playerObject = new GameObject
                            {
                                Nickname = "npc_population_smoke_player",
                                Flags = GameObjectFlags.Exists | GameObjectFlags.Player
                            };
                            playerObject.SetLocalTransform(new Transform3D(tradeZone.Position, Quaternion.Identity));
                            playerObject.AddComponent(new SPlayerComponent(fakePlayer, playerObject));
                            var originalIds = world.GameWorld.Objects
                                .Where(obj => obj.TryGetComponent<SNPCComponent>(out _))
                                .Select(obj => obj.GetComponent<SNPCComponent>()!.NpcId)
                                .ToHashSet();
                            try
                            {
                                world.GameWorld.AddObject(playerObject);
                                world.Players.Add(fakePlayer, playerObject);
                                tradeZone.Encounters = [forcedTradeEncounter];
                                world.Population.PopulateInitialAroundPlayer(playerObject);
                                var spawned = world.GameWorld.Objects
                                    .Where(obj => obj.TryGetComponent<SNPCComponent>(out var npc) &&
                                                  !originalIds.Contains(npc.NpcId))
                                    .ToArray();
                                var cargoShips = spawned.Where(obj =>
                                    obj.TryGetComponent<SNPCCargoComponent>(out var cargo) &&
                                    cargo.Cargo.Any(item => item.Item is LibreLancer.Data.GameData.Items.CommodityEquipment))
                                    .ToArray();
                                if (cargoShips.Length == 0)
                                    throw new InvalidOperationException("Trade encounter spawned no commodity cargo carrier.");
                                foreach (var cargoShip in cargoShips)
                                    cargoShip.GetComponent<SHealthComponent>()?.OnProjectileHit(playerObject);
                                var attackers = spawned.Count(obj =>
                                    obj.TryGetComponent<SNPCComponent>(out var npc) &&
                                    npc.CurrentDirective is AiAttackState attack &&
                                    ReferenceEquals(attack.Target, playerObject));
                                if (attackers == 0)
                                    throw new InvalidOperationException("Cargo attack did not direct any spawned trader ships against the attacker.");
                                var fleeing = spawned.Count(obj =>
                                    obj.TryGetComponent<SNPCComponent>(out var npc) && npc.PopulationFleeing);
                                var result = string.Join("; ", cargoShips.Select(obj =>
                                {
                                    var cargo = obj.GetComponent<SNPCCargoComponent>()!.Cargo
                                        .Where(item => item.Item is LibreLancer.Data.GameData.Items.CommodityEquipment)
                                        .Select(item => $"{item.Item.Nickname}x{item.Count}");
                                    return $"{obj.GetComponent<ShipComponent>()?.Ship.Nickname}:{string.Join(",", cargo)}";
                                }));
                                ready.TrySetResult($"NPC_POPULATION_READY system={system.Nickname} zone={tradeZone.Nickname} " +
                                                   $"encounter={tradeEncounter.Archetype} spawned={spawned.Length} responding={attackers} " +
                                                   $"fleeing={fleeing} cargo={result}");
                            }
                            catch (Exception exception)
                            {
                                ready.TrySetException(exception);
                            }
                            finally
                            {
                                tradeZone.Encounters = savedEncounters;
                                world.Players.Remove(fakePlayer);
                                world.GameWorld.RemoveObject(playerObject);
                            }
                        }), []);
                        try
                        {
                            Console.WriteLine(await ready.Task.WaitAsync(TimeSpan.FromSeconds(60)));
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine($"NPC population smoke failed: {exception}");
                        }
                        break;
                    }
#endif
                    case "ban":
                    {
                        Guid? g = null;
                        if (cmdargs.Length > 0)
                        {
                            if (Guid.TryParse(cmdargs, out var v))
                                g = v;
                            else
                                g = await app.Server?.Database.FindAccount(cmdargs)!;
                        }
                        if (g.HasValue)
                        {
                            FLLog.Info("Server", $"Banning account {g} for 30 days");
                            await app.Server?.Database.BanAccount(g.Value, DateTime.UtcNow.AddDays(30))!;
                        }
                        break;
                    }
                    case "unban":
                    {
                        Guid? g = null;
                        if (cmdargs.Length > 0)
                        {
                            if (Guid.TryParse(cmdargs, out var v))
                                g = v;
                            else
                                g = await app.Server?.Database.FindAccount(cmdargs)!;
                        }
                        if (g.HasValue)
                        {
                            FLLog.Info("Server", $"Unbanning account {g}");
                            await app.Server?.Database.UnbanAccount(g.Value)!;
                        }
                        break;
                    }
                    case "op":
                    case "deop":
                    {
                        if (!Guid.TryParse(cmdargs, out var accountId))
                        {
                            Console.WriteLine("Use op <account-uuid> or deop <account-uuid>.");
                            break;
                        }
                        var enabled = cmd.Equals("op", StringComparison.OrdinalIgnoreCase);
                        if (app.Server?.SetLocalOperator(accountId, enabled) == true)
                            Console.WriteLine($"Local operator {(enabled ? "enabled" : "removed")} for {accountId:D}.");
                        else
                            Console.WriteLine("Local OP is available only without Gateway. In Nexus, use /admin op or /admin deop <account-uuid> in-game with permissions.manage.");
                        break;
                    }
                    case "admin":
                    {
                        Console.WriteLine("Character admin flags are disabled. Use op/deop <account-uuid> on an isolated server.");
                        break;
                    }
                    case "deadmin":
                    {
                        Console.WriteLine("Character admin flags are disabled. Use op/deop <account-uuid> on an isolated server.");
                        break;
                    }
                }
            }

            Console.WriteLine("Server shutting down");
			app.StopServer();
			return 0;
		}

        private static (string cmd, string args) GetCommand(string commandString)
        {
            var firstSpace = commandString.IndexOf(' ');
            string cmd;
            string args;
            if (firstSpace == -1) {
                cmd = commandString;
                args = "";
            }
            else
            {
                cmd = commandString.Substring(0, firstSpace).Trim();
                args = commandString.Substring(firstSpace).Trim();
            }
            return (cmd, args);
        }

        private static void MakeConfig(string configPath)
		{
			ServerConfig config = new ServerConfig();

			Console.Write("Freelancer Path: ");
			config.FreelancerPath = (Console.ReadLine() ?? "").Trim();

			Console.Write("Db Path: ");
			config.DatabasePath = (Console.ReadLine() ?? "").Trim();

			Console.Write("Server Name: ");
			config.ServerName = (Console.ReadLine() ?? "").Trim();

			Console.Write("Server Description: ");
			config.ServerDescription = (Console.ReadLine() ?? "").Trim();

			File.WriteAllText(configPath, JSON.Serialize(config));
		}

#if DEBUG
        private sealed class PopulationSmokePacketClient : IPacketClient
        {
            public int MaxSequencedSize => 1200;
            public void SendPacket(IPacket packet, PacketDeliveryMethod method) { }
            public void Disconnect(DisconnectReason reason) { }
        }
#endif
	}
}
