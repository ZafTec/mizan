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

    public static IEnumerable<McpServerResource> Resources() => Pages.Select(page =>
    {
        var html = Read(page);
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

    private static string Read(string page)
    {
        var assembly = typeof(MizanApps).Assembly;
        using var stream = assembly.GetManifestResourceStream($"apps/{page}.html")
            ?? throw new InvalidOperationException($"The app page {page} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
