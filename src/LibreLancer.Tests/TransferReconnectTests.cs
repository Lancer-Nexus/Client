using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using LibreLancer.Client;
using LibreLancer.Net;
using LibreLancer.Net.Protocol;
using LiteNetLib;
using Xunit;

namespace LibreLancer.Tests;

public sealed class TransferReconnectTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IntentionalDisconnectPreservesSessionUntilTargetAcceptsOrRejects(bool rejectTarget)
    {
        var sourceListener = new EventBasedNetListener();
        var targetListener = new EventBasedNetListener();
        var source = new NetManager(sourceListener);
        var target = new NetManager(targetListener);
        var connection = new GameNetClient(new ImmediateUiThread());
        var session = new CGameSession(null!, connection);
        var disconnected = false;
        var targetRequested = false;
        var sourceGone = false;
        sourceListener.ConnectionRequestEvent += request => request.Accept();
        sourceListener.PeerConnectedEvent += SendLogin;
        sourceListener.PeerDisconnectedEvent += (_, _) => sourceGone = true;
        targetListener.ConnectionRequestEvent += request =>
        {
            targetRequested = true;
            if (rejectTarget)
                request.Reject();
            else
                request.Accept();
        };
        targetListener.PeerConnectedEvent += SendLogin;
        connection.Disconnected += _ => disconnected = true;
        Assert.True(source.Start(0));
        Assert.True(target.Start(0));
        connection.Start();
        try
        {
            connection.Connect(new IPEndPoint(IPAddress.Loopback, source.LocalPort));
            await PumpUntil(() => connection.Connected, source, target);
            // Let the ordered login acknowledgement arrive before starting the handoff.
            await Task.Delay(100);
            Assert.False(session.Update());

            connection.ReconnectWithTransferTicket($"127.0.0.1:{target.LocalPort}", "test-transfer-ticket");
            await PumpUntil(() => sourceGone && !connection.Connected, source);
            Assert.True(connection.IsTransferReconnecting);
            // The old code shut down networking and opened LuaMenu during this gap.
            Assert.False(session.Update());
            Assert.False(disconnected);

            await PumpUntil(() => targetRequested && !connection.IsTransferReconnecting, source, target);
            Assert.Equal(rejectTarget, disconnected);
            Assert.Equal(!rejectTarget, connection.Connected);
        }
        finally
        {
            connection.Shutdown();
            source.Stop();
            target.Stop();
        }
    }

    private static void SendLogin(NetPeer peer)
    {
        var packet = new PacketWriter();
        Packets.Write(packet, new LoginSuccessPacket());
        peer.Send(packet, DeliveryMethod.ReliableOrdered);
    }

    private static async Task PumpUntil(Func<bool> complete, params NetManager[] servers)
    {
        var elapsed = Stopwatch.StartNew();
        while (!complete())
        {
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10), "UDP test timed out");
            foreach (var server in servers)
                server.PollEvents();
            await Task.Delay(5);
        }
    }

    private sealed class ImmediateUiThread : IUIThread
    {
        public void QueueUIThread(Action work) => work();
    }
}
