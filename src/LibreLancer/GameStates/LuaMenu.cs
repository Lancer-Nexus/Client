// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LibreLancer.Client;
using LibreLancer.Data.GameData;
using LibreLancer.ImUI.NodeEditor;
using LibreLancer.Input;
using LibreLancer.Interface;
using LibreLancer.Items;
using LibreLancer.Net;
using LibreLancer.Net.Protocol;
using LibreLancer.Render;
using LibreLancer.Resources;
using LibreLancer.Thn;
using LiteNetLib;
using WattleScript.Interpreter;
using DisconnectReason = LibreLancer.Net.DisconnectReason;

namespace LibreLancer
{
    public class LuaMenu : GameState
    {
        private UiContext ui;
        private readonly Cursor cur;
        private Cutscene? scene;
        private MenuAPI api;
        private KeyCaptureContext keyCapture = null!;

        private IntroScene intro;

        public LuaMenu(FreelancerGame g) : base(g)
        {
            api = new MenuAPI(this);
            ui = Game.Ui;
            ui.GameApi = api;
            ui.TextScale = 0.5f;
            ui.Visible = true;
            ui.OpenScene("mainmenu", 0.4);
            g.GameData.PopulateCursors();
            g.CursorKind = CursorKind.None;
            intro = g.GameData.GetIntroScene();
            TryRunScript(intro.Scripts);
            FLLog.Info("Thn", "Playing " + intro.ThnName);
            cur = g.ResourceManager.GetCursor("arrow")!;
            GC.Collect(); // crap
            g.Sound.PlayMusic(intro.Music!, 0);
            g.Keyboard.KeyDown += UiKeyDown;
            g.Keyboard.TextInput += UiTextInput;
#if DEBUG
            g.Keyboard.KeyDown += Keyboard_KeyDown;
#endif
            g.Keyboard.KeyUp += Keyboard_OnKeyUp;
            g.Mouse.MouseUp += Mouse_MouseUp;
            Game.Saves.Selected = -1;

            if (g.LoadTimer != null)
            {
                g.LoadTimer.Stop();
                FLLog.Info("Game", $"Initial load took {g.LoadTimer.Elapsed.TotalSeconds} seconds");
                g.LoadTimer = null;
            }

            // Set low latency GC mode only once everything has been loaded in
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            FadeIn(0.1, 0.3);
            if (!string.IsNullOrWhiteSpace(g.Config.ClusterGatewayUrl))
                ui.OpenScene("serverlist");
        }

        private void TryRunScript(List<ResolvedThn> thnScripts)
        {
            var intro = new List<ThnScript>();
            scene = new Cutscene(new ThnScriptContext(null), Game.GameData, Game.ResourceManager, Game.Sound,
                Game.RenderContext.CurrentViewport, Game);

            foreach (var s in thnScripts)
            {
#if !DEBUG
                try
                {
                    intro.Add(s.LoadScript());
                }
                catch (Exception e)
                {
                    FLLog.Error("Thn", $"Error loading script {s.SourcePath}: {e.Message}\n{e.StackTrace}");
                    scene = null;
                    return;
                }
#else
                intro.Add(s.LoadScript());
#endif
            }

            scene.BeginScene(intro);
        }

        public override void OnSettingsChanged()
        {
            scene?.Renderer?.Settings = Game.Config.Settings;
        }

        private void Mouse_MouseUp(MouseEventArgs e)
        {
            if (e.Buttons != MouseButtons.Left && KeyCaptureContext.Capturing(keyCapture))
            {
                keyCapture.Set(UserInput.FromMouse(e.Buttons));
            }
        }

        private void Keyboard_OnKeyUp(KeyEventArgs e)
        {
            if (!KeyCaptureContext.Capturing(keyCapture))
            {
                return;
            }

            if (e.Key == Keys.Escape || e.Key == Keys.F1)
            {
                keyCapture.Cancel();
            }
            else if (IsModifierKey(e.Key))
            {
                keyCapture.Set(UserInput.FromKey(e.Modifiers, e.Key));
            }
        }

        private void UiTextInput(string text)
        {
            if (!KeyCaptureContext.Capturing(keyCapture))
                ui.OnTextEntry(text);
        }

