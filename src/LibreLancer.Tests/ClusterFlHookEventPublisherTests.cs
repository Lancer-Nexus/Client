#if LANCER_NEXUS_FLHOOKCOMPAT_TESTS
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LancerNexus.FlhookCompat;
using LancerNexus.FlhookCompat.Hosting;
using LancerNexus.Protocol;
using LLServer;
using MessagePack;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ClusterFlHookEventPublisherTests
{
    [Fact]
    public async Task LostAcknowledgementRetriesSameDurableEventAndDuplicateAckAdvancesCursor()
    {
        Assert.True(QuicListener.IsSupported && QuicConnection.IsSupported,
            "This integration test requires System.Net.Quic and libmsquic.");
        var directory = Path.Combine(Path.GetTempPath(), "ln-flhook-publisher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var journalPath = Path.Combine(directory, "events.jsonl");
        var clientCertificatePath = Path.Combine(directory, "agent.pfx");
        var caPath = Path.Combine(directory, "ca.cer");
        var cursorPath = Path.Combine(directory, "cursor.json");
        using var authority = CreateAuthority();
        using var coordinatorCertificate = CreateLeaf(authority, "localhost", "1.3.6.1.5.5.7.3.1");
        using var agentCertificate = CreateLeaf(authority, "agent-01", "1.3.6.1.5.5.7.3.2");
        await File.WriteAllBytesAsync(clientCertificatePath, agentCertificate.Export(X509ContentType.Pkcs12));
        await File.WriteAllBytesAsync(caPath, authority.Export(X509ContentType.Cert));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var errors = new ConcurrentQueue<Exception>();
        var retryAccepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FlHookEventV1? firstAttempt = null;
        await using var listener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
            ListenBacklog = 8,
            ApplicationProtocols = [new SslApplicationProtocol("lancer-nexus-control/1")],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0x100,
                DefaultCloseErrorCode = 0x101,
                MaxInboundBidirectionalStreams = 4,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = [new SslApplicationProtocol("lancer-nexus-control/1")],
                    EnabledSslProtocols = SslProtocols.Tls13,
                    ClientCertificateRequired = true,
                    ServerCertificate = coordinatorCertificate,
                    RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                        certificate is X509Certificate2 peer && ChainsTo(peer, authority) && HasDnsSan(peer, "agent-01")
                }
            })
        }, cancellation.Token);

        var eventHub = new FlHookCompatServerEventHub("li-01", durableJournalPath: journalPath);
        eventHub.Publish(FlHookCompatServerEventKind.PlayerConnected, "account-01");
        var coordinator = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var connection = await listener.AcceptConnectionAsync(cancellation.Token);
                var (helloRequest, helloStream) = await ReceiveAsync(connection, cancellation.Token);
                Assert.Equal((ushort)ClusterMessageType.Hello, helloRequest.MessageType);
                var hello = MessagePackSerializer.Deserialize<ClusterHello>(helloRequest.Payload);
                Assert.Equal("agent-01", hello.NodeId);
                Assert.Equal("li-01", hello.InstanceId);
                await using (helloStream)
                    await RespondAsync(helloStream, helloRequest, ClusterMessageType.Hello,
                        new ClusterHandshakeResponse
                        {
                            Accepted = true,
                            ReasonCode = "accepted",
                            NegotiatedCapabilities = [ClusterCapabilities.FlHookEventsV1]
                        }, cancellation.Token);

                var (eventRequest, eventStream) = await ReceiveAsync(connection, cancellation.Token);
                await using (eventStream)
                {
                    Assert.Equal((ushort)ClusterMessageType.FlHookEvent, eventRequest.MessageType);
                    var value = MessagePackSerializer.Deserialize<FlHookEventV1>(eventRequest.Payload);
                    Assert.True(value.IsValid());
                    Assert.Equal("li-01", value.InstanceId);
                    Assert.Equal(1, value.Sequence);

                    if (attempt == 0)
                    {
                        // Coordinator storage succeeded, but the first response is lost.
                        firstAttempt = value;
                        await connection.CloseAsync(0, cancellation.Token);
                        continue;
                    }

                    Assert.NotNull(firstAttempt);
                    Assert.Equal(firstAttempt!.ProducerEpoch, value.ProducerEpoch);
                    Assert.Equal(firstAttempt.Sequence, value.Sequence);
                    Assert.Equal(firstAttempt.InstanceId, value.InstanceId);
                    Assert.Equal(firstAttempt.OccurredUtc, value.OccurredUtc);
                    Assert.Equal(firstAttempt.Kind, value.Kind);
                    Assert.Equal(firstAttempt.PlayerId, value.PlayerId);
                    await RespondAsync(eventStream, eventRequest, ClusterMessageType.FlHookEventIngestResult,
                        new FlHookEventIngestResultV1
                        {
                            CorrelationId = eventRequest.CorrelationId,
                            InstanceId = value.InstanceId,
                            ProducerEpoch = value.ProducerEpoch,
                            Sequence = value.Sequence,
                            Status = FlHookEventIngestStatusV1.Duplicate
                        }, cancellation.Token);
                    retryAccepted.TrySetResult();
                    return;
                }
            }
        }, cancellation.Token);

        try
        {
            await using (var publisher = new CoordinatorFlHookEventPublisher(eventHub,
                             new CoordinatorFlHookEventPublisher.Settings(
                                 (IPEndPoint)listener.LocalEndPoint, "localhost", "agent-01", "li-01",
                                 clientCertificatePath, null, caPath, cursorPath, errors.Enqueue)))
            {
                publisher.Start();
                var timeout = Task.Delay(TimeSpan.FromSeconds(15), cancellation.Token);
                var completed = await Task.WhenAny(retryAccepted.Task, coordinator, timeout);
                if (completed == timeout)
                    throw new InvalidOperationException("Timed out waiting for Coordinator event ingest. Transport errors: " +
                        string.Join(" | ", errors.Select(error => error.ToString())));
                if (completed == coordinator)
                    await coordinator;
                if (!retryAccepted.Task.IsCompleted)
                    throw new InvalidOperationException("Coordinator did not receive the retry. Transport errors: " +
                        string.Join(" | ", errors.Select(error => error.ToString())));
                await retryAccepted.Task;

                var ackDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(3);
                long cursorSequence = 0;
                while (DateTimeOffset.UtcNow < ackDeadline &&
                       (eventHub.AcknowledgedSequence < 1 || cursorSequence < 1))
                {
                    if (File.Exists(cursorPath))
                    {
                        using var cursor = JsonDocument.Parse(await File.ReadAllTextAsync(cursorPath, cancellation.Token));
                        cursorSequence = cursor.RootElement.GetProperty("sequence").GetInt64();
                    }
                    if (eventHub.AcknowledgedSequence < 1 || cursorSequence < 1)
                        await Task.Delay(TimeSpan.FromMilliseconds(10), cancellation.Token);
                }
                Assert.Equal(1, eventHub.AcknowledgedSequence);
                Assert.Equal(1, cursorSequence);
            }

            await coordinator.WaitAsync(TimeSpan.FromSeconds(3), cancellation.Token);
            Assert.True(eventHub.AcknowledgedSequence == 1,
                "Publisher did not apply the ACK. Transport errors: " + string.Join(" | ", errors.Select(error => error.ToString())));
            using var cursorDocument = JsonDocument.Parse(await File.ReadAllTextAsync(cursorPath, cancellation.Token));
            Assert.Equal(1, cursorDocument.RootElement.GetProperty("sequence").GetInt64());
            Assert.Equal(0, eventHub.ReadAfter(1).Events.Count);
        }
        finally
        {
            cancellation.Cancel();
            eventHub.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<(ClusterEnvelope Envelope, QuicStream Stream)> ReceiveAsync(QuicConnection connection,
        CancellationToken token)
    {
        var stream = await connection.AcceptInboundStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(chunk, token)) != 0) buffer.Write(chunk, 0, read);
        var envelope = MessagePackSerializer.Deserialize<ClusterEnvelope>(buffer.ToArray());
        ClusterEnvelopeValidator.Validate(envelope);
        return (envelope, stream);
    }

    private static async Task RespondAsync<T>(QuicStream stream, ClusterEnvelope request,
        ClusterMessageType type, T payload, CancellationToken token)
    {
        var bytes = MessagePackSerializer.Serialize(payload);
        var envelope = new ClusterEnvelope
        {
            MessageType = (ushort)type,
            Flags = ClusterFrameFlags.Response,
            CorrelationId = request.CorrelationId,
            Sequence = request.Sequence,
            PayloadLength = checked((uint)bytes.Length),
            Payload = bytes
        };
        await stream.WriteAsync(MessagePackSerializer.Serialize(envelope), token);
        stream.CompleteWrites();
    }

    private static X509Certificate2 CreateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=LLServer test CA", key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign |
            X509KeyUsageFlags.CrlSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 authority, string dnsName, string usage)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={dnsName}", key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature |
            X509KeyUsageFlags.KeyEncipherment, false));
        var usages = new OidCollection();
        usages.Add(new Oid(usage));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(dnsName);
        request.CertificateExtensions.Add(names.Build());
        return request.Create(authority, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(50),
            RandomNumberGenerator.GetBytes(16)).CopyWithPrivateKey(key);
    }

    private static bool ChainsTo(X509Certificate2 certificate, X509Certificate2 authority)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(certificate);
    }

    private static bool HasDnsSan(X509Certificate2 certificate, string expected)
    {
        var san = certificate.Extensions.FirstOrDefault(value => value.Oid?.Value == "2.5.29.17");
        var names = san is null ? [] : new X509SubjectAlternativeNameExtension(san.RawData, san.Critical)
            .EnumerateDnsNames().ToArray();
        return names.Length == 1 && names[0] == expected;
    }
}
#endif
