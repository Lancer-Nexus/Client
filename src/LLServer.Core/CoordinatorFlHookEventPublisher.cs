#if LANCER_NEXUS_FLHOOKCOMPAT
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LancerNexus.FlhookCompat;
using LancerNexus.FlhookCompat.Hosting;
using LancerNexus.Protocol;
using LibreLancer;
using MessagePack;
using ClusterProtocolViolationException = LancerNexus.Protocol.ProtocolViolationException;

namespace LLServer;

/// <summary>Retries the local bounded event history over the authenticated Coordinator QUIC control channel.</summary>
[SupportedOSPlatform("linux")]
internal sealed class CoordinatorFlHookEventPublisher : IAsyncDisposable
{
    private const int MaximumSerializedEnvelopeLength = checked((int)ClusterEnvelopeValidator.MaxPayloadLength + 16 * 1024);
    private static readonly SslApplicationProtocol Alpn = new("lancer-nexus-control/1");
    private static readonly MessagePackSerializerOptions UntrustedMessagePack =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FlHookCompatServerEventHub events;
    private readonly Settings settings;
    private readonly X509Certificate2 clientCertificate;
    private readonly X509Certificate2 coordinatorCaCertificate;
    private readonly CancellationTokenSource cancellation = new();
    private Task? runner;

