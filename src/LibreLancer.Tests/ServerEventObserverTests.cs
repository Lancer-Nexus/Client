using System;
using System.IO;
using LibreLancer.Data.IO;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ServerEventObserverTests
{
    [Fact]
    public void ObserverReceivesEventsWithoutStealingTheExistingServerQueue()
    {
        var game = new GameServer(new FileSystem(new EmptyFileSystem()));
        ServerEvent observed = default;
        game.ServerEventObserver = serverEvent => observed = serverEvent;
        var expected = new ServerEvent
        {
            Type = ServerEventType.PlayerConnected,
            TimeUtc = DateTime.UtcNow,
            Payload = new object()
        };

        game.PublishServerEvent(expected);

        Assert.Equal(expected.Type, observed.Type);
        Assert.Same(expected.Payload, observed.Payload);
        Assert.True(game.ServerEvents.TryDequeue(out var queued));
        Assert.Equal(expected.Type, queued.Type);
        Assert.Same(expected.Payload, queued.Payload);
    }

    [Fact]
    public void ObserverFailureDoesNotPreventExistingServerEventDelivery()
    {
        var game = new GameServer(new FileSystem(new EmptyFileSystem()));
        game.ServerEventObserver = _ => throw new InvalidOperationException("observer failure");
        var expected = new ServerEvent
        {
            Type = ServerEventType.PlayerDisconnected,
            TimeUtc = DateTime.UtcNow,
            Payload = new object()
        };

        game.PublishServerEvent(expected);

        Assert.True(game.ServerEvents.TryDequeue(out var queued));
        Assert.Equal(expected.Type, queued.Type);
    }

    private sealed class EmptyFileSystem : BaseFileSystemProvider
    {
        public EmptyFileSystem() => Refresh();

        public override void Refresh()
        {
            Root = new VfsDirectory();
            var exe = new VfsDirectory { Name = "EXE", Parent = Root };
            Root.Items["EXE"] = exe;
            exe.Items.Add("freelancer.ini", new EmptyFile { Name = "freelancer.ini" });
        }

        private sealed class EmptyFile : VfsFile
        {
            public override Stream OpenRead() => new MemoryStream();
        }
    }
}
