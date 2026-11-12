using System.Text.Json;
using NSec.Cryptography;
using Serilog;

namespace Mizan.Mcp.Server.Authentication;

/// <summary>
/// The gateway's Ed25519 signing keys, read from its JWKS. Kept for an hour; a token naming a key not held
/// fetches the set again, at most once a minute, so a rotation is picked up and a stream of made-up key ids is not.
/// A change of the keys URL in the settings drops the keys held from the old one.
/// </summary>
public sealed class GatewayKeys
{
    public const string HttpClientName = "mcp-gateway-jwks";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan RefreshFloor = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _clients;
    private readonly GatewaySettings _settings;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyDictionary<string, PublicKey> _keys = new Dictionary<string, PublicKey>();
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;
    private string _fetchedFrom = string.Empty;

    public GatewayKeys(IHttpClientFactory clients, GatewaySettings settings, TimeProvider time)
    {
        _clients = clients;
        _settings = settings;
        _time = time;
    }

    public async Task<PublicKey?> FindAsync(string kid, CancellationToken cancellationToken)
    {
        var url = _settings.Current.JwksUrl;
        if (url.Length == 0) return null;

        var now = _time.GetUtcNow();
        var current = url == Volatile.Read(ref _fetchedFrom);
        if (current && _keys.TryGetValue(kid, out var held) && now - _fetchedAt < Lifetime) return held;
        if (current && now - _fetchedAt < RefreshFloor) return _keys.GetValueOrDefault(kid);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            now = _time.GetUtcNow();
            if (url != _fetchedFrom || now - _fetchedAt >= RefreshFloor)
            {
                var fetched = await FetchAsync(url, cancellationToken);
                _keys = fetched ?? (url == _fetchedFrom ? _keys : new Dictionary<string, PublicKey>());
                _fetchedAt = now;
                Volatile.Write(ref _fetchedFrom, url);
            }
            return _keys.GetValueOrDefault(kid);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, PublicKey>?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _clients.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error("[MCP Gateway] The JWKS answered {Status}", (int)response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var keys = new Dictionary<string, PublicKey>(StringComparer.Ordinal);
            if (!document.RootElement.TryGetProperty("keys", out var list) || list.ValueKind != JsonValueKind.Array) return keys;
            foreach (var jwk in list.EnumerateArray())
            {
                if (Read(jwk, "kty") != "OKP" || Read(jwk, "crv") != "Ed25519") continue;
                if (Read(jwk, "use") is { } use && use != "sig") continue;
                if (Read(jwk, "alg") is { } alg && alg != "EdDSA") continue;
                if (Read(jwk, "kid") is not { Length: > 0 } kid || Read(jwk, "x") is not { } x) continue;
                if (PublicKey.TryImport(SignatureAlgorithm.Ed25519, GatewayTokenVerifier.Base64Url(x), KeyBlobFormat.RawPublicKey, out var key) && key is not null)
                    keys[kid] = key;
            }
            return keys;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or TaskCanceledException)
        {
            Log.Error(ex, "[MCP Gateway] The JWKS could not be read");
            return null;
        }
    }

    private static string? Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
