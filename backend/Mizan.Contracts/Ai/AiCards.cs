using System.Text.Json;
using Mizan.Contracts.Planning;

namespace Mizan.Contracts.Ai;

/// <summary>
/// The card types a chat turn can carry. Each has one data record below, and each surface renders
/// a fixed component for it: the web a component, Telegram text and a keyboard, the MCP App the
/// same data. A card is built by code from a command's result and checked against its record
/// before it leaves the API; nothing a model writes is ever rendered as a card (docs/AI.md).
/// </summary>
public static class AiCardTypes
{
    public const string PlanPreview = "plan_preview";
    public const string DayPlan = "day_plan";
    public const string CostTable = "cost_table";
    public const string ShoppingList = "shopping_list";

    /// <summary>Each type and the record its <c>data</c> must match.</summary>
    public static readonly IReadOnlyDictionary<string, Type> DataTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        [PlanPreview] = typeof(PlanPreviewCardData),
        [DayPlan] = typeof(DayPlanCardData),
        [CostTable] = typeof(CostTableCardData),
        [ShoppingList] = typeof(ShoppingListCardData),
    };
}

/// <summary>One card of <c>AiChatTurnDto.Cards</c>: a type from <see cref="AiCardTypes"/> and its data record.</summary>
public record AiCardDto(string Type, JsonElement Data);

/// <summary><c>plan_preview</c>: a draft or plan at a glance, with what breaks its rules.</summary>
public record PlanPreviewCardData(
    Guid PlanId,
    string? Name,
    string Status,
    DateOnly? StartDate,
    int DurationDays,
    int EntryCount,
    int UnresolvedCount,
    decimal? BudgetCeiling,
    decimal? WeeklyCost,
    string? Currency,
    IReadOnlyList<PlanViolationDto> Violations);

/// <summary><c>day_plan</c>: one day of a plan in the same shape the plan page shows.</summary>
public record DayPlanCardData(Guid PlanId, string? PlanName, PlanDayDto Day);

/// <summary><c>cost_table</c>: what a plan costs, line by line, with the unpriced lines named.</summary>
public record CostTableCardData(Guid PlanId, PlanCostDto Cost);

/// <summary><c>shopping_list</c>: a list made from a plan.</summary>
public record ShoppingListCardData(Guid ListId, int ItemCount, int NeedsReviewCount);
