using System.Security.Cryptography;
using System.Text;
using Mizan.Contracts.Mcp;

namespace Mizan.Mcp.Server.Authentication;

/// <summary>
/// The gateway path: a hosted assistant reaches this server through a gateway that authenticates itself with
/// <see cref="ClientKey"/> and names the person on every tool call with a signed token (docs/MCP.md#served-to-a-hosted-assistant-through-the-gateway).
/// Built from the Assistant gateway settings an administrator sets in the admin area, which this server reads
/// from the API (<see cref="GatewaySettings"/>); the end-user issuer is Mizan's own, from Tokens Mizan signs.
/// One instance holds one reading and never changes. Off unless the six required values are set and valid.
/// </summary>
public sealed class GatewayOptions
{
    /// <summary>The gateway path closed: what this server holds before the API first answers.</summary>
    public static readonly GatewayOptions Off = new();

    /// <summary>The static credential the gateway presents.</summary>
    public string ClientKey { get; init; } = string.Empty;

    /// <summary>The header that carries <see cref="ClientKey"/>. Empty means <c>Authorization: Bearer</c>.</summary>
    public string ClientKeyHeader { get; init; } = string.Empty;

    /// <summary>The <c>_meta</c> key of a tools/call that holds the person's token.</summary>
    public string MetaKey { get; init; } = string.Empty;

    /// <summary>Where the gateway publishes the Ed25519 keys its tokens are signed with. https only.</summary>
    public string JwksUrl { get; init; } = string.Empty;

    /// <summary>The token's <c>iss</c>.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>The token's <c>aud</c>: this server, as the gateway knows it.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>The token's <c>typ</c> header.</summary>
    public string TokenType { get; init; } = string.Empty;

    /// <summary>The <c>end_user_issuer</c> a token must carry: Mizan's own end-user issuer, so only people Mizan named are served.</summary>
    public string EndUserIssuer { get; init; } = string.Empty;

    /// <summary>True when every required value is set; <see cref="From"/> hands out only valid ones.</summary>
    public bool IsEnabled => Required.All(value => !string.IsNullOrWhiteSpace(value));

    private string[] Required => [ClientKey, MetaKey, JwksUrl, Issuer, Audience, TokenType];

    /// <summary>
    /// The gateway as the settings describe it, or <see cref="Off"/> with the reasons when they are partly set,
    /// invalid, or this server does not serve the task tools. The reasons name settings, never the key.
    /// </summary>
    public static GatewayOptions From(McpGatewaySettings settings, McpToolset toolset, out IReadOnlyList<string> problems)
    {
        var options = new GatewayOptions
        {
            ClientKey = settings.ClientKey?.Trim() ?? string.Empty,
            ClientKeyHeader = settings.ClientKeyHeader?.Trim() ?? string.Empty,
            MetaKey = settings.MetaKey?.Trim() ?? string.Empty,
            JwksUrl = settings.JwksUrl?.Trim() ?? string.Empty,
            Issuer = settings.Issuer?.Trim() ?? string.Empty,
            Audience = settings.Audience?.Trim() ?? string.Empty,
            TokenType = settings.TokenType?.Trim() ?? string.Empty,
            EndUserIssuer = settings.EndUserIssuer?.Trim() ?? string.Empty,
        };
        problems = options.Problems(toolset);
        return problems.Count == 0 && options.IsEnabled ? options : Off;
    }

    /// <summary>What keeps these settings from serving the gateway. Empty when they are all unset or all good.</summary>
    public IReadOnlyList<string> Problems(McpToolset toolset)
    {
        if (!IsEnabled)
        {
            var anySet = Required.Any(value => !string.IsNullOrWhiteSpace(value)) || !string.IsNullOrWhiteSpace(ClientKeyHeader);
            return anySet
                ? ["The assistant gateway settings are partly set. Set Client key, Identity field, Gateway keys URL, Issuer, Audience and Token type together, or none of them."]
                : [];
        }

        var problems = new List<string>();
        if (toolset != McpToolset.Tasks) problems.Add("The gateway needs Mcp:Toolset tasks.");
        if (ClientKey.Length < 32) problems.Add("Client key must be at least 32 characters.");
        if (!Uri.TryCreate(JwksUrl, UriKind.Absolute, out var jwks) || jwks.Scheme != Uri.UriSchemeHttps)
            problems.Add("Gateway keys URL must be an absolute https address.");
        if (string.IsNullOrWhiteSpace(EndUserIssuer)) problems.Add("The gateway needs the Issuer under Tokens Mizan signs.");
        if (!string.IsNullOrWhiteSpace(ClientKeyHeader)
            && (string.Equals(ClientKeyHeader, "Authorization", StringComparison.OrdinalIgnoreCase) || !ClientKeyHeader.All(IsTokenChar)))
            problems.Add("Client key header is not a usable header name. Leave it empty for a bearer key.");
        return problems;
    }

    /// <summary>The credential this request presents in the gateway's place, or null when it presents none there.</summary>
    public string? PresentedKey(HttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(ClientKeyHeader))
            return request.Headers.TryGetValue(ClientKeyHeader, out var value) ? value.ToString() : null;

        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
    }

    /// <summary>Constant-time: the comparison takes as long whatever the presented key is.</summary>
    public bool IsClientKey(string? presented) =>
        presented is not null
        && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(presented)),
            SHA256.HashData(Encoding.UTF8.GetBytes(ClientKey)));

    private static bool IsTokenChar(char c) => char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c);
}
