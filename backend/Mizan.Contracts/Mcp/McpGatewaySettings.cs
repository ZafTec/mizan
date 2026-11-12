namespace Mizan.Contracts.Mcp;

/// <summary>
/// What the MCP server reads from GET /api/internal/settings/mcp-gateway at startup and every
/// minute: the assistant gateway's settings, which an administrator sets in the admin area
/// (docs/MCP.md#served-to-a-hosted-assistant-through-the-gateway). Only the service key reads them,
/// because the client key travels in clear inside the internal network. A value not set is null.
/// </summary>
/// <param name="ClientKey">The key the gateway presents.</param>
/// <param name="ClientKeyHeader">The header that carries it; null means Authorization: Bearer.</param>
/// <param name="MetaKey">The _meta key of a tool call that holds the person's token.</param>
/// <param name="JwksUrl">Where the gateway publishes its signing keys.</param>
/// <param name="Issuer">The iss of the gateway's tokens.</param>
/// <param name="Audience">The aud of the gateway's tokens.</param>
/// <param name="TokenType">The typ header of the gateway's tokens.</param>
/// <param name="EndUserIssuer">Mizan's own end-user issuer, which a gateway token must carry.</param>
public sealed record McpGatewaySettings(
    string? ClientKey,
    string? ClientKeyHeader,
    string? MetaKey,
    string? JwksUrl,
    string? Issuer,
    string? Audience,
    string? TokenType,
    string? EndUserIssuer);
