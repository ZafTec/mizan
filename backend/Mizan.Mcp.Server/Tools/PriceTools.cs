using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// Food prices: what a food costs in the user's region, reporting a wrong one,
/// and the administrator's price book (docs/PLANNING.md#prices). Every tool is
/// one API call; the API resolves the region, picks the price and checks roles.
/// </summary>
[McpServerToolType]
public sealed class PriceTools
{
    private readonly IBackendApiClient _api;

    public PriceTools(IBackendApiClient api) => _api = api;

    // ---- People -----------------------------------------------------------

    [McpServerTool(Name = "get_food_price", ReadOnly = true, Idempotent = true)]
    [Description(
        "The price of a food in the user's price region: price per kilogram, the price as quoted (price for quantity and unit), "
        + "currency, confidence (Observed, Reported or Estimated), source and date. priced is false when the region has no price.")]
    public Task<string> GetFoodPrice([Description("Food UUID")] string foodId, CancellationToken ct = default) =>
        _api.GetAsync($"/api/Foods/{ToolArguments.ParseId(foodId, "foodId")}/price", ct);

    [McpServerTool(Name = "report_food_price")]
    [Description(
        "Report that a food's shown price is wrong, with what the user paid: price for quantity of unit (g, kg, ml, l or piece). "
        + "An administrator reviews it; an accepted report becomes the region's price.")]
    public Task<string> ReportFoodPrice(
        [Description("Food UUID")] string foodId,
        [Description("What the user paid, in the region's currency")] decimal suggestedPrice,
        [Description("How much they bought for that price")] decimal quantity,
        [Description("g, kg, ml, l or piece")] string unit,
        [Description("Where and when, optional")] string? note = null,
        CancellationToken ct = default) =>
        _api.PostAsync($"/api/Foods/{ToolArguments.ParseId(foodId, "foodId")}/price-reports", new { suggestedPrice, quantity, unit, note }, ct);

    // ---- Administration ---------------------------------------------------