        private void UiKeyDown(KeyEventArgs e)
        {
            if (KeyCaptureContext.Capturing(keyCapture))
            {
                if (e.Key == Keys.Escape || e.Key == Keys.F1)
                    keyCapture.Cancel();
                else if (!IsModifierKey(e.Key))
                    keyCapture.Set(UserInput.FromKey(e.Modifiers, e.Key));
                return;
            }

            if (e.Key == Keys.Escape)
            {
                ui.OnEscapePressed();
            }

            ui.OnKeyDown(e.Key, (e.Modifiers & KeyModifiers.Control) != 0);
        }

        private static bool IsModifierKey(Keys key) => key is
            Keys.LeftShift or Keys.RightShift or
            Keys.LeftControl or Keys.RightControl or
            Keys.LeftAlt or Keys.RightAlt;

        [WattleScriptUserData]
        public class ServerList : ITableData
        {
            public List<LocalServerInfo> Servers = [];
            public int Count => Servers.Count;
            public int Selected { get; set; } = -1;

            public string? GetContentString(int row, string column)
            {
                if (row < 0 || row > Count || string.IsNullOrEmpty(column)) return null;

                switch (column.ToLowerInvariant())
                {
                    case "name":
                        return Servers[row].Name;
                    case "ip":
                        var addr = Servers[row].EndPoint.Address;
                        if (addr.IsIPv4MappedToIPv6)
                            return addr.MapToIPv4().ToString();
                        return addr.ToString();
                    case "visit":
                        return "NO";
                    case "ping":
                        return Servers[row].Ping.ToString();
                    case "players":
                        return $"{Servers[row].CurrentPlayers}/{Servers[row].MaxPlayers}";
                    case "version":
                        return Servers[row].DataVersion;
                    case "lan":
                        return "YES";
                    default:
                        return null;
                }
            }

            public string CurrentDescription()
            {
                if (Selected < 0 || Selected >= Count) return "";
                return Servers[Selected].Description;
            }

            public bool ValidSelection()
            {
                return (Selected >= 0 && Selected < Count);
            }

            public void Reset()
            {
                Selected = -1;
                Servers = [];
            }
        }

        [WattleScript.Interpreter.WattleScriptUserData]
        public class MenuAPI : UiApi
        {
            private LuaMenu state;

            public MenuAPI(LuaMenu m)
            {
                state = m;
            }

            public KeyMapTable GetKeyMap()
            {
                var table = new KeyMapTable(state.Game.InputMap, state.Game.GameData.Items.Ini.Infocards);
                table.OnCaptureInput += (k) => { state.keyCapture = k; };
                return table;
            }

            public GameSettings GetCurrentSettings() => state.Game.Config.Settings.MakeCopy();

            public void ApplySettings(GameSettings settings)
            {
                state.Game.Config.Settings.Apply(settings);
                state.Game.Config.Save();
            }

            public SaveGameFolder SaveGames() => state.Game.Saves;
            public bool GatewayEnabled() => !string.IsNullOrWhiteSpace(state.Game.Config.ClusterGatewayUrl);

            public bool HasDebugLoginCredentials()
            {
#if DEBUG
                return GatewayEnabled() && !string.IsNullOrWhiteSpace(state.Game.DebugLoginEmail) &&
                       !string.IsNullOrEmpty(state.Game.DebugLoginPassword);
#else
                return false;
#endif
            }

            public void LoginWithDebugCredentials()
            {
#if DEBUG
                if (HasDebugLoginCredentials())
                    Login(state.Game.DebugLoginEmail!, state.Game.DebugLoginPassword!);
#endif
            }

