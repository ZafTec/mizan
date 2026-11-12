using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using NSec.Cryptography;
using Serilog;

namespace Mizan.Mcp.Server.Authentication;

/// <summary>
/// Checks the token a gateway tool call carries in <c>_meta</c> and returns the person it names. The token only
/// says who the person is; it grants nothing. What they may do is decided by the API from their own account and
/// their assistant consent. A refusal is an MCP error whose message starts with its code and never quotes the token.
/// </summary>
public sealed class GatewayTokenVerifier
{
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(60);
    private const string Algorithm = "EdDSA";

    private readonly GatewaySettings _settings;
    private readonly GatewayKeys _keys;
    private readonly IGatewayReplayGuard _replay;
    private readonly TimeProvider _time;

    public GatewayTokenVerifier(GatewaySettings settings, GatewayKeys keys, IGatewayReplayGuard replay, TimeProvider time)
    {
        _settings = settings;
        _keys = keys;
        _replay = replay;
        _time = time;
    }

    /// <summary>The person the call's token names. Throws <see cref="McpProtocolException"/> when the token does not hold.</summary>
    public async Task<Guid> VerifyAsync(JsonObject? meta, CancellationToken cancellationToken)
    {
        // One reading of the settings for the whole check, so a change mid-call cannot mix two of them.
        var options = _settings.Current;
        if (!options.IsEnabled) throw Refuse(GatewayRefusals.Missing, "The gateway path is not configured.");

        var token = meta?[options.MetaKey] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        if (string.IsNullOrWhiteSpace(token))
            throw Refuse(GatewayRefusals.Missing, "The call carries no identity token.");

        var parts = token.Split('.');
        if (parts.Length != 3) throw Refuse(GatewayRefusals.Malformed, "The identity token is not a signed JWT.");

        JsonObject header, claims;
        byte[] signature;
        try
        {
            header = JsonNode.Parse(Base64Url(parts[0]))!.AsObject();
            claims = JsonNode.Parse(Base64Url(parts[1]))!.AsObject();
            signature = Base64Url(parts[2]);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException or NullReferenceException)
        {
            throw Refuse(GatewayRefusals.Malformed, "The identity token could not be read.");
        }

        if (Text(header, "alg") != Algorithm) throw Refuse(GatewayRefusals.Algorithm, "The identity token is not signed with EdDSA.");
        if (Text(header, "typ") != options.TokenType) throw Refuse(GatewayRefusals.Type, "The identity token is not of the expected type.");
        if (Text(header, "kid") is not { Length: > 0 } kid) throw Refuse(GatewayRefusals.Malformed, "The identity token names no key.");

        var key = await _keys.FindAsync(kid, cancellationToken)
            ?? throw Refuse(GatewayRefusals.UnknownKey, "The identity token is signed with a key the gateway does not publish.");
        var signed = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        if (!SignatureAlgorithm.Ed25519.Verify(key, signed, signature))
            throw Refuse(GatewayRefusals.Signature, "The identity token's signature does not verify.");

        var now = _time.GetUtcNow();
        if (Seconds(claims, "exp") is not { } exp) throw Refuse(GatewayRefusals.Malformed, "The identity token has no expiry.");
        var expires = DateTimeOffset.FromUnixTimeSeconds(exp);
        if (now > expires + ClockSkew) throw Refuse(GatewayRefusals.Expired, "The identity token has expired.");
        if (Seconds(claims, "nbf") is { } nbf && now + ClockSkew < DateTimeOffset.FromUnixTimeSeconds(nbf))
            throw Refuse(GatewayRefusals.NotYetValid, "The identity token is not valid yet.");

        if (Text(claims, "iss") != options.Issuer) throw Refuse(GatewayRefusals.Issuer, "The identity token comes from another issuer.");
        if (!Audiences(claims).Contains(options.Audience, StringComparer.Ordinal))
            throw Refuse(GatewayRefusals.Audience, "The identity token is meant for another service.");
        if (Text(claims, "end_user_issuer") != options.EndUserIssuer)
            throw Refuse(GatewayRefusals.EndUserIssuer, "The identity token names a person Mizan did not identify.");
        if (claims["anonymous"] is not JsonValue anonymous || !anonymous.TryGetValue<bool>(out var isAnonymous) || isAnonymous)
            throw Refuse(GatewayRefusals.Anonymous, "The identity token names no signed-in person.");
        if (!Guid.TryParse(Text(claims, "sub"), out var userId) || userId == Guid.Empty)
            throw Refuse(GatewayRefusals.Subject, "The identity token's subject is not a Mizan account.");
        if (Text(claims, "jti") is not { Length: > 0 } jti) throw Refuse(GatewayRefusals.Malformed, "The identity token has no id.");

        // Last, so a token that fails another check does not use up its id.
        if (!await _replay.TryClaimAsync(jti, expires + ClockSkew, cancellationToken))
            throw Refuse(GatewayRefusals.Replayed, "The identity token was already used.");

        return userId;
    }

    public static McpProtocolException Refuse(string code, string sentence)
    {
        Log.Warning("[MCP Gateway] Refused a call: {Code}", code);
        return new McpProtocolException($"{code}: {sentence}", McpErrorCode.InvalidRequest);
    }

    private static string? Text(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static long? Seconds(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<double>(out var seconds) ? (long)seconds : null;

    private static IEnumerable<string> Audiences(JsonObject claims) => claims["aud"] switch
    {
        JsonValue single when single.TryGetValue<string>(out var one) => [one],
        JsonArray many => many.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>(),
        _ => [],
    };

    internal static byte[] Base64Url(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", 0 => string.Empty, _ => throw new FormatException() };
        return Convert.FromBase64String(padded);
    }
}

/// <summary>The codes a gateway call can be refused with. Each is the start of the MCP error's message.</summary>
public static class GatewayRefusals
{
    public const string Missing = "gateway_token_missing";
    public const string Malformed = "gateway_token_malformed";
    public const string Algorithm = "gateway_token_algorithm";
    public const string Type = "gateway_token_type";
    public const string UnknownKey = "gateway_token_unknown_key";
    public const string Signature = "gateway_token_signature";
    public const string Expired = "gateway_token_expired";
    public const string NotYetValid = "gateway_token_not_yet_valid";
    public const string Issuer = "gateway_token_issuer";
    public const string Audience = "gateway_token_audience";
    public const string EndUserIssuer = "gateway_end_user_issuer";
    public const string Anonymous = "gateway_token_anonymous";
    public const string Subject = "gateway_token_subject";
    public const string Replayed = "gateway_token_replayed";
    public const string SubjectUnknown = "gateway_subject_unknown";
}