    public CoordinatorFlHookEventPublisher(FlHookCompatServerEventHub events, Settings settings)
    {
        this.events = events;
        this.settings = settings;
        clientCertificate = X509CertificateLoader.LoadPkcs12FromFile(settings.ClientCertificatePath,
            settings.ClientCertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
        coordinatorCaCertificate = X509CertificateLoader.LoadCertificateFromFile(settings.CoordinatorCaCertificatePath);
        ValidateClientCertificate(clientCertificate, settings.NodeId);
    }

    public void Start()
    {
        if (runner is not null)
            throw new InvalidOperationException("Coordinator FLHook event publisher is already running.");
        runner = Task.Run(() => RunAsync(cancellation.Token));
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        if (runner is not null)
        {
            try { await runner.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            runner = null;
        }
        clientCertificate.Dispose();
        coordinatorCaCertificate.Dispose();
        cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var retryDelay = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            var sessionStartedAt = DateTimeOffset.UtcNow;
            try
            {
                await RunSessionAsync(cancellationToken).ConfigureAwait(false);
                retryDelay = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                FLLog.Warning("FLHookCompat", $"Coordinator event stream unavailable: {exception.GetType().Name}: {exception.Message}");
                try { settings.ConnectionErrorObserver?.Invoke(exception); }
                catch (Exception observerException)
                {
                    FLLog.Warning("FLHookCompat", $"Coordinator event error observer failed: {observerException.GetType().Name}");
                }
                if (DateTimeOffset.UtcNow - sessionStartedAt >= TimeSpan.FromMinutes(1))
                    retryDelay = TimeSpan.FromSeconds(1);
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 30));
            }
        }
    }

    private async Task RunSessionAsync(CancellationToken cancellationToken)
    {
        var options = new QuicClientConnectionOptions
        {
            RemoteEndPoint = settings.CoordinatorEndPoint,
            DefaultStreamErrorCode = 0x100,
            DefaultCloseErrorCode = 0x101,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                TargetHost = settings.CoordinatorServerName,
                ApplicationProtocols = [Alpn],
                EnabledSslProtocols = SslProtocols.Tls13,
                ClientCertificates = [clientCertificate],
                RemoteCertificateValidationCallback = ValidateCoordinatorCertificate
            }
        };

        await using var connection = await QuicConnection.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        await ExchangeHelloAsync(connection, cancellationToken).ConfigureAwait(false);
        FLLog.Info("FLHookCompat", $"Connected authenticated Coordinator event stream for {settings.InstanceId}.");

        var cursor = await LoadCursorAsync(cancellationToken).ConfigureAwait(false);
        var currentEpoch = events.ProducerEpoch;
        var acknowledgedSequence = events.AcknowledgedSequence;
        if (cursor is null || cursor.ProducerEpoch != currentEpoch || cursor.Sequence < acknowledgedSequence)
        {
            cursor = new Cursor(settings.InstanceId, currentEpoch, acknowledgedSequence);
            await SaveCursorAsync(cursor, cancellationToken).ConfigureAwait(false);
        }
        while (!cancellationToken.IsCancellationRequested)
        {
            var replay = events.ReadAfter(cursor?.Sequence ?? 0, 500, cursor?.ProducerEpoch);

            if (replay.HasGap)
                FLLog.Warning("FLHookCompat", $"Local event replay has a gap for {settings.InstanceId}: cursor {cursor?.Sequence ?? 0}, oldest {replay.OldestAvailableSequence}.");

            if (replay.Events.Count == 0)
            {
                cursor ??= new Cursor(settings.InstanceId, replay.ProducerEpoch, 0);
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
                continue;
            }

            foreach (var value in replay.Events)
            {
                var wireEvent = ToProtocolEvent(value);
                var acknowledgement = await SendEventAsync(connection, wireEvent, cancellationToken).ConfigureAwait(false);
                if (acknowledgement.Status is not (FlHookEventIngestStatusV1.Stored or FlHookEventIngestStatusV1.Duplicate))
                    throw new IOException($"Coordinator did not durably accept event {wireEvent.InstanceId}/{wireEvent.ProducerEpoch}/{wireEvent.Sequence}: {acknowledgement.Status}.");

                events.Acknowledge(wireEvent.ProducerEpoch, wireEvent.Sequence);
                cursor = new Cursor(settings.InstanceId, wireEvent.ProducerEpoch, wireEvent.Sequence);
                await SaveCursorAsync(cursor, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ExchangeHelloAsync(QuicConnection connection, CancellationToken cancellationToken)
    {
        var hello = new ClusterHello
        {
            NodeId = settings.NodeId,
            InstanceId = settings.InstanceId,
            BuildVersion = typeof(CoordinatorFlHookEventPublisher).Assembly.GetName().Version?.ToString() ?? "unknown",
            Capabilities = ["cluster_handshake_v1", ClusterCapabilities.FlHookEventsV1]
        };
        var request = CreateEnvelope(ClusterMessageType.Hello, ClusterFrameFlags.Request, 1,
            Guid.NewGuid(), MessagePackSerializer.Serialize(hello));
        var response = await ExchangeAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (response.MessageType != (ushort)ClusterMessageType.Hello ||
            response.CorrelationId != request.CorrelationId || response.Sequence != request.Sequence ||
            response.Flags is not (ClusterFrameFlags.Response or (ClusterFrameFlags.Response | ClusterFrameFlags.Error)))
            throw new ClusterProtocolViolationException("Coordinator returned an invalid cluster Hello response.");

        var result = MessagePackSerializer.Deserialize<ClusterHandshakeResponse>(response.Payload, UntrustedMessagePack);
        if (!result.Accepted || !result.NegotiatedCapabilities.Contains(ClusterCapabilities.FlHookEventsV1, StringComparer.Ordinal))
            throw new InvalidOperationException($"Coordinator did not negotiate FLHook event publishing: {result.ReasonCode}.");
    }

    private async Task<FlHookEventIngestResultV1> SendEventAsync(QuicConnection connection, FlHookEventV1 value,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var request = CreateEnvelope(ClusterMessageType.FlHookEvent, ClusterFrameFlags.Request,
            checked((ulong)value.Sequence), correlationId, MessagePackSerializer.Serialize(value));
        var response = await ExchangeAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (response.MessageType != (ushort)ClusterMessageType.FlHookEventIngestResult ||
            response.CorrelationId != correlationId || response.Sequence != request.Sequence ||
            response.Flags is not (ClusterFrameFlags.Response or (ClusterFrameFlags.Response | ClusterFrameFlags.Error)))
            throw new ClusterProtocolViolationException("Coordinator returned an invalid FLHook event acknowledgement frame.");

        var result = MessagePackSerializer.Deserialize<FlHookEventIngestResultV1>(response.Payload, UntrustedMessagePack);
        if (!result.IsValid() || result.CorrelationId != correlationId || result.InstanceId != value.InstanceId ||
            result.ProducerEpoch != value.ProducerEpoch || result.Sequence != value.Sequence)
            throw new ClusterProtocolViolationException("Coordinator acknowledgement identity does not match the submitted event.");
        return result;
    }

    private static async Task<ClusterEnvelope> ExchangeAsync(QuicConnection connection, ClusterEnvelope request,
        CancellationToken cancellationToken)
    {
        await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(MessagePackSerializer.Serialize(request), cancellationToken).ConfigureAwait(false);
        stream.CompleteWrites();
        var responseBytes = await ReadToEndAsync(stream, cancellationToken).ConfigureAwait(false);
        var response = MessagePackSerializer.Deserialize<ClusterEnvelope>(responseBytes, UntrustedMessagePack);
        ClusterEnvelopeValidator.Validate(response);
        return response;
    }

    private static async Task<byte[]> ReadToEndAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
            {
                if (buffer.Length + read > MaximumSerializedEnvelopeLength)
                    throw new ClusterProtocolViolationException("Coordinator response exceeds the cluster frame limit.");
                buffer.Write(chunk, 0, read);
            }
            if (buffer.Length == 0)
                throw new ClusterProtocolViolationException("Coordinator returned an empty cluster response.");
            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    private static ClusterEnvelope CreateEnvelope(ClusterMessageType messageType, ClusterFrameFlags flags,
        ulong sequence, Guid correlationId, byte[] payload) => new()
    {
        MessageType = (ushort)messageType,
        Flags = flags,
        CorrelationId = correlationId,
        Sequence = sequence,
        PayloadLength = checked((uint)payload.Length),
        Payload = payload
    };

    private static FlHookEventV1 ToProtocolEvent(FlHookCompatServerEvent value) => new()
    {
        InstanceId = value.InstanceId,
        Sequence = value.Sequence,
        OccurredUtc = value.OccurredUtc.ToUniversalTime(),
        Kind = value.Kind switch
        {
            FlHookCompatServerEventKind.PlayerConnected => FlHookEventKind.PlayerConnected,
            FlHookCompatServerEventKind.PlayerDisconnected => FlHookEventKind.PlayerDisconnected,
            FlHookCompatServerEventKind.CharacterConnected => FlHookEventKind.CharacterConnected,
            FlHookCompatServerEventKind.CharacterDisconnected => FlHookEventKind.CharacterDisconnected,
            FlHookCompatServerEventKind.PlayerSystemChanged => FlHookEventKind.PlayerSystemChanged,
            _ => throw new ClusterProtocolViolationException("Local event kind is unsupported by the cluster contract.")
        },
        PlayerId = value.PlayerId,
        SystemId = value.SystemId,
        CharacterId = value.CharacterId,
        ProducerEpoch = value.ProducerEpoch
    };

    private async Task<Cursor?> LoadCursorAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.CursorPath)) return null;
        await using var stream = new FileStream(settings.CursorPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var cursor = await JsonSerializer.DeserializeAsync<Cursor>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("FLHook event cursor file is empty.");
        if (cursor.InstanceId != settings.InstanceId || cursor.ProducerEpoch == Guid.Empty || cursor.Sequence < 0)
            throw new InvalidDataException("FLHook event cursor file does not match the configured instance.");
        return cursor;
    }

    private async Task SaveCursorAsync(Cursor cursor, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings.CursorPath)!);
        var temporaryPath = settings.CursorPath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, cursor, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(true);
        }
        File.Move(temporaryPath, settings.CursorPath, overwrite: true);
    }

    private bool ValidateCoordinatorCertificate(object sender, X509Certificate? certificate,
        X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is not X509Certificate2 peer ||
            (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0 ||
            !HasEnhancedKeyUsage(peer, "1.3.6.1.5.5.7.3.1"))
            return false;

        using var validationChain = new X509Chain();
        validationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        validationChain.ChainPolicy.CustomTrustStore.Add(coordinatorCaCertificate);
        validationChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return validationChain.Build(peer);
    }

    private static void ValidateClientCertificate(X509Certificate2 certificate, string nodeId)
    {
        var now = DateTime.UtcNow;
        var sanExtension = certificate.Extensions.FirstOrDefault(extension => extension.Oid?.Value == "2.5.29.17");
        var san = sanExtension is null ? null : new X509SubjectAlternativeNameExtension(sanExtension.RawData, sanExtension.Critical);
        var dnsNames = san?.EnumerateDnsNames().ToArray() ?? [];
        if (!certificate.HasPrivateKey || certificate.NotBefore.ToUniversalTime() > now ||
            certificate.NotAfter.ToUniversalTime() <= now || !HasEnhancedKeyUsage(certificate, "1.3.6.1.5.5.7.3.2") ||
            dnsNames.Length != 1 || dnsNames[0].Contains('*', StringComparison.Ordinal) ||
            !string.Equals(dnsNames[0], nodeId, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("Coordinator event client certificate must have Client Authentication EKU and exactly one DNS SAN matching the configured Agent node ID.");
    }

    private static bool HasEnhancedKeyUsage(X509Certificate2 certificate, string expectedOid) => certificate.Extensions
        .Where(extension => extension.Oid?.Value == "2.5.29.37")
        .Select(extension => new X509EnhancedKeyUsageExtension(extension, extension.Critical))
        .Any(extension => extension.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>()
            .Any(oid => oid.Value == expectedOid));

    internal sealed record Settings(IPEndPoint CoordinatorEndPoint, string CoordinatorServerName, string NodeId,
        string InstanceId, string ClientCertificatePath, string? ClientCertificatePassword,
        string CoordinatorCaCertificatePath, string CursorPath, Action<Exception>? ConnectionErrorObserver = null);

    private sealed record Cursor(string InstanceId, Guid ProducerEpoch, long Sequence);
}
#endif
