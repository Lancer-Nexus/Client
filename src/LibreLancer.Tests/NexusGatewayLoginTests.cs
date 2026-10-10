using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LancerNexus.Protocol;
using LibreLancer.Net;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NexusGatewayLoginTests
{
    [Fact]
    public async Task SupportedVersionIsSentBeforeCredentials()
    {
        var folder = CreateMetadata();
        try
        {
            var handler = new FakeHandler(false);
            using var client = new NexusGatewayLogin(new Uri("https://gateway.example.test"), folder, handler);
            var result = await client.LoginAsync("pilot@example.net", "secret",
                TestContext.Current.CancellationToken);

            Assert.True(result.LoggedIn);
            Assert.Equal(2, handler.Requests.Count);
            Assert.Equal("/api/v1/client/version", handler.Requests[0].Path);
            Assert.Equal("/api/v1/auth/login", handler.Requests[1].Path);
            Assert.DoesNotContain("secret", handler.Requests[0].Body);
            Assert.Contains("proof", handler.Requests[1].Body);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task MissingInstalledMetadataStopsBeforeNetworkAndCredentials()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"nexus-gateway-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var handler = new FakeHandler(false);
            using var client = new NexusGatewayLogin(new Uri("https://gateway.example.test"), folder, handler);
            await Assert.ThrowsAsync<ClientVersionMetadataException>(() =>
                client.LoginAsync("pilot@example.net", "secret", TestContext.Current.CancellationToken));
            Assert.Empty(handler.Requests);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task PlacementUsesSessionAfterHandshakeAndLogin()
    {
        var folder = CreateMetadata();
        try
        {
            var handler = new FakeHandler(false);
            using var client = new NexusGatewayLogin(new Uri("https://gateway.example.test"), folder, handler);
            var result = await client.LoginAndPlaceAsync("pilot@example.net", "secret",
                "li01", "eu", TestContext.Current.CancellationToken);

            Assert.True(result.Assigned);
            Assert.Equal("game.example.test:2300", result.GameEndpoint);
            Assert.Equal("join-ticket", result.JoinTicket);
            Assert.Equal("instance-eu-01", result.InstanceId);
            Assert.Equal("li01", result.SystemId);
            Assert.Equal(3, handler.Requests.Count);
            Assert.Equal("/api/v1/placement/request", handler.Requests[2].Path);
            Assert.Contains("\"targetSystem\":\"li01\"", handler.Requests[2].Body);
            Assert.Equal("Bearer access", handler.PlacementAuthorization);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task TransferStartUsesBearerSessionAndValidatesPreparedTicket()
    {
        var folder = CreateMetadata();
        try
        {
            var handler = new FakeHandler(false);
            using var client = new NexusGatewayLogin(new Uri("https://gateway.example.test"), folder, handler);
            var request = new TransferStartRequest
            {
                TransferId = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CharacterId = 42,
                TargetInstanceId = "instance-eu-02",
                TargetSystemId = "li02",
                ExpiresUtc = DateTime.UtcNow.AddMinutes(1),
                IdempotencyKey = Guid.NewGuid().ToString("N")
            };

            var result = await client.StartTransferAsync("access", request, TestContext.Current.CancellationToken);

            Assert.True(result.Prepared.Accepted);
            Assert.Equal(request.TransferId, result.Prepared.TransferId);
            Assert.Equal("transfer-ticket", result.Prepared.TransferTicket);
            Assert.Equal("li02", result.TargetSystemId);
            Assert.Equal("Bearer access", handler.TransferAuthorization);
            Assert.Equal("/api/v1/transfers/start", handler.Requests[^1].Path);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task TransferStartRejectsResponseForDifferentTargetSystem()
    {
        var folder = CreateMetadata();
        try
        {
            var handler = new FakeHandler(false, mismatchedTransferTarget: true);
            using var client = new NexusGatewayLogin(new Uri("https://gateway.example.test"), folder, handler);
            var request = new TransferStartRequest
            {
                TransferId = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CharacterId = 42,
                TargetInstanceId = "instance-eu-02",
                TargetSystemId = "li02",
                ExpiresUtc = DateTime.UtcNow.AddMinutes(1),
                IdempotencyKey = Guid.NewGuid().ToString("N")
            };

            await Assert.ThrowsAsync<InvalidDataException>(() => client.StartTransferAsync(
                "access", request, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task RefreshRotatesTokensForTheSameSession()
    {
        var folder = CreateMetadata();
        try
        {
            var sessionId = Guid.NewGuid();
            var handler = new FakeHandler(false);
            using var client = new NexusGatewayLogin(new Uri("https://gateway.example.test"), folder, handler);
            var result = await client.RefreshAsync(sessionId, "old-refresh-token", TestContext.Current.CancellationToken);

            Assert.Equal(sessionId, result.SessionId);
            Assert.Equal("new-access", result.AccessToken);
            Assert.Equal("new-refresh", result.RefreshToken);
            Assert.Equal("/api/v1/auth/refresh", handler.Requests[^1].Path);
            Assert.Contains(sessionId.ToString(), handler.Requests[^1].Body);
            Assert.Contains("old-refresh-token", handler.Requests[^1].Body);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task RequiredUpdateStopsBeforeSendingCredentials()
    {
        var folder = CreateMetadata();
        try
        {
            var handler = new FakeHandler(true);
            using var client = new NexusGatewayLogin(new Uri("https://gateway.example.test"), folder, handler);
            var result = await client.LoginAsync("pilot@example.net", "secret",
                TestContext.Current.CancellationToken);

            Assert.False(result.LoggedIn);
            Assert.Equal(42, result.RequestedExitCode);
            Assert.Single(handler.Requests);
            Assert.DoesNotContain("secret", handler.Requests[0].Body);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static string CreateMetadata()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"nexus-gateway-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "client-version.json"),
            """{"clientVersion":"1.0.0","buildId":"20260923.1","protocolVersion":1,"dataManifestId":"data-2026-09-22","platform":"linux-x64","channel":"stable","capabilities":["transfer-v1"]}""");
        return folder;
    }

    private sealed class FakeHandler(bool updateRequired, bool mismatchedTransferTarget = false) : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];
        public string? PlacementAuthorization { get; private set; }
        public string? TransferAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.AbsolutePath, body));
            var payload = request.RequestUri.AbsolutePath == "/api/v1/client/version"
                ? updateRequired
                    ? """{"status":"update_required","sessionAllowed":false,"serverProtocolVersion":1,"messageKey":"client_update_required"}"""
                    : """{"status":"supported","sessionAllowed":true,"serverProtocolVersion":1,"messageKey":"client_version_supported","handshakeToken":"proof"}"""
                : request.RequestUri.AbsolutePath == "/api/v1/placement/request"
                    ? PlacementPayload(request, body)
                : request.RequestUri.AbsolutePath == "/api/v1/transfers/start"
                    ? TransferPayload(request, body)
                    : request.RequestUri.AbsolutePath == "/api/v1/auth/refresh"
                    ? RefreshPayload(body)
                : JsonSerializer.Serialize(new
                {
                    accessToken = "access", refreshToken = "refresh",
                    accountId = Guid.NewGuid(), sessionId = Guid.NewGuid(),
                    expiresAtUtc = DateTime.UtcNow.AddMinutes(10)
                });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }

        private string PlacementPayload(HttpRequestMessage request, string body)
        {
            PlacementAuthorization = request.Headers.Authorization?.ToString();
            var requestId = JsonDocument.Parse(body).RootElement.GetProperty("requestId").GetGuid();
            return JsonSerializer.Serialize(new
            {
                decision = new
                {
                    requestId, accepted = true, instanceId = "instance-eu-01", systemId = "li01",
                    endpoint = "game.example.test:2300",
                    joinTicket = "join-ticket", expiresUtc = DateTime.UtcNow.AddMinutes(2)
                },
                duplicate = false
            });
        }

        private string TransferPayload(HttpRequestMessage request, string body)
        {
            TransferAuthorization = request.Headers.Authorization?.ToString();
            var transferId = JsonDocument.Parse(body).RootElement.GetProperty("transferId").GetGuid();
            return JsonSerializer.Serialize(new
            {
                prepared = new
                {
                    transferId, accepted = true, transferTicket = "transfer-ticket",
                    expiresUtc = DateTime.UtcNow.AddSeconds(30), reasonCode = "prepared"
                },
                sourceInstanceId = "instance-eu-01",
                targetEndpoint = "game-2.example.test:2300",
                targetInstanceId = "instance-eu-02",
                targetSystemId = mismatchedTransferTarget ? "li99" : "li02",
                leaseVersion = 5,
                duplicate = false
            });
        }

        private static string RefreshPayload(string body)
        {
            var sessionId = JsonDocument.Parse(body).RootElement.GetProperty("sessionId").GetGuid();
            return JsonSerializer.Serialize(new
            {
                accessToken = "new-access", refreshToken = "new-refresh",
                accountId = Guid.NewGuid(), sessionId, expiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });
        }
    }
}
