namespace Mizan.Mcp.Server;

/// <summary>
/// The web app's addresses as the MCP service tells them to a model. One place, so a deployment
/// on another host never points its users at the wrong site. <c>App:PublicUrl</c> names the app;
/// without it the service takes <c>Mcp:PublicUrl</c> minus its trailing <c>/mcp</c>, since both
/// sit on the one public host in production.
/// </summary>
public sealed class McpLinks
{
    public const string DefaultAppUrl = "http://localhost:3000";

    public McpLinks(IConfiguration? configuration)
        : this(configuration?["App:PublicUrl"], configuration?["Mcp:PublicUrl"]) { }

    public McpLinks(string? appPublicUrl, string? mcpPublicUrl) => AppUrl = Resolve(appPublicUrl, mcpPublicUrl);

    /// <summary>The web app's origin, without a trailing slash.</summary>
    public string AppUrl { get; }

    public string Billing => $"{AppUrl}/billing";

    public string LimitReached(int monthlyLimit) =>
        $"[MONTHLY LIMIT REACHED] The free plan includes {monthlyLimit} MCP calls per month. Upgrade at {Billing}.";

    public string UpgradeRequired(string message) =>
        $"[UPGRADE REQUIRED] {message} Manage plan: {Billing}";

    internal static string Resolve(string? appPublicUrl, string? mcpPublicUrl)
    {
        if (!string.IsNullOrWhiteSpace(appPublicUrl)) return appPublicUrl.Trim().TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(mcpPublicUrl))
        {
            var origin = mcpPublicUrl.Trim().TrimEnd('/');
            if (origin.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase)) origin = origin[..^"/mcp".Length];
            return origin;
        }

        return DefaultAppUrl;
    }
}
