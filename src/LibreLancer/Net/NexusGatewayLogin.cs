using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LancerNexus.Protocol;

namespace LibreLancer.Net;

public sealed class ClientVersionMetadataException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed record NexusGatewayLoginResult(
    ClientVersionDecision VersionDecision, string? AccessToken, string? RefreshToken,
    Guid? SessionId, string? GameEndpoint = null, string? JoinTicket = null,
    string? InstanceId = null, string? SystemId = null)
{
    public bool LoggedIn => AccessToken is not null && SessionId.HasValue;
    public bool Assigned => GameEndpoint is not null && JoinTicket is not null;
    public int? RequestedExitCode => VersionDecision.Status is
        ClientVersionStatus.UpdateRequired or ClientVersionStatus.ProtocolUnsupported ? 42 : null;
}

public sealed record NexusGatewayRefreshResult(
    string AccessToken, string RefreshToken, Guid SessionId, DateTime ExpiresAtUtc);

public sealed class NexusGatewayLogin : IDisposable
{
    private readonly HttpClient http;
    private readonly Uri gateway;
    private readonly string installDirectory;

    public NexusGatewayLogin(Uri gateway)
        : this(gateway, AppContext.BaseDirectory, new HttpClientHandler { AllowAutoRedirect = false })
    {
    }

    internal NexusGatewayLogin(Uri gateway, string installDirectory, HttpMessageHandler handler)
    {
        if (!gateway.IsAbsoluteUri || gateway.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(gateway.UserInfo) || !string.IsNullOrEmpty(gateway.Query) ||
            !string.IsNullOrEmpty(gateway.Fragment))
            throw new ArgumentException("Gateway endpoint must be an HTTPS origin.", nameof(gateway));
        this.gateway = gateway;
        this.installDirectory = installDirectory;
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    }

    public Task<NexusGatewayLoginResult> LoginAsync(
        string email, string password, CancellationToken cancellationToken = default) =>
        LoginWithHelloAsync(LoadInstalledHello(), email, password, cancellationToken);

