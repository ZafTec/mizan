using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class NutritionTools
{
    private readonly IBackendApiClient _api;
    public NutritionTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "get_nutrition_summary", ReadOnly = true, Idempotent = true)]
    [Description("Get daily nutrition totals plus goal progress for a date.")]
    public async Task<string> GetNutritionSummary([Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null, CancellationToken ct = default)
    {
        var query = "/api/Nutrition/daily" + (string.IsNullOrWhiteSpace(date) ? string.Empty : $"?date={Uri.EscapeDataString(date)}");
        return await _api.GetAsync(query, ct);
    }
}
