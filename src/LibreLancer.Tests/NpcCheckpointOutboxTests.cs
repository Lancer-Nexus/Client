using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LancerNexus.Protocol;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcCheckpointOutboxTests
{
    private static NpcCheckpointWriteRequestV1 Request() => new()
    {
        RequestId = Guid.NewGuid(),
        InstanceId = "source",
        SystemId = "li01",
        Npcs = [new() { NpcId = Guid.NewGuid(), OwnershipVersion = 4, SystemId = "li01",
            RuntimeState = MessagePack.MessagePackSerializer.Serialize(new NpcRuntimeStateV1
            { Orientation = new() { W = 1 }, LoadoutArchetype = "freighter",
                Ai = new() { StateId = "idle", PreviousStateId = "idle" } }) }],
        ExpectedRevisions = [new() { NpcId = Guid.Empty, OwnershipVersion = 4 }]
    };

    [Fact]
    public async Task DurableAsyncDeliveryPersistsResponseAndRevisionAcrossRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var request = Request();
            request = request with { ExpectedRevisions = [request.ExpectedRevisions[0] with { NpcId = request.Npcs[0].NpcId }] };
            await using (var outbox = new NpcCheckpointOutbox(directory, new Uri("http://127.0.0.1"), "test", "source",
                             new Handler()))
            {
                await outbox.Ready.WaitAsync(TimeSpan.FromSeconds(3));
                var queued = outbox.Queue(request);
                await queued.Durable.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.True(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.pending")));
                var response = await queued.Completed.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.True(response.Accepted);
                Assert.Equal(1, outbox.GetRevision(request.Npcs[0].NpcId, 4));
                Assert.True(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.response")));
                Assert.False(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.pending")));
            }
            await using var restarted = new NpcCheckpointOutbox(directory, new Uri("http://127.0.0.1"), "test", "source",
                new Handler());
            await restarted.Ready.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1, restarted.GetRevision(request.Npcs[0].NpcId, 4));
            Assert.Equal(0, restarted.GetRevision(request.Npcs[0].NpcId, 5));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CoordinatorNetworkWaitDoesNotDelayDurableDiskWrite()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new Handler(blocked: true);
            var request = Request();
            request = request with { ExpectedRevisions = [request.ExpectedRevisions[0] with { NpcId = request.Npcs[0].NpcId }] };
            Task<NpcCheckpointWriteResponseV1>? completion;
            await using (var outbox = new NpcCheckpointOutbox(directory, new Uri("http://127.0.0.1"), "test", "source", handler))
            {
                await outbox.Ready.WaitAsync(TimeSpan.FromSeconds(3));
                var queued = outbox.Queue(request);
                completion = queued.Completed;
                await queued.Durable.WaitAsync(TimeSpan.FromSeconds(3));
                await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.True(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.pending")));
                Assert.False(queued.Completed.IsCompleted);
            }
            Assert.NotNull(completion);
            Assert.True(completion.IsCanceled);
            Assert.True(File.Exists(Path.Combine(directory, $"{request.RequestId:N}.pending")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RecoveryLoadsOnlyCurrentSystemAndRestoresCheckpointRevision()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var request = Request();
            request = request with { ExpectedRevisions = [request.ExpectedRevisions[0] with
                { NpcId = request.Npcs[0].NpcId }] };
            var recovery = new NpcCheckpointRecoveryV1
            {
                Snapshot = request,
                Result = new NpcCheckpointWriteResponseV1
                {
                    RequestId = request.RequestId,
                    Accepted = true,
                    Revisions = request.ExpectedRevisions.Select(expected => expected with
                        { Revision = expected.Revision + 1 }).ToArray()
                }
            };
            var handler = new RecoveryHandler(recovery);
            await using var outbox = new NpcCheckpointOutbox(directory, new Uri("http://127.0.0.1"),
                "test-key", "source", handler);
            await outbox.Ready.WaitAsync(TimeSpan.FromSeconds(3));

            var page = await outbox.GetRecoveryPageAsync("Li01", null);
            Assert.Equal(request.RequestId, Assert.Single(page.CheckpointIds));
            var recovered = await outbox.GetRecoveryCheckpointAsync(request.RequestId, "li01");

            Assert.NotNull(recovered);
            Assert.Equal(request.RequestId, recovered.Snapshot.RequestId);
            Assert.Equal(1, outbox.GetRevision(request.Npcs[0].NpcId, 4));
            Assert.Equal(2, handler.Requests);
            Assert.True(handler.AllRequestsAuthenticated);
            Assert.True(handler.RecoverySystemFilterIsCanonical);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class Handler(bool blocked = false) : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Started.TrySetResult();
            if (blocked) await Task.Delay(Timeout.Infinite, token);
            var body = await request.Content!.ReadFromJsonAsync<NpcCheckpointWriteRequestV1>(token);
            var response = new NpcCheckpointWriteResponseV1
            {
                RequestId = body!.RequestId,
                Accepted = true,
                ReasonCode = "checkpointed",
                Revisions = body.ExpectedRevisions.Select(expected => expected with
                { Revision = expected.Revision + 1 }).ToArray()
            };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }
    }

    private sealed class RecoveryHandler(NpcCheckpointRecoveryV1 recovery) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public bool AllRequestsAuthenticated { get; private set; } = true;
        public bool RecoverySystemFilterIsCanonical { get; private set; } = true;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            AllRequestsAuthenticated &= request.Headers.Authorization?.Parameter == "test-key";
            if (!request.RequestUri!.AbsolutePath.Contains(recovery.Snapshot.RequestId.ToString("D"), StringComparison.Ordinal))
                RecoverySystemFilterIsCanonical &= request.RequestUri.Query.Contains("systemId=li01", StringComparison.Ordinal);
            if (request.RequestUri!.AbsolutePath.EndsWith("/recovery", StringComparison.Ordinal) &&
                request.RequestUri.AbsolutePath.Contains(recovery.Snapshot.RequestId.ToString("D"), StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(recovery) });
            if (request.RequestUri.AbsolutePath.EndsWith("/recovery", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(new NpcCheckpointRecoveryPageV1
                        { CheckpointIds = [recovery.Snapshot.RequestId] }) });
            throw new InvalidOperationException("Unexpected NPC recovery request.");
        }
    }
}
