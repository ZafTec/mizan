using System.ComponentModel;
using System.Text.Json;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Resources;

/// <summary>
/// The fixed lists the tools take values from, read from <c>GET /api/tools/reference</c>: the price regions
/// a person can choose, goal types, meal slots, macro modes and reminder kinds. They hold nothing of the
/// person, so every connection may read them and a read is not counted (docs/MCP.md#resources).
/// </summary>
[McpServerResourceType]
public sealed class ReferenceResources
{
    private readonly IBackendApiClient _api;

    public ReferenceResources(IBackendApiClient api) => _api = api;

    [McpServerResource(UriTemplate = "mizan://reference", Name = "reference", Title = "Reference lists", MimeType = "application/json")]
    [Description("Every fixed list the tools take values from: priceRegions (code, name, currency), goalTypes, mealSlots, macroModes and reminderKinds.")]
    public Task<string> All(CancellationToken ct) => _api.GetAsync("/api/tools/reference", ct);

    [McpServerResource(UriTemplate = "mizan://reference/price-regions", Name = "price-regions", Title = "Price regions", MimeType = "application/json")]
    [Description("The price regions a person can choose, each with its code, name and currency.")]
    public Task<string> PriceRegions(CancellationToken ct) => Part("priceRegions", ct);

    [McpServerResource(UriTemplate = "mizan://reference/goal-types", Name = "goal-types", Title = "Goal types", MimeType = "application/json")]
    [Description("The goal types a goal can have.")]
    public Task<string> GoalTypes(CancellationToken ct) => Part("goalTypes", ct);

    [McpServerResource(UriTemplate = "mizan://reference/meal-slots", Name = "meal-slots", Title = "Meal slots", MimeType = "application/json")]
    [Description("The meals a diary entry or a plan entry can belong to.")]
    public Task<string> MealSlots(CancellationToken ct) => Part("mealSlots", ct);

    [McpServerResource(UriTemplate = "mizan://reference/macro-modes", Name = "macro-modes", Title = "Macro modes", MimeType = "application/json")]
    [Description("The macro modes a meal plan's rules can use.")]
    public Task<string> MacroModes(CancellationToken ct) => Part("macroModes", ct);

    [McpServerResource(UriTemplate = "mizan://reference/reminder-kinds", Name = "reminder-kinds", Title = "Reminder kinds", MimeType = "application/json")]
    [Description("The kinds of reminder a person can set.")]
    public Task<string> ReminderKinds(CancellationToken ct) => Part("reminderKinds", ct);

    private async Task<string> Part(string name, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(await _api.GetAsync("/api/tools/reference", ct));
        foreach (var property in document.RootElement.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value.GetRawText();
        return "[]";
    }
}
