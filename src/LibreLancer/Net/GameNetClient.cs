// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LibreLancer.Net.Protocol;
using LibreLancer.Net.Protocol.RpcPackets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibreLancer.Net
{
    public class GameNetClient : IPacketConnection
    {
        private bool running = false;
        private IUIThread mainThread;
        private Thread networkThread = null!;
        private NetManager client = null!;
        public string AppIdentifier = LNetConst.DEFAULT_APP_IDENT;

        public string ClusterInstanceId { get; set; } = "";
        public string ClusterSystemId { get; set; } = "";
        public string ClusterEndpoint { get; set; } = "";
        public string ClusterGatewayUrl { get; set; } = "";
        public string ClusterAccessToken { get; set; } = "";
        public string ClusterRefreshToken { get; set; } = "";
        public Guid ClusterSessionId { get; set; }
        private (IPEndPoint Endpoint, string Ticket)? pendingTransferReconnect;
        private volatile bool transferReconnecting;
        public bool IsTransferReconnecting => transferReconnecting;
        private readonly SemaphoreSlim clusterRefreshGate = new(1, 1);
        private CancellationTokenSource? clusterHeartbeatCancellation;
        private Task? clusterHeartbeatTask;

        public int MaxSequencedSize => 500; // Min safe UDP packet size - 8 bytes overhead
        public event Action<LocalServerInfo>? ServerFound;
        public event Action<bool>? AuthenticationRequired;
        public event Action<DisconnectReason>? Disconnected;
        public Guid UUID;
        private ConcurrentQueue<IPacket> packets = new();
        private HttpClient http = null!;

        private Stopwatch sw = null!;
        private List<LocalServerInfo> srvinfo = [];

        private AuthInfo? loginServer;
        private IPEndPoint loginEndpoint = null!;
        private bool connecting = true;

        public int LossPercent
        {
            get
            {
                if (running)
                {
                    return (int) (client?.FirstPeer?.Statistics?.PacketLossPercent ?? 100);
                }

                return -1;
            }
        }

        public int Ping
        {
            get
            {
                if (running)
                    // LiteNetLib returns Ping as RTT/2 - not the regular measure of ping.
                {
                    return (client?.FirstPeer?.Ping ?? 0) * 2;
                }

                return -1;
            }
        }

        public int BytesSent
        {
            get
            {
                if (running)
                {
                    return (int) (client?.Statistics?.BytesSent ?? 0);
                }

                return 0;
            }
        }

        public int BytesReceived
        {
            get
            {
                if (running)
                {
                    return (int) (client?.Statistics?.BytesReceived ?? 0);
                }

                return 0;
            }
        }

        public uint EstimateTickDelay()
        {
            const float TickMs = (1 / 60.0f) * 1000.0f;
            var ticks = (int) Math.Ceiling(Ping / TickMs);
            if (ticks < 2)
            {
                return 2;
            }

            return (uint) ticks;
        }

        public void Start()
        {
            if (running)
            {
                throw new InvalidOperationException();
            }

            running = true;
            networkThread?.Join();
            networkThread = new Thread(NetworkThread)
            {
                Name = "NetClient"
            };
            networkThread.Start();
        }

        public GameNetClient(IUIThread mainThread)
        {
            this.mainThread = mainThread;
        }

        public void Stop()
        {
            if (!running)
            {
                throw new InvalidOperationException();
            }

            running = false;
            transferReconnecting = false;
            clusterHeartbeatCancellation?.Cancel();
        }

        public bool Connected =>
            (client?.FirstPeer != null && client.FirstPeer.ConnectionState == ConnectionState.Connected);

        private long localPeerRequests;

        public void DiscoverLocalPeers()
        {
            if (running)
            {
                Interlocked.Increment(ref localPeerRequests);
            }
        }

        public void DiscoverGlobalPeers()
        {
            // HTTP?
        }

        public void Connect(IPEndPoint endPoint)
        {
            ConnectInternal(endPoint, null);
        }

        private void ConnectInternal(IPEndPoint endPoint, string? token)
        {
            var dw = new PacketWriter();
            dw.Put(AppIdentifier + GeneratedProtocol.PROTOCOL_HASH);
            if (!string.IsNullOrEmpty(token))
            {
                dw.Put(token);
            }

            if (!running)
            {
                throw new InvalidOperationException();
            }

            lock (srvinfo) srvinfo.Clear();
            connecting = true;
            loginServer = null;
            while (client is not { IsRunning: true })
            {
                Thread.Sleep(0);
            }

            client.Statistics?.Reset();
            client.Connect(endPoint, dw);
        }

        public void Connect(string str)
        {
            Task.Run(() =>
            {
                if (ParseEP(str, out var ep))
                {
                    Connect(ep);
                }
                else
                {
                    mainThread.QueueUIThread(() => { Disconnected?.Invoke(DisconnectReason.InvalidEndpoint); });
                }
            });
        }

        public void ConnectWithTicket(string endpoint, string ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket))
                throw new ArgumentException("Join ticket is required.", nameof(ticket));
            StartClusterSessionHeartbeat();
            Task.Run(() =>
            {
                if (!running)
                    return;
                if (ParseEP(endpoint, out var ep))
                    ConnectInternal(ep, ticket);
                else
                    mainThread.QueueUIThread(() => Disconnected?.Invoke(DisconnectReason.InvalidEndpoint));
            });
        }

        public void ReconnectWithTransferTicket(string endpoint, string transferTicket)
        {
            if (string.IsNullOrWhiteSpace(transferTicket) || !ParseEP(endpoint, out var parsed))
                throw new ArgumentException("Transfer endpoint or ticket is invalid.");
            var source = client?.FirstPeer;
            if (!running || source?.ConnectionState != ConnectionState.Connected)
                throw new InvalidOperationException("A transfer requires an active source connection.");
            lock (this)
            {
                transferReconnecting = true;
                pendingTransferReconnect = (parsed, transferTicket);
            }
            source.Disconnect();
        }

        private static bool ParseEP(string str, out IPEndPoint endpoint)
        {
            endpoint = new IPEndPoint(IPAddress.None, 0);
            if (str.StartsWith("quic://", StringComparison.OrdinalIgnoreCase))
                str = str.Substring("quic://".Length);
            else if (str.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
                str = str.Substring("udp://".Length);

            if (IPAddress.TryParse(str, out var ip))
            {
                endpoint = new IPEndPoint(ip, LNetConst.DEFAULT_PORT);
                return true;
            }

            if (str.Contains(":"))
            {
                var idxOf = str.LastIndexOf(':');
                var first = str.Remove(idxOf);
                var last = str.Substring(idxOf + 1);
                if (!int.TryParse(last, out var portNum))
                {
                    return false;
                }

                if (IPAddress.TryParse(first, out ip))
                {
                    endpoint = new IPEndPoint(ip, portNum);
                    return true;
                }
                else
                {
                    if (TryResolve(first, out ip))
                    {
                        endpoint = new IPEndPoint(ip, portNum);
                        return true;
                    }

                    return false;
                }
            }

            if (TryResolve(str, out ip))
            {
                endpoint = new IPEndPoint(ip, LNetConst.DEFAULT_PORT);
                return true;
            }

            return false;
        }

        private static bool TryResolve(string str, out IPAddress addr)
        {
            addr = IPAddress.None;

            try
            {
                var addresses = Dns.GetHostAddresses(str);

                if (addresses.Length > 0)
                {
                    addr = addresses[0];
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Login(string username, string password)
        {
            if (!running || loginServer == null)
            {
                throw new InvalidOperationException();
            }

            Task.Run(async () =>
            {
                var token = await http.Login(loginServer, username, password);

                if (token != null)
                {
                    ConnectInternal(loginEndpoint, token);
                }
                else
                {
                    FLLog.Error("Http", "Login failed");
                    mainThread.QueueUIThread(() => AuthenticationRequired?.Invoke(true));
                }
            });
        }


        private void NetworkThread()
        {
            sw = Stopwatch.StartNew();
            http = new HttpClient();
            var listener = new EventBasedNetListener();
            client = new NetManager(listener)
            {
                UnconnectedMessagesEnabled = true,
                IPv6Enabled = true,
                NatPunchEnabled = true,
                EnableStatistics = true,
                ChannelsCount = 3
            };
            listener.NetworkReceiveUnconnectedEvent += (remote, npr, type) =>
            {
                if (type == UnconnectedMessageType.Broadcast)
                {
                    return;
                }

                var msg = new PacketReader(npr);

                if (msg.GetByte() == 0)
                {
                    lock (srvinfo)
                    {
                        foreach (var info in srvinfo)
                        {
                            if (!info.EndPoint.Equals(remote))
                            {
                                continue;
                            }

                            var t = sw.ElapsedMilliseconds;
                            info.Ping = (int) (t - info.LastPingTime);
                            if (info.Ping < 0)
                            {
                                info.Ping = 0;
                            }
                        }
                    }
                }
                else if (ServerFound != null)
                {
                    var info = new LocalServerInfo
                    {
                        EndPoint = remote,
                        Unique = msg.GetGuid(),
                        Name = msg.GetString()!,
                        Description = msg.GetString()!,
                        DataVersion = msg.GetString()!,
                        CurrentPlayers = (int) msg.GetVariableUInt32(),
                        MaxPlayers = (int) msg.GetVariableUInt32(),
                        LastPingTime = sw.ElapsedMilliseconds,
                    };

                    info.EndPoint.Port = (int) msg.GetVariableUInt32();

                    PacketWriter writer = new PacketWriter();
                    writer.Put(LNetConst.PING_MAGIC);
                    client.SendUnconnectedMessage(writer, remote);

                    lock (srvinfo)
                    {
                        var add = true;

                        foreach (var infoObj in srvinfo.Where(infoObj => infoObj.Unique == info.Unique))
                        {
                            add = false;

                            // Prefer IPv6
                            if (infoObj.EndPoint.AddressFamily != AddressFamily.InterNetwork &&
                                info.EndPoint.AddressFamily == AddressFamily.InterNetwork)
                            {
                                infoObj.EndPoint = info.EndPoint;
                            }

                            break;
                        }

                        if (add)
                        {
                            srvinfo.Add(info);
                            mainThread.QueueUIThread(() => ServerFound?.Invoke(info));
                        }
                    }
                }

                npr.Recycle();
            };
            listener.NetworkReceiveEvent += (peer, msg, channel, method) =>
            {
#if !DEBUG
                try
                {
#endif
                    var reader = new PacketReader(msg);
                    var pkt = Packets.Read(reader);
                    if (connecting)
                    {
                        if (pkt is GuidAuthenticationPacket)
                        {
                            FLLog.Info("Net", "GUID Request Received");
                            SendPacket(new AuthenticationReplyPacket() { Guid = this.UUID },
                                PacketDeliveryMethod.ReliableOrdered);
                        }
                        else if (pkt is LoginSuccessPacket)
                        {
                            FLLog.Info("Client", "Login success");
                            connecting = false;
                            transferReconnecting = false;
                        }
                        else
                        {
                            FLLog.Info("Net", $"Invalid login packet {pkt.GetType()}");
                            client.DisconnectAll();
                        }
                    }
                    else
                    {
                        packets.Enqueue(pkt);
                    }
#if !DEBUG
                }

                catch (Exception e)
                {
                    FLLog.Error("Client", "Error reading packet");
                    client.DisconnectAll();
                }
#endif
            };

            listener.PeerDisconnectedEvent += (peer, info) =>
            {
                (IPEndPoint Endpoint, string Ticket)? transferReconnect;
                lock (this)
                {
                    transferReconnect = pendingTransferReconnect;
                    pendingTransferReconnect = null;
                }
                if (transferReconnect is { } next)
                {
                    packets.Clear();
                    FLLog.Info("Transfer", $"Source disconnected; reconnecting to {next.Endpoint}");
                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(250).ConfigureAwait(false);
                            if (running)
                                ConnectInternal(next.Endpoint, next.Ticket);
                        }
                        catch (Exception error)
                        {
                            FLLog.Error("Transfer", $"Target reconnect failed: {error.GetType().Name}");
                            transferReconnecting = false;
                            mainThread.QueueUIThread(() => Disconnected?.Invoke(DisconnectReason.ConnectionError));
                        }
                    });
                    return;
                }
                transferReconnecting = false;
                var additional = new PacketReader(info.AdditionalData);

                if (additional.TryGetDisconnectReason(out var reason) &&
                    connecting &&
                    reason == DisconnectReason.TokenRequired &&
                    additional.TryGetString(out var url))
                {
                    loginEndpoint = new IPEndPoint(peer.Address, peer.Port);
                    Task.Run(async () =>
                    {
                        loginServer = await http.LoginServerInfo(url);

                        if (loginServer != null)
                        {
                            mainThread.QueueUIThread(() => AuthenticationRequired?.Invoke(false));
                        }
                        else
                        {
                            FLLog.Error("Net", "Remote disconnected with invalid login server");
                            mainThread.QueueUIThread(() => { Disconnected?.Invoke(DisconnectReason.ConnectionError); });
                        }
                    });
                }
                else
                {
                    mainThread.QueueUIThread(() =>
                    {
                        FLLog.Info("Net", $"Remote disconnected for reason '{reason}' ({info.Reason})");
                        Disconnected?.Invoke(reason == DisconnectReason.Unknown
                            ? DisconnectReason.ConnectionError
                            : reason);
                    });
                }
            };

            client.Start();

            while (running)
            {
                if (Interlocked.Read(ref localPeerRequests) > 0)
                {
                    Interlocked.Decrement(ref localPeerRequests);
                    lock (srvinfo) srvinfo.Clear();
                    var dw = new PacketWriter();
                    dw.Put(LNetConst.BROADCAST_KEY);
                    FLLog.Debug("Net", "Sending broadcast");
                    client.SendBroadcast(dw, LNetConst.BROADCAST_PORT);
                }

                // ping servers
                lock (srvinfo)
                {
                    foreach (var inf in srvinfo)
                    {
                        var nowMs = sw.ElapsedMilliseconds;

                        if (nowMs - inf.LastPingTime > 2000) // ping every 2 seconds?
                        {
                            inf.LastPingTime = nowMs;
                            var om = new PacketWriter();
                            om.Put(LNetConst.PING_MAGIC);
                            client.SendUnconnectedMessage(om, inf.EndPoint);
                        }
                    }
                }

                // events
                client.PollEvents();
                Thread.Sleep(1);
            }

            client.DisconnectAll();
            client.Stop();
            clusterHeartbeatCancellation?.Cancel();
            http.Dispose();
        }

        public async Task RefreshClusterSessionAsync(CancellationToken cancellationToken = default)
        {
            if (ClusterSessionId == Guid.Empty || string.IsNullOrWhiteSpace(ClusterRefreshToken) ||
                string.IsNullOrWhiteSpace(ClusterGatewayUrl))
                return;
            await clusterRefreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var gateway = new NexusGatewayLogin(new Uri(ClusterGatewayUrl, UriKind.Absolute));
                var refreshed = await gateway.RefreshAsync(ClusterSessionId, ClusterRefreshToken, cancellationToken)
                    .ConfigureAwait(false);
                ClusterAccessToken = refreshed.AccessToken;
                ClusterRefreshToken = refreshed.RefreshToken;
            }
            finally
            {
                clusterRefreshGate.Release();
            }
        }

        private void StartClusterSessionHeartbeat()
        {
            if (clusterHeartbeatTask != null || ClusterSessionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(ClusterRefreshToken) || string.IsNullOrWhiteSpace(ClusterGatewayUrl))
                return;
            clusterHeartbeatCancellation = new CancellationTokenSource();
            var cancellationToken = clusterHeartbeatCancellation.Token;
            clusterHeartbeatTask = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
                        if (!running)
                            return;
                        if (Connected)
                            await RefreshClusterSessionAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (UnauthorizedAccessException error)
                    {
                        FLLog.Warning("Net", $"Gateway session refresh rejected: {error.Message}");
                        return;
                    }
                    catch (Exception error)
                    {
                        FLLog.Warning("Net", $"Gateway session refresh failed: {error.Message}");
                    }
                }
            }, cancellationToken);
        }

        public void Update() => client?.TriggerUpdate();

        public void SendPacket(IPacket packet, PacketDeliveryMethod method)
        {
            var om = new PacketWriter(new NetDataWriter());
            Packets.Write(om, packet);
            method.ToLiteNetLib(out DeliveryMethod mt, out byte ch);
            client.FirstPeer?.Send(om, ch, mt);
        }

        public void Shutdown()
        {
            if (running)
            {
                Stop();
            }
        }

        public bool PollPacket([MaybeNullWhen(false)] out IPacket packet)
        {
            packet = null;
            return !packets.IsEmpty && packets.TryDequeue(out packet);
        }
    }
}
