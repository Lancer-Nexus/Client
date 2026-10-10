using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LancerNexus.Protocol;
using LibreLancer.Server;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcRetirementOutboxTests
{
    private static NpcRetirementRequestV1 Request() => new()
    {
        RequestId = Guid.NewGuid(), InstanceId = "source",
        Npcs = [new() { NpcId = Guid.NewGuid(), OwnershipVersion = 4, Reason = NpcRetirementReasonV1.Destroyed }]
    };

    [Fact]
    public async Task DiskPersistenceContinuesWhileCoordinatorIsBlocked()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new Handler(directory, blocked: true);
            await using var outbox = new NpcRetirementOutbox(directory, new Uri("http://127.0.0.1"), "test", "source", handler);
            await outbox.Ready.WaitAsync(TimeSpan.FromSeconds(3));
            var first = Request();
            var queued = outbox.Queue(first);
            await queued.Durable.WaitAsync(TimeSpan.FromSeconds(3));
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var second = Request();
            await outbox.Queue(second).Durable.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(queued.Completed.IsCompleted);
            Assert.True(outbox.IsPending(first.Npcs[0].NpcId, 4));
            Assert.True(File.Exists(Path.Combine(directory, $"{second.RequestId:N}.pending")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RestartReplaysExactDurableRequestAndRetainsConfirmedResponse()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var request = Request();
        try
        {
            await using (var original = new NpcRetirementOutbox(directory, new Uri("http://127.0.0.1"),
                             "test", "source", new Handler(directory, blocked: true)))
            {
                await original.Ready;
                await original.Queue(request).Durable.WaitAsync(TimeSpan.FromSeconds(3));
            }
            var handler = new Handler(directory);
            await using var restarted = new NpcRetirementOutbox(directory, new Uri("http://127.0.0.1"), "test", "source", handler);
            await restarted.Ready;
            var restored = await handler.Received.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(request.RequestId, restored.RequestId);
            Assert.Equal(request.Npcs, restored.Npcs);
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (restarted.IsPending(request.Npcs[0].NpcId, 4) && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            Assert.False(restarted.IsPending(request.Npcs[0].NpcId, 4));
            Assert.True(restarted.BlocksActivation(request.Npcs[0].NpcId, 4));
            Assert.False(restarted.BlocksActivation(request.Npcs[0].NpcId, 5));
            Assert.False(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.pending")));
            Assert.True(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.response")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task MismatchedResponseCannotAcknowledgeDurableIntent()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new Handler(directory, mismatch: true);
            Task<NpcRetirementResponseV1>? completion = null;
            var request = Request();
            await using (var outbox = new NpcRetirementOutbox(directory, new Uri("http://127.0.0.1"), "test", "source", handler))
            {
                await outbox.Ready;
                var queued = outbox.Queue(request);
                completion = queued.Completed;
                await queued.Durable.WaitAsync(TimeSpan.FromSeconds(3));
                await handler.Received.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Task.Delay(150);
                Assert.False(queued.Completed.IsCompleted);
                Assert.True(outbox.IsPending(request.Npcs[0].NpcId, 4));
            }
            Assert.NotNull(completion);
            Assert.True(completion.IsCanceled);
            Assert.True(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.pending")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ConfirmedFenceRemainsBlockedAfterAnotherRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var request = Request();
        try
        {
            await using (var outbox = new NpcRetirementOutbox(directory, new Uri("http://127.0.0.1"),
                             "test", "source", new Handler(directory)))
            {
                await outbox.Ready;
                var result = await outbox.Queue(request).Completed.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.True(result.Npcs[0].Accepted);
            }
            var handler = new Handler(directory);
            await using var restored = new NpcRetirementOutbox(directory, new Uri("http://127.0.0.1"), "test", "source", handler);
            await restored.Ready.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(restored.BlocksActivation(request.Npcs[0].NpcId, 4));
            Assert.False(restored.BlocksActivation(request.Npcs[0].NpcId, 5));
            Assert.False(handler.Received.Task.IsCompleted);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CorruptStoredResponsePreventsRecoveryReadiness()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory, $"{Guid.NewGuid():N}.response"),
                MessagePack.MessagePackSerializer.Serialize(new NpcRetirementResponseV1()));
            var outbox = new NpcRetirementOutbox(directory, new Uri("http://127.0.0.1"), "test", "source", new Handler(directory));
            await Assert.ThrowsAsync<InvalidDataException>(() => outbox.Ready);
            await Assert.ThrowsAsync<InvalidDataException>(() => outbox.DisposeAsync().AsTask());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task PendingRetirementBlocksSnapshotRestorationBeforeObjectCreation()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            await using var outbox = new NpcRetirementOutbox(directory, new Uri("http://127.0.0.1"),
                "test", "source", new Handler(directory, blocked: true));
            await outbox.Ready;
            var request = Request();
            await outbox.Queue(request).Durable.WaitAsync(TimeSpan.FromSeconds(3));
            var server = (GameServer)RuntimeHelpers.GetUninitializedObject(typeof(GameServer));
            server.InstanceId = "source";
            server.NpcCoordinatorUrl = "http://127.0.0.1";
            server.NpcTransferStagingDirectory = directory;
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameServer).GetField("npcRetirementSync", fields)!.SetValue(server, new object());
            typeof(GameServer).GetField("npcCoordinatorApiKey", fields)!.SetValue(server, "test");
            typeof(GameServer).GetField("npcRetirementOutbox", fields)!.SetValue(server, outbox);
            var world = (ServerWorld)RuntimeHelpers.GetUninitializedObject(typeof(ServerWorld));
            world.Server = server;
            world.System = new LibreLancer.Data.GameData.World.StarSystem { SourceFile = "fixture.ini", Nickname = "li03" };
            world.GameWorld = new GameWorld(null, null, null, null, initPhys: false);
            var manager = (NPCManager)RuntimeHelpers.GetUninitializedObject(typeof(NPCManager));
            manager.World = world;
            var state = new NpcRuntimeStateV1 { LoadoutArchetype = "fixture", Orientation = new() { W = 1 },
                Ai = new() { StateId = "none", PreviousStateId = "none" } };
            var snapshot = new NpcTransferSnapshot { TransferId = Guid.NewGuid(), TargetSystemId = "li03",
                NpcIds = [request.Npcs[0].NpcId], Npcs = [new() { NpcId = request.Npcs[0].NpcId,
                    OwnershipVersion = 3, SystemId = "li01", RuntimeState = MessagePack.MessagePackSerializer.Serialize(state) }] };
            var failure = Assert.Throws<InvalidOperationException>(() => manager.RestoreTransfer(snapshot));
            Assert.Contains("retirement intent", failure.Message);
            Assert.Empty(world.GameWorld.Objects);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Handler(string directory, bool blocked = false, bool mismatch = false) : HttpMessageHandler
    {
        public TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<NpcRetirementRequestV1> Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken token)
        {
            var request = (await message.Content!.ReadFromJsonAsync<NpcRetirementRequestV1>(token))!;
            Assert.True(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.pending")));
            Assert.Equal("Bearer", message.Headers.Authorization!.Scheme);
            Started.TrySetResult(true);
            Received.TrySetResult(request);
            if (blocked)
                await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new NpcRetirementResponseV1
                {
                    RequestId = mismatch ? Guid.NewGuid() : request.RequestId, ReasonCode = "processed",
                    Npcs = [new() { NpcId = request.Npcs[0].NpcId, Accepted = true,
                        OwnershipVersion = 5, ReasonCode = "retired" }]
                })
            };
        }
    }
}