            public bool SelectDebugCharacter()
            {
#if DEBUG
                var requestedName = state.Game.DebugCharacterName;
                if (string.IsNullOrWhiteSpace(requestedName))
                    return false;
                var index = cselInfo.Characters.FindIndex(character =>
                    string.Equals(character.Name, requestedName, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    FLLog.Warning("Debug", $"Requested character '{requestedName}' was not found; opening character selection.");
                    state.Game.DebugCharacterName = null;
                    return false;
                }
                cselInfo.Selected = index;
                FLLog.Info("Debug", $"Automatically selecting requested character '{cselInfo.Characters[index].Name}'.");
                state.Game.DebugCharacterName = null;
                return true;
#else
                return false;
#endif
            }

            public void DeleteSelectedGame() => state.Game.Saves.TryDelete(state.Game.Saves.Selected);

            public void LoadSelectedGame()
            {
                if (GatewayEnabled()) return;
                state.FadeOut(0.2, () =>
                {
                    var embeddedServer = new EmbeddedServer(state.Game.GameData, state.Game.ResourceManager,
                        state.Game.GetSaveFolder());
                    var session = new CGameSession(state.Game, embeddedServer);
                    embeddedServer.StartFromSave(state.Game.Saves.SelectedFile!,
                        File.ReadAllBytes(state.Game.Saves.SelectedFile!));
                    state.Game.ChangeState(new NetWaitState(session, state.Game));
                });
            }

            public override void NewGame()
            {
                if (GatewayEnabled()) return;
                state.FadeOut(0.2, () =>
                {
                    var embeddedServer = new EmbeddedServer(state.Game.GameData, state.Game.ResourceManager,
                        state.Game.GetSaveFolder());
                    var session = new CGameSession(state.Game, embeddedServer);
                    var newPlayerPath = state.Game.GameData.Items.Ini.Freelancer.NewPlayerPath;
                    embeddedServer.StartFromSave(newPlayerPath,
                        state.Game.GameData.VFS.ReadAllBytes(newPlayerPath));
                    state.Game.ChangeState(new NetWaitState(session, state.Game));
                });
            }

            private UiNewCharacter[] newCharacters = null!;

            public UiNewCharacter[] GetNewCharacters() => newCharacters;

            private void ResolveNicknames(SelectableCharacter c)
            {
                c.Ship = state.Game.GameData.GetString(state.Game.GameData.Items.Ships.Get(c.Ship)!.IdsName);
                c.Location = state.Game.GameData.GetString(state.Game.GameData.Items.Systems.Get(c.Location)!.IdsName);
            }

            internal void _Update()
            {
                if (netClient == null)
                {
                    return;
                }

                while (netClient.PollPacket(out var pkt))
                {
                    switch (pkt)
                    {
                        case OpenCharacterListPacket oclist:
                            FLLog.Info("Net", "Opening Character List");
                            this.cselInfo = oclist.Info;
                            foreach (var sc in oclist.Info.Characters)
                                ResolveNicknames(sc);
                            state.ui.Event("CharacterList");
                            break;
                        case AddCharacterPacket ac:
                            ResolveNicknames(ac.Character);
                            cselInfo.Characters.Add(ac.Character);
                            break;
                        case NewCharacterDBPacket ncdb:
                        {
                            newCharacters = new UiNewCharacter[ncdb.Factions.Count];

                            for (int i = 0; i < ncdb.Factions.Count; i++)
                            {
                                var package = ncdb.Packages.First(x =>
                                    x.Nickname.Equals(ncdb.Factions[i].Package, StringComparison.OrdinalIgnoreCase));
                                var ship = state.Game.GameData.Items.Ships.Get(package.Ship)!;
                                ship.ModelFile!.LoadFile(state.Game.ResourceManager);
                                var loc = state.Game.GameData.GetString(
                                    state.Game.GameData.Items.Bases.Get(ncdb.Factions[i].Base)!.IdsName);
                                newCharacters[i] = new UiNewCharacter()
                                {
                                    Money = package.Money,
                                    StridDesc = package.StridDesc,
                                    StridName = package.StridName,
                                    ShipName = state.Game.GameData.GetString(ship.IdsName),
                                    ShipModel = ship.ModelFile!.ModelFile!,
                                    Location = loc
                                };
                            }

                            state.ui.Event("OpenNewCharacter");
                            break;
                        }
                        default:
                            netSession.HandlePacket(pkt);
                            break;
                    }

                }
            }

            private GameNetClient? netClient;
            private CancellationTokenSource? gatewayLoginCancellation;
            private CGameSession netSession = null!;
            private ServerList serverList = new();
            private CharacterSelectInfo cselInfo = null!;

            public CharacterSelectInfo CharacterList() => cselInfo;
            public ServerList ServerList() => serverList;

            public void StartNetworking()
            {
                StopNetworking();
                netClient = new GameNetClient(state.Game);
                netSession = new CGameSession(state.Game, netClient);
                netClient.UUID = state.Game.Config.UUID;
                netClient.ServerFound += info => serverList.Servers.Add(info);
                netClient.Disconnected += NetClientOnDisconnected;
                netClient.AuthenticationRequired += NetClientOnAuthenticationRequired;
                netClient.Start();
                if (!GatewayEnabled())
                    RefreshServers();
            }

            private void NetClientOnAuthenticationRequired(bool retry)
            {
                if (retry) state.ui.Event("IncorrectPassword");
                else state.ui.Event("Login");
            }

            public void Login(string username, string password)
            {
                if (!GatewayEnabled())
                    netClient?.Login(username, password);
                else
                    _ = LoginToGatewayAsync(username, password);
            }

            private async Task LoginToGatewayAsync(string email, string password)
            {
                gatewayLoginCancellation?.Cancel();
                gatewayLoginCancellation?.Dispose();
                gatewayLoginCancellation = new CancellationTokenSource();
                var cancellation = gatewayLoginCancellation.Token;
                var client = netClient;
                if (client is null)
                    return;
                try
                {
                    using var gateway = new NexusGatewayLogin(
                        new Uri(state.Game.Config.ClusterGatewayUrl, UriKind.Absolute));
                    var result = await gateway.LoginAndPlaceAsync(email, password,
                        state.Game.Config.ClusterTargetSystem, state.Game.Config.ClusterRegion,
                        cancellation);
                    state.Game.QueueUIThread(() =>
                    {
                        if (netClient != client || cancellation.IsCancellationRequested)
                            return;
                        if (result.RequestedExitCode is not null)
                        {
                            state.ui.Event("UpdateRequired", result.VersionDecision.MessageKey);
                        }
                        else if (result.Assigned)
                        {
                            client.ClusterGatewayUrl = state.Game.Config.ClusterGatewayUrl;
                            client.ClusterAccessToken = result.AccessToken ?? "";
                            client.ClusterRefreshToken = result.RefreshToken ?? "";
                            client.ClusterSessionId = result.SessionId ?? Guid.Empty;
                            client.ClusterInstanceId = result.InstanceId ?? "";
                            client.ClusterSystemId = result.SystemId ?? "";
                            client.ClusterEndpoint = result.GameEndpoint ?? "";
                            client.ConnectWithTicket(result.GameEndpoint!, result.JoinTicket!);
                        }
                        else
                            state.ui.Event("Disconnect", "AssignmentFailed");
                    });
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
                catch (UnauthorizedAccessException)
                {
                    state.Game.QueueUIThread(() =>
                    {
                        if (netClient == client)
                            state.ui.Event("IncorrectPassword");
                    });
                }
                catch (ClientVersionMetadataException)
                {
                    state.Game.QueueUIThread(() =>
                    {
                        if (netClient == client)
                            state.ui.Event("RepairRequired");
                    });
                }
                catch (Exception exception) when (exception is HttpRequestException or
                    InvalidDataException or ArgumentException or FormatException)
                {
                    FLLog.Error("Gateway", exception.Message);
                    state.Game.QueueUIThread(() =>
                    {
                        if (netClient == client)
                            state.ui.Event("Disconnect", "ConnectionError");
                    });
                }
            }

            public void RequestNewCharacter()
            {
                netSession.RpcServer.RequestCharacterDB();
            }

            public void LoadCharacter()
            {
                netSession.RpcServer.SelectCharacter(cselInfo.Selected).ContinueWith(x => state.Game.QueueUIThread(() =>
                {
                    if (x.Result)
                    {
                        state.FadeOut(0.2, () =>
                        {
                            netClient!.Disconnected -= NetClientOnDisconnected;
                            netClient = null;
                            state.Game.ChangeState(new NetWaitState(netSession, state.Game));
                        });
                    }
                    else
                    {
                        state.ui.Event("SelectCharFailure");
                    }
                }));

            }

            private int delIndex = -1;

            public void DeleteCharacter()
            {
                delIndex = cselInfo.Selected;
                netSession.RpcServer.DeleteCharacter(cselInfo.Selected).ContinueWith((t) =>
                {
                    if (t.Result)
                    {
                        state.Game.QueueUIThread(() =>
                        {
                            cselInfo.Characters.RemoveAt(delIndex);
                            delIndex = -1;
                        });
                    }
                });
            }

            private void NetClientOnDisconnected(DisconnectReason reason)
            {
                netClient?.Shutdown();
                netClient = null;
                state.ui.Event("Disconnect", reason.ToString());
            }

            public void RefreshServers()
            {
                if (GatewayEnabled()) return;
                serverList.Reset();
                netClient!.DiscoverLocalPeers();
            }

            public void ConnectSelection()
            {
                if (GatewayEnabled()) return;
                if (serverList.Selected != -1)
                {
                    netClient!.Connect(serverList.Servers[serverList.Selected].EndPoint);
                }
            }

            public void ConnectAddress(string address)
            {
                if (!GatewayEnabled())
                    netClient!.Connect(address);
            }

            public void ExitForUpdate()
            {
                Environment.ExitCode = 42;
                state.FadeOut(0.2, () => state.Game.Exit());
            }

            public void ExitForRepair()
            {
                Environment.ExitCode = 43;
                state.FadeOut(0.2, () => state.Game.Exit());
            }

            public void NewCharacter(string name, int index, Closure onError)
            {
                FLLog.Info("Net", $"Requesting new char: `{name}`");
                netSession.RpcServer.CreateNewCharacter(name, index).ContinueWith((task) =>
                {
                    if (!task.Result) state.Game.QueueUIThread(() => onError.Call());
                });
            }

            public void StopNetworking()
            {
                gatewayLoginCancellation?.Cancel();
                gatewayLoginCancellation?.Dispose();
                gatewayLoginCancellation = null;
                netClient?.Shutdown();
                netClient = null;
            }

            public override void Exit() => state.FadeOut(0.2, () => state.Game.Exit());
        }

        public override void Draw(double delta)
        {
            scene?.Draw(delta, Game.Width, Game.Height);
            ui.RenderWidget(delta);
            DoFade(delta);
            var dlist = Game.RenderContext.Renderer2D.CreateDrawList();
            cur.Draw(dlist, Game.Mouse, Game.TotalTime);
            dlist.Render();
        }

        private int uframe = 0;
        private bool newUI = false;
        public override void Update(double delta)
        {
            ui.Update(Game, delta);
            Game.TextInputEnabled = ui.KeyboardGrabbed;
            scene?.UpdateViewport(Game.RenderContext.CurrentViewport, (float) Game.Width / Game.Height);
            scene?.Update(delta);
            api._Update();
        }
#if DEBUG
        void LoadSpecific(int index)
        {
            intro = Game.GameData.GetIntroSceneSpecific(index);
            scene?.Dispose();
            TryRunScript(intro.Scripts);
            scene?.Update(1 / 60.0); // Do all the setup events - smoother entrance
            Game.Sound.PlayMusic(intro.Music, 0);
        }

        void Keyboard_KeyDown(KeyEventArgs e)
        {
            if ((e.Modifiers & KeyModifiers.LeftControl) == KeyModifiers.LeftControl)
            {
                switch (e.Key)
                {
                    case Keys.D1:
                        LoadSpecific(0);
                        break;
                    case Keys.D2:
                        LoadSpecific(1);
                        break;
                    case Keys.D3:
                        LoadSpecific(2);
                        break;
                    case Keys.D:
                        var p = scene?.Renderer?.FxPool;
                        if (p != null)
                            p.DrawDebug = !p.DrawDebug;
                        break;
                }
            }
        }
#endif

        public override void Exiting()
        {
            api.StopNetworking(); // Disconnect
        }

        protected override void OnUnload()
        {
            scene?.Dispose();
            Game.Keyboard.KeyDown -= UiKeyDown;
            Game.Keyboard.TextInput -= UiTextInput;
#if DEBUG
            Game.Keyboard.KeyDown -= Keyboard_KeyDown;
#endif
            Game.Keyboard.KeyUp -= Keyboard_OnKeyUp;
            Game.Mouse.MouseUp -= Mouse_MouseUp;
        }
    }
}
