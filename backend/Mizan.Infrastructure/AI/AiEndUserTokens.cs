using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Contracts.Ai;
using Mizan.Domain.Settings;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// A signed token naming the person behind each provider call, for a gateway that limits and
/// blocks per end user (docs/AI.md#behind-a-gateway). Built from the <c>EndUser.*</c> runtime
/// settings, under Tokens Mizan signs in the admin area: the private key is a secret, and the rest
/// describes how this deployment proves itself to the gateway. Off unless <see cref="Header"/>, <see cref="Issuer"/>,
/// <see cref="Audience"/>, <see cref="KeyId"/> and <see cref="PrivateKey"/> are all set.
/// </summary>
public sealed class AiEndUserOptions
{
    public const string SectionName = "EndUser";

    /// <summary>The request header the token goes in.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>The <c>iss</c> claim, exactly as registered with the gateway.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The <c>aud</c> claim: the application id the gateway gave this deployment.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>The <c>kid</c> of <see cref="PrivateKey"/>, published in the JWKS.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>A P-256 private key in PEM (SEC 1 or PKCS #8). Signs ES256.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>How long a token lives, at most 3600 seconds.</summary>
    public int LifetimeSeconds { get; set; } = 600;

    /// <summary>The key being rotated out, as a P-256 public key in PEM, still published so tokens it signed verify.</summary>
    public string PreviousPublicKey { get; set; } = string.Empty;

    public string PreviousKeyId { get; set; } = string.Empty;

    public bool IsEnabled => Required.All(value => !string.IsNullOrWhiteSpace(value));

    internal bool IsPartlySet =>
        !IsEnabled && (Required.Any(value => !string.IsNullOrWhiteSpace(value))
            || !string.IsNullOrWhiteSpace(PreviousPublicKey) || !string.IsNullOrWhiteSpace(PreviousKeyId));

    private string[] Required => [Header, Issuer, Audience, KeyId, PrivateKey];
}

