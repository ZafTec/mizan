using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Push;

/// <summary>
/// Sends push notifications through Firebase Cloud Messaging (HTTP v1). It signs its own
/// service-account token, so no Google SDK is needed. Unconfigured, it reports so and the
/// rest of the app carries on without push. The project and key are read per send, so a change in
/// the admin area applies without a restart; the access token is kept only for the key it was
/// signed with.
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    public const string HttpClientName = "fcm";
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<FcmPushSender> _logger;
    private readonly IRuntimeOptions<PushOptions> _settings;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private (string Json, ServiceAccount? Account) _parsed = (string.Empty, null);
    private ServiceAccount? _tokenAccount;
    private string? _accessToken;
    private DateTime _accessTokenExpires;

    public FcmPushSender(IHttpClientFactory http, IRuntimeOptions<PushOptions> settings, ILogger<FcmPushSender> logger)
    {
        _http = http;
        _logger = logger;
        _settings = settings;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) =>
        await CurrentAsync(cancellationToken) is not null;

    /// <summary>The project and parsed key as the settings stand now, or null when push is off.</summary>
    private async Task<(string ProjectId, ServiceAccount Account)?> CurrentAsync(CancellationToken cancellationToken)
    {
        var options = await _settings.GetAsync(cancellationToken);
        var parsed = _parsed;
        if (!string.Equals(parsed.Json, options.FcmServiceAccountJson, StringComparison.Ordinal))
        {
            parsed = (options.FcmServiceAccountJson, ServiceAccount.TryParse(options.FcmServiceAccountJson));
            _parsed = parsed;
        }

        return parsed.Account is null || string.IsNullOrWhiteSpace(options.FcmProjectId)
            ? null
            : (options.FcmProjectId, parsed.Account);
    }

    public async Task<PushOutcome> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        if (await CurrentAsync(cancellationToken) is not { } current) return PushOutcome.Failed;
        var projectId = current.ProjectId;

        string token;
        try { token = await AccessTokenAsync(current.Account, cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or CryptographicException)
        {
            _logger.LogWarning(ex, "Could not get a Firebase access token");
            return PushOutcome.Failed;
        }

        var data = new Dictionary<string, string>();
        if (message.LinkUrl is not null) data["linkUrl"] = message.LinkUrl;
        if (message.NotificationId is not null) data["notificationId"] = message.NotificationId;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send")
        {
            Content = JsonContent.Create(new
            {
                message = new
                {
                    token = message.Token,
                    notification = new { title = message.Title, body = message.Body },
                    data,
                    android = new { priority = "high" },
                },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try { response = await _http.CreateClient(HttpClientName).SendAsync(request, cancellationToken); }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Firebase could not be reached");
            return PushOutcome.Failed;
        }

        using (response)
        {
            if (response.IsSuccessStatusCode) return PushOutcome.Sent;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized) _accessToken = null;

            var outcome = Classify(response.StatusCode, body);
            if (outcome == PushOutcome.Failed)
                _logger.LogWarning("Firebase refused a push with {Status}", (int)response.StatusCode);
            return outcome;
        }
    }

    /// <summary>Which failures mean the device is gone and which are worth another try.</summary>
    public static PushOutcome Classify(HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.NotFound) return PushOutcome.TokenInvalid;

        try
        {
            using var document = JsonDocument.Parse(body);
            var error = document.RootElement.GetProperty("error");
            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array
                && details.EnumerateArray().Any(d => d.TryGetProperty("errorCode", out var code) && code.GetString() == "UNREGISTERED"))
                return PushOutcome.TokenInvalid;

            // A 400 about the token itself, as opposed to a problem with our message.
            if (status == HttpStatusCode.BadRequest
                && error.TryGetProperty("message", out var text)
                && (text.GetString() ?? string.Empty).Contains("registration token", StringComparison.OrdinalIgnoreCase))
                return PushOutcome.TokenInvalid;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // An answer we cannot read is treated as a failure to retry.
        }

        return PushOutcome.Failed;
    }

    private async Task<string> AccessTokenAsync(ServiceAccount account, CancellationToken ct)
    {
        if (Fresh(account)) return _accessToken!;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (Fresh(account)) return _accessToken!;

            var now = DateTimeOffset.UtcNow;
            var assertion = Sign(account, now);

            using var response = await _http.CreateClient(HttpClientName).PostAsync(account.TokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }), ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            _accessToken = json.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Google returned no access token.");
            // Renewed a few minutes early, so a token never expires mid-request.
            _accessTokenExpires = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32() - 300);
            _tokenAccount = account;
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>A token signed with this very key that has not run out; a replaced key needs a new one.</summary>
    private bool Fresh(ServiceAccount account) =>
        _accessToken is not null && DateTime.UtcNow < _accessTokenExpires && Equals(_tokenAccount, account);

    private static string Sign(ServiceAccount account, DateTimeOffset now)
    {
        static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = B64(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var claims = B64(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = Scope,
            aud = account.TokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds(),
        }));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{B64(signature)}";
    }

    private sealed record ServiceAccount(string ClientEmail, string PrivateKey, string TokenUri)
    {
        public static ServiceAccount? TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                return new ServiceAccount(
                    root.GetProperty("client_email").GetString()!,
                    root.GetProperty("private_key").GetString()!,
                    root.TryGetProperty("token_uri", out var uri) ? uri.GetString()! : "https://oauth2.googleapis.com/token");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
