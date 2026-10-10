using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using LancerNexus.Protocol;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcIdentityAllocatorTests
{
    [Fact]
    public async Task EmptyPoolQueuesSpawnContinuationWithoutWaitingForCoordinator()
    {
        var handler = new DelayedAllocationHandler();
        using var allocator = new NpcIdentityAllocator(
            new Uri("http://127.0.0.1:8080"), "test-key", "li-01", "li01", handler);
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(allocator.TryTake(out _));
        var received = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        allocator.WhenAvailable(id => received.TrySetResult(id));
        handler.ReleaseResponse();

        var npcId = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotEqual(Guid.Empty, npcId);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(128, handler.RequestedCount);
    }

    private sealed class DelayedAllocationHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }
        public int RequestedCount { get; private set; }

        public void ReleaseResponse() => release.TrySetResult(true);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = await request.Content!.ReadFromJsonAsync<NpcIdBatchAllocationRequest>(cancellationToken);
            Assert.NotNull(body);
            RequestedCount = body.Count;
            RequestStarted.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new NpcIdBatchAllocationResponse
                {
                    RequestId = body.RequestId,
                    Accepted = true,
                    ReasonCode = "allocated",
                    Npcs = Enumerable.Range(0, body.Count).Select(_ => new NpcOwnershipLease
                    {
                        NpcId = Guid.CreateVersion7(),
                        InstanceId = body.InstanceId,
                        OwnershipVersion = 1
                    }).ToArray()
                })
            };
        }
    }
}