/// <summary>Finds a half-set configuration or a key that is not P-256, which turns the tokens off. Never quotes the key.</summary>
public sealed class AiEndUserOptionsValidator : IValidateOptions<AiEndUserOptions>
{
    public ValidateOptionsResult Validate(string? name, AiEndUserOptions options)
    {
        if (!options.IsEnabled)
        {
            return options.IsPartlySet
                ? ValidateOptionsResult.Fail(
                    "The end-user token settings are partly set. Set Header, Issuer, Audience, Key id and Signing key together, or none of them.")
                : ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (options.LifetimeSeconds is < 60 or > 3600) failures.Add("Token lifetime must be between 60 and 3600.");
        if (!IsHeaderName(options.Header)) failures.Add("Header is not a valid header name.");
        if (options.Issuer.Trim().Length > 2048) failures.Add("Issuer is too long.");
        if (!TryLoad(options.PrivateKey, requirePrivate: true)) failures.Add("Signing key is not a P-256 private key in PEM.");

        var previousKey = !string.IsNullOrWhiteSpace(options.PreviousPublicKey);
        var previousId = !string.IsNullOrWhiteSpace(options.PreviousKeyId);
        if (previousKey != previousId) failures.Add("Previous public key and Previous key id go together.");
        else if (previousKey && !TryLoad(options.PreviousPublicKey, requirePrivate: false))
            failures.Add("Previous public key is not a P-256 public key in PEM.");
        if (previousId && options.PreviousKeyId.Trim() == options.KeyId.Trim()) failures.Add("Previous key id must differ from Key id.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsHeaderName(string header) =>
        header.Trim().Length is > 0 and <= 128
        && header.Trim().All(c => char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c));

    private static bool TryLoad(string pem, bool requirePrivate)
    {
        try
        {
            using var key = AiEndUserTokens.LoadKey(pem);
            var parameters = key.ExportParameters(includePrivateParameters: false);
            if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value) return false;
            if (!requirePrivate) return true;
            key.ExportParameters(includePrivateParameters: true);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// Signs the end-user token (ES256, compact JWS) and publishes the public keys. <c>sub</c> is the
/// Mizan user id, an opaque GUID; no email or name is ever put in it. The key comes from the
/// runtime settings and is loaded again whenever they change; a token is cached per user until a
/// minute before it expires. Neither the token nor the key is logged.
/// </summary>
public sealed class AiEndUserTokens : IAiCallIdentity, IEndUserSigningKeys, IDisposable
{
    /// <summary>A cached token is replaced this long before it expires, so none arrives stale.</summary>
    public static readonly TimeSpan RenewBefore = TimeSpan.FromSeconds(60);

    private const int PruneAbove = 10_000;

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly IRuntimeOptions<AiEndUserOptions> _options;
    private readonly IServiceScopeFactory? _scopes;
    private readonly TimeProvider _time;
    private readonly ILogger<AiEndUserTokens> _logger;
    private Signer? _signer;

    public AiEndUserTokens(
        IRuntimeOptions<AiEndUserOptions> options,
        TimeProvider time,
        ILogger<AiEndUserTokens> logger,
        IServiceScopeFactory? scopes = null)
    {
        _options = options;
        _time = time;
        _logger = logger;
        _scopes = scopes;
    }

    /// <summary>True while the settings hold a complete, valid signing configuration.</summary>
    public async ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken = default) =>
        (await SignerAsync(cancellationToken)).Key is not null;

    /// <summary>The JSON Web Key Set: the signing key's public half, and the previous key during a rotation. Empty while off.</summary>
    public async ValueTask<AiJsonWebKeySet> GetKeysAsync(CancellationToken cancellationToken = default) =>
        (await SignerAsync(cancellationToken)).Keys;

    public async ValueTask<IReadOnlyList<KeyValuePair<string, string>>> HeadersForAsync(Guid? endUserId, CancellationToken cancellationToken = default)
    {
        if (endUserId is not { } userId || userId == Guid.Empty) return [];
        var signer = await SignerAsync(cancellationToken);
        if (signer.Key is null) return [];
        return [new(signer.Options.Header.Trim(), signer.TokenFor(userId, _time.GetUtcNow()))];
    }

    public async ValueTask<string> TokenForAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var signer = await SignerAsync(cancellationToken);
        if (signer.Key is null) throw new InvalidOperationException("The end-user token is not configured.");
        return signer.TokenFor(userId, _time.GetUtcNow());
    }

    public async Task<EndUserKeyRotationResult> RotateAsync(Guid? rotatedByUserId, CancellationToken cancellationToken = default)
    {
        if (_scopes is null) throw new InvalidOperationException("Rotation needs the settings store.");

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = "mizan-" + _time.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + "-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3));
        var values = new Dictionary<string, string>
        {
            ["EndUser.PrivateKey"] = key.ExportPkcs8PrivateKeyPem(),
            ["EndUser.KeyId"] = keyId,
        };

        // The key in use stays published, so the tokens it signed in the last few minutes still verify.
        var current = (await SignerAsync(cancellationToken)).Key;
        string? previousKeyId = null;
        if (current is not null)
        {
            previousKeyId = (await _options.GetAsync(cancellationToken)).KeyId.Trim();
            values["EndUser.PreviousPublicKey"] = current.ExportSubjectPublicKeyInfoPem();
            values["EndUser.PreviousKeyId"] = previousKeyId;
        }

        using var scope = _scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRuntimeSettings>().SetManyAsync(values, rotatedByUserId, cancellationToken);
        _logger.LogInformation("The end-user signing key was rotated to {KeyId}", keyId);
        return new EndUserKeyRotationResult(keyId, previousKeyId);
    }

    private async ValueTask<Signer> SignerAsync(CancellationToken cancellationToken)
    {
        // The runtime options hand out one instance until a setting changes, so a new instance
        // means new settings and the key is loaded again.
        var options = await _options.GetAsync(cancellationToken);
        var signer = Volatile.Read(ref _signer);
        if (signer is not null && ReferenceEquals(signer.Options, options)) return signer;

        signer = Signer.Create(options, _logger);
        // The replaced key is left to the collector: another call may still be signing with it.
        Volatile.Write(ref _signer, signer);
        return signer;
    }

    internal static ECDsa LoadKey(string pem)
    {
        var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(RuntimeSettingDefinitions.NormalizePem(pem));
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    internal static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => Volatile.Read(ref _signer)?.Key?.Dispose();

    /// <summary>One configuration's key, published set and token cache.</summary>
    private sealed class Signer
    {
        private readonly ConcurrentDictionary<Guid, (string Token, DateTimeOffset ExpiresAt)> _cache = new();

        private Signer(AiEndUserOptions options, ECDsa? key, AiJsonWebKeySet keys)
        {
            Options = options;
            Key = key;
            Keys = keys;
        }

        public AiEndUserOptions Options { get; }

        public ECDsa? Key { get; }

        public AiJsonWebKeySet Keys { get; }

        public static Signer Create(AiEndUserOptions options, ILogger logger)
        {
            var empty = new AiJsonWebKeySet([]);
            if (!options.IsEnabled && !options.IsPartlySet) return new Signer(options, null, empty);

            var check = new AiEndUserOptionsValidator().Validate(null, options);
            if (!options.IsEnabled || check.Failed)
            {
                // The messages name settings, never a key.
                logger.LogWarning("End-user tokens are off: {Problems}", check.Failed ? check.FailureMessage : "the settings are only partly set");
                return new Signer(options, null, empty);
            }

            var key = LoadKey(options.PrivateKey);
            var keys = new List<AiJsonWebKey> { PublicJwk(key, options.KeyId.Trim()) };
            if (!string.IsNullOrWhiteSpace(options.PreviousPublicKey))
            {
                using var previous = LoadKey(options.PreviousPublicKey);
                keys.Add(PublicJwk(previous, options.PreviousKeyId.Trim()));
            }

            return new Signer(options, key, new AiJsonWebKeySet(keys));
        }

        public string TokenFor(Guid userId, DateTimeOffset now)
        {
            if (_cache.TryGetValue(userId, out var cached) && cached.ExpiresAt - RenewBefore > now) return cached.Token;

            var issuedAt = now.ToUnixTimeSeconds();
            var expiresAt = issuedAt + Options.LifetimeSeconds;
            var header = new JwsHeader("ES256", "JWT", Options.KeyId.Trim());
            var claims = new EndUserClaims(Options.Issuer.Trim(), Options.Audience.Trim(), userId.ToString("D"), issuedAt, expiresAt);

            var signingInput = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header, Json)) + "."
                + Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims, Json));
            // .NET signs ECDSA as r || s (IEEE P1363), the form JWS uses.
            var signature = Key!.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);
            var token = signingInput + "." + Base64Url(signature);

            if (_cache.Count > PruneAbove)
            {
                foreach (var (id, entry) in _cache)
                {
                    if (entry.ExpiresAt <= now) _cache.TryRemove(id, out _);
                }
            }
            _cache[userId] = (token, DateTimeOffset.FromUnixTimeSeconds(expiresAt));
            return token;
        }

        private static AiJsonWebKey PublicJwk(ECDsa key, string keyId)
        {
            // Only the public point is exported; the private scalar never leaves the key object.
            var parameters = key.ExportParameters(includePrivateParameters: false);
            return new AiJsonWebKey("EC", "P-256", Base64Url(parameters.Q.X!), Base64Url(parameters.Q.Y!), keyId, "ES256", "sig");
        }
    }

    private sealed record JwsHeader(
        [property: JsonPropertyName("alg")] string Alg,
        [property: JsonPropertyName("typ")] string Typ,
        [property: JsonPropertyName("kid")] string Kid);

    private sealed record EndUserClaims(
        [property: JsonPropertyName("iss")] string Iss,
        [property: JsonPropertyName("aud")] string Aud,
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("iat")] long Iat,
        [property: JsonPropertyName("exp")] long Exp);
}