    private async Task<NexusGatewayLoginResult> LoginWithHelloAsync(
        ClientVersionHello hello, string email, string password, CancellationToken cancellationToken)
    {
        using var versionResponse = await http.PostAsJsonAsync(
            new Uri(gateway, "/api/v1/client/version"), hello, cancellationToken);
        versionResponse.EnsureSuccessStatusCode();
        var decision = await versionResponse.Content.ReadFromJsonAsync<ClientVersionDecision>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Gateway version response is empty.");
        if (!decision.SessionAllowed)
        {
            if (decision.Status is not (ClientVersionStatus.UpdateRequired or ClientVersionStatus.ProtocolUnsupported))
                throw new InvalidDataException("Gateway version response is contradictory.");
            return new NexusGatewayLoginResult(decision, null, null, null);
        }
        if (decision.Status is not (ClientVersionStatus.Supported or ClientVersionStatus.UpdateRecommended) ||
            string.IsNullOrWhiteSpace(decision.HandshakeToken))
            throw new InvalidDataException("Gateway version response has no valid handshake proof.");

        using var loginResponse = await http.PostAsJsonAsync(
            new Uri(gateway, "/api/v1/auth/login"),
            new GatewayCredentials(email, password, decision.HandshakeToken), cancellationToken);
        if (loginResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("Gateway credentials were rejected.");
        if (!loginResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Gateway login failed with HTTP {(int)loginResponse.StatusCode}.");
        var login = await loginResponse.Content.ReadFromJsonAsync<GatewayLoginResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Gateway login response is empty.");
        if (string.IsNullOrWhiteSpace(login.AccessToken) || login.SessionId == Guid.Empty)
            throw new InvalidDataException("Gateway login response is incomplete.");
        return new NexusGatewayLoginResult(decision, login.AccessToken, login.RefreshToken, login.SessionId);
    }

    public async Task<NexusGatewayLoginResult> LoginAndPlaceAsync(
        string email, string password, string targetSystem, string region,
        CancellationToken cancellationToken = default)
    {
        var hello = LoadInstalledHello();
        var login = await LoginWithHelloAsync(hello, email, password, cancellationToken);
        if (!login.LoggedIn)
            return login;
        if (string.IsNullOrWhiteSpace(targetSystem))
            throw new ArgumentException("Target system is required.", nameof(targetSystem));
        var requestId = Guid.NewGuid();
        var placement = new PlacementRequest
        {
            RequestId = requestId,
            SessionId = login.SessionId!.Value,
            TargetSystem = targetSystem,
            ClientBuild = hello.BuildId,
            Region = region,
            IdempotencyKey = requestId.ToString("N")
        };
        using var message = new HttpRequestMessage(HttpMethod.Post,
            new Uri(gateway, "/api/v1/placement/request"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        message.Content = JsonContent.Create(placement);
        using var response = await http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gateway placement failed with HTTP {(int)response.StatusCode}.");
        var envelope = await response.Content.ReadFromJsonAsync<GatewayPlacementEnvelope>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Gateway placement response is empty.");
        var decision = envelope.Decision;
        if (decision is null || decision.RequestId != requestId || !decision.Accepted ||
            string.IsNullOrWhiteSpace(decision.Endpoint) || string.IsNullOrWhiteSpace(decision.JoinTicket) ||
            string.IsNullOrWhiteSpace(decision.InstanceId) || string.IsNullOrWhiteSpace(decision.SystemId) ||
            decision.ExpiresUtc.Kind != DateTimeKind.Utc || decision.ExpiresUtc <= DateTime.UtcNow)
            throw new InvalidDataException("Gateway placement response is invalid.");
        return login with
        {
            GameEndpoint = decision.Endpoint,
            JoinTicket = decision.JoinTicket,
            InstanceId = decision.InstanceId,
            SystemId = decision.SystemId
        };
    }

    /// <summary>Requests a target reservation for a transfer that the source server has approved for preparation.</summary>
    public async Task<TransferStartResult> StartTransferAsync(
        string accessToken, TransferStartRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Gateway access token is required.", nameof(accessToken));
        if (request.TransferId == Guid.Empty || request.SessionId == Guid.Empty || request.CharacterId <= 0 ||
            request.TargetInstanceId is { Length: > 96 } || string.IsNullOrWhiteSpace(request.TargetSystemId) ||
            request.ExpiresUtc.Kind != DateTimeKind.Utc || request.ExpiresUtc <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ArgumentException("Transfer request is incomplete or expired.", nameof(request));

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(gateway, "/api/v1/transfers/start"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string? reasonCode = null;
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                using var document = JsonDocument.Parse(errorBody);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.String)
                    reasonCode = error.GetString();
            }
            catch (JsonException) { }
            var reason = string.IsNullOrWhiteSpace(reasonCode) ? "" : $" ({reasonCode})";
            throw new HttpRequestException(
                $"Gateway transfer preparation failed with HTTP {(int)response.StatusCode}{reason}.");
        }
        var result = await response.Content.ReadFromJsonAsync<TransferStartResult>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Gateway transfer response is empty.");
        var prepared = result.Prepared;
        if (prepared is null || prepared.TransferId != request.TransferId || !prepared.Accepted ||
            string.IsNullOrWhiteSpace(prepared.TransferTicket) || prepared.ExpiresUtc.Kind != DateTimeKind.Utc ||
            prepared.ExpiresUtc <= DateTime.UtcNow || prepared.ExpiresUtc > request.ExpiresUtc ||
            string.IsNullOrWhiteSpace(result.SourceInstanceId) || string.IsNullOrWhiteSpace(result.TargetEndpoint) ||
            string.IsNullOrWhiteSpace(result.TargetInstanceId) ||
            !string.Equals(result.TargetSystemId, request.TargetSystemId, StringComparison.Ordinal) || result.LeaseVersion < 0)
            throw new InvalidDataException("Gateway transfer response is invalid.");
        return result;
    }

    public async Task<NexusGatewayRefreshResult> RefreshAsync(
        Guid sessionId, string refreshToken, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty || string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Gateway refresh session and token are required.");
        using var response = await http.PostAsJsonAsync(
            new Uri(gateway, "/api/v1/auth/refresh"),
            new GatewayRefreshRequest(sessionId, refreshToken), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("Gateway session expired or refresh token was rejected.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gateway token refresh failed with HTTP {(int)response.StatusCode}.");
        var login = await response.Content.ReadFromJsonAsync<GatewayLoginResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Gateway refresh response is empty.");
        if (string.IsNullOrWhiteSpace(login.AccessToken) || string.IsNullOrWhiteSpace(login.RefreshToken) ||
            login.SessionId != sessionId || login.ExpiresAtUtc.Kind != DateTimeKind.Utc ||
            login.ExpiresAtUtc <= DateTime.UtcNow)
            throw new InvalidDataException("Gateway refresh response is invalid.");
        return new NexusGatewayRefreshResult(login.AccessToken, login.RefreshToken, sessionId, login.ExpiresAtUtc);
    }

    internal ClientVersionHello LoadInstalledHello()
    {
        try
        {
            var path = Path.Combine(installDirectory, "client-version.json");
            using var stream = File.OpenRead(path);
            if (stream.Length is 0 or > 16_384)
                throw new InvalidDataException("Installed client version metadata has an invalid size.");
            var hello = JsonSerializer.Deserialize<ClientVersionHello>(stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidDataException("Installed client version metadata is empty.");
            if (string.IsNullOrWhiteSpace(hello.ClientVersion) || string.IsNullOrWhiteSpace(hello.BuildId) ||
                hello.ProtocolVersion < 1 || string.IsNullOrWhiteSpace(hello.DataManifestId) ||
                string.IsNullOrWhiteSpace(hello.Platform) || string.IsNullOrWhiteSpace(hello.Channel) ||
                hello.Capabilities is null)
                throw new InvalidDataException("Installed client version metadata is incomplete.");
            return hello;
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new ClientVersionMetadataException("Installed client version metadata requires repair.", error);
        }
    }

    public void Dispose() => http.Dispose();

    private sealed record GatewayCredentials(string Email, string Password, string HandshakeToken);
    private sealed record GatewayRefreshRequest(Guid SessionId, string RefreshToken);
    private sealed record GatewayLoginResponse(
        string AccessToken, string RefreshToken, Guid AccountId, Guid SessionId, DateTime ExpiresAtUtc);
    private sealed record GatewayPlacementEnvelope(PlacementDecision Decision, bool Duplicate);
}
