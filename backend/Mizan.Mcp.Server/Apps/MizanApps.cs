using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Apps;

/// <summary>
/// The small interactive views Mizan shows inside a chat (MCP Apps). Each one is a
/// single HTML page the host renders in a sandbox. A page can only call tools
/// through its host, so it never holds a credential and can do no more than the
/// connection allows.
/// </summary>
public static class MizanApps
{
    public const string NutritionDay = "ui://mizan/nutrition-day.html";
    public const string FoodPhoto = "ui://mizan/food-photo.html";
    public const string BodyTrend = "ui://mizan/body-trend.html";

    private static readonly string[] Pages = ["nutrition-day", "food-photo", "body-trend"];

    /// <summary>Where a page takes the shared host bridge (<c>_bridge.js</c>) when it is served.</summary>
    public const string BridgeMarker = "<!-- mizan:bridge -->";

    public static IEnumerable<McpServerResource> Resources() => Pages.Select(page =>
    {
        var html = Page(page);
        var resource = McpServerResource.Create(
            () => html,
            new McpServerResourceCreateOptions
            {
                UriTemplate = $"ui://mizan/{page}.html",
                Name = page,
                MimeType = McpApps.HtmlMimeType,
            });
        return McpApps.SetResourceUi(resource, new McpUiResourceMeta { PrefersBorder = false });
    });

    private static readonly Lazy<string> Bridge = new(() => Read("apps/_bridge.js"));

    /// <summary>
    /// The page with the shared host bridge inlined at its marker. Served pages stay one file each,
    /// because the host blocks network requests, while the bridge is kept once in the source.
    /// </summary>
    internal static string Page(string page)
    {
        var html = Read($"apps/{page}.html");
        if (!html.Contains(BridgeMarker, StringComparison.Ordinal))
            throw new InvalidOperationException($"The app page {page} has no {BridgeMarker} marker, so it would be served without its host bridge.");
        return html.Replace(BridgeMarker, Bridge.Value, StringComparison.Ordinal);
    }

    private static string Read(string resource)
    {
        var assembly = typeof(MizanApps).Assembly;
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"The app resource {resource} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