    [McpServerTool(Name = "admin_list_price_sources", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Price sources with their last run, status and error, and how many products are mapped or waiting for review.")]
    public Task<string> ListSources([Description("Region code such as ET-AA, optional")] string? regionCode = null, CancellationToken ct = default) =>
        _api.GetAsync(
            string.IsNullOrWhiteSpace(regionCode) ? "/api/admin/prices/sources" : $"/api/admin/prices/sources?regionCode={Uri.EscapeDataString(regionCode)}",
            ct);

    [McpServerTool(Name = "admin_run_price_source")]
    [Description("Admin only. Asks the prices worker to run a scraper source now. The result shows on the source later as its last run.")]
    public Task<string> RunSource([Description("Price source UUID")] string sourceId, CancellationToken ct = default) =>
        _api.PostAsync($"/api/admin/prices/sources/{ToolArguments.ParseId(sourceId, "sourceId")}/run", null, ct);

    [McpServerTool(Name = "admin_list_price_review_queue", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Products the price sources showed that are not mapped to a food or ignored yet, newest first, with the last price seen, "
        + "the suggested shared food (suggestedFoodId, suggestedFoodName) and the package size read from the name (suggestedQuantity, suggestedUnit).")]
    public Task<string> ReviewQueue(
        [Description("Price source UUID, optional")] string? sourceId = null,
        [Description("Part of the product name, optional")] string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new QueryString()
            .Add("queue", "true")
            .Add("sourceId", ToolArguments.ParseOptionalId(sourceId, "sourceId")?.ToString())
            .Add("search", search)
            .Add("page", page)
            .Add("pageSize", pageSize);
        return _api.GetAsync($"/api/admin/prices/aliases{query}", ct);
    }

    [McpServerTool(Name = "admin_update_price_alias", Destructive = true)]
    [Description(
        "Admin only. Maps a source's product to a catalogue food, with the quantity and unit one listing holds when its name does not say, "
        + "or marks it ignored. Every field is written as sent: leaving foodId out clears the mapping. The next observation stores a price.")]
    public Task<string> UpdateAlias(
        [Description("Alias UUID from the review queue")] string aliasId,
        [Description("Catalogue food UUID")] string? foodId = null,
        [Description("Amount in one listing, with unit")] decimal? quantity = null,
        [Description("g, kg, ml, l or piece")] string? unit = null,
        [Description("True to stop pricing this product")] bool ignored = false,
        CancellationToken ct = default) =>
        _api.PutAsync(
            $"/api/admin/prices/aliases/{ToolArguments.ParseId(aliasId, "aliasId")}",
            new { foodId = ToolArguments.ParseOptionalId(foodId, "foodId"), quantity, unit, ignored },
            ct);

    [McpServerTool(Name = "admin_accept_price_alias_suggestions")]
    [Description("Admin only. Maps up to 200 products from the review queue to their suggested food and package size, as the queue shows them. "
        + "Products already mapped or ignored, or with no suggested food, are skipped and listed with the reason.")]
    public Task<string> AcceptAliasSuggestions(
        [Description("Alias UUIDs from the review queue")] string[] aliasIds,
        CancellationToken ct = default) =>
        _api.PostAsync(
            "/api/admin/prices/aliases/accept-suggestions",
            new { aliasIds = aliasIds.Select(id => ToolArguments.ParseId(id, "aliasIds")).ToList() },
            ct);

    [McpServerTool(Name = "admin_import_starter_foods", Idempotent = true)]
    [Description("Admin only. Adds the starter foods to the shared catalogue: generic foods with USDA SR Legacy values per 100 g (verified) and "
        + "Ethiopian staples such as injera, shiro, berbere and niter kibbeh with estimated values (unverified). Safe to repeat; existing foods are left alone. "
        + "Seeding the staple prices afterwards (Admin, Prices) prices them in Addis Ababa.")]
    public Task<string> ImportStarterFoods(CancellationToken ct = default) =>
        _api.PostAsync("/api/admin/prices/import-starter-foods", null, ct);

    [McpServerTool(Name = "admin_add_manual_price")]
    [Description(
        "Admin only. Types a price in for a food in a region: price for quantity of unit (g, kg, ml, l or piece). "
        + "confidence is Estimated (default) or Observed when it was seen on a shelf.")]
    public Task<string> AddManualPrice(
        [Description("Food UUID")] string foodId,
        [Description("Region code such as ET-AA")] string regionCode,
        decimal price,
        decimal quantity,
        [Description("g, kg, ml, l or piece")] string unit,
        [Description("Estimated or Observed")] string? confidence = null,
        CancellationToken ct = default) =>
        _api.PostAsync(
            "/api/admin/prices/prices",
            new { foodId = ToolArguments.ParseId(foodId, "foodId"), regionCode, price, quantity, unit, confidence },
            ct);

    [McpServerTool(Name = "admin_list_price_reports", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Price reports from users, newest first, with the price they were shown and the one they paid. status is Open (default), Accepted, Rejected or all.")]
    public Task<string> ListReports(string status = "Open", int page = 1, CancellationToken ct = default) =>
        _api.GetAsync($"/api/admin/prices/reports?status={Uri.EscapeDataString(status)}&page={page}", ct);

    [McpServerTool(Name = "admin_resolve_price_report", Destructive = true)]
    [Description("Admin only. action is accept, which stores the reported price as a Reported price, or reject, which only closes the report. The user is notified.")]
    public Task<string> ResolveReport(
        [Description("Price report UUID")] string reportId,
        [Description("accept or reject")] string action,
        string? note = null,
        CancellationToken ct = default)
    {
        var verb = action.Trim().ToLowerInvariant() switch
        {
            "accept" => "accept",
            "reject" => "reject",
            _ => throw new ArgumentException("action is accept or reject."),
        };
        return _api.PostAsync($"/api/admin/prices/reports/{ToolArguments.ParseId(reportId, "reportId")}/{verb}", new { note }, ct);
    }
}
