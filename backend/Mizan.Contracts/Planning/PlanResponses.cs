namespace Mizan.Contracts.Planning;

/// <summary>Nutrition of one entry or one day. Code computes it; nothing on the wire is trusted back.</summary>
public record EntryNutritionDto(
    decimal Calories,
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatGrams,
    decimal FiberGrams);

/// <summary>One way a plan breaks its rules. The same list on every surface.</summary>
public record PlanViolationDto(int? DayIndex, string? Slot, string Rule, string Message);

public record PrepSessionDto(Guid Id, int DayIndex, DateOnly? Date, string Title, string? Notes, int EntryCount);

/// <summary>
/// One entry as every surface shows it. <c>Nutrition</c> is null when it cannot
/// be resolved (a text line, a recipe without summable ingredients).
/// <c>Logged</c> says whether the viewer's diary already has a row from this
/// entry for its date. <c>Cost</c> is reserved for the prices step and null for now.
/// </summary>
public record PlanEntryDto
{
    public Guid Id { get; init; }
    public int DayIndex { get; init; }
    public DateOnly? Date { get; init; }
    public string Slot { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public Guid? RecipeId { get; init; }
    public string? RecipeTitle { get; init; }
    public string? RecipeImageUrl { get; init; }
    public decimal? Servings { get; init; }
    public Guid? FoodId { get; init; }
    public string? FoodName { get; init; }
    public decimal? Grams { get; init; }
    public string? Text { get; init; }
    public string? Notes { get; init; }
    public int SortOrder { get; init; }
    public Guid? PrepSessionId { get; init; }
    public EntryNutritionDto? Nutrition { get; init; }
    public bool Logged { get; init; }
    public decimal? Cost { get; init; }
}

public record PlanSlotDto(string Slot, List<PlanEntryDto> Entries);

/// <summary>One day of a plan: its slots, its totals, and the violations that belong to it.</summary>
public record PlanDayDto
{
    public int DayIndex { get; init; }
    public DateOnly? Date { get; init; }
    public List<PlanSlotDto> Slots { get; init; } = [];
    public EntryNutritionDto Totals { get; init; } = new(0, 0, 0, 0, 0);
    public int UnresolvedCount { get; init; }
    public int EntryCount { get; init; }
    public int LoggedCount { get; init; }
    public List<PlanViolationDto> Violations { get; init; } = [];
    public decimal? Cost { get; init; }
}

/// <summary>A row of GET /api/MealPlans. <c>Kind</c> and <c>Status</c> travel as names.</summary>
public record MealPlanSummaryDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string Kind { get; init; } = "Personal";
    public string Status { get; init; } = "Active";
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int DurationDays { get; init; }
    public Guid? UserId { get; init; }
    public Guid? HouseholdId { get; init; }
    public Guid? AuthoredByUserId { get; init; }
    public string? AuthoredByName { get; init; }
    public int EntryCount { get; init; }
    public bool HasRules { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>GET /api/MealPlans/{id}: the plan with its days, sessions, totals and violations.</summary>
public record MealPlanDetailDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string Kind { get; init; } = "Personal";
    public string Status { get; init; } = "Active";
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int DurationDays { get; init; }
    public Guid? UserId { get; init; }
    public Guid? HouseholdId { get; init; }
    public Guid? AuthoredByUserId { get; init; }
    public string? AuthoredByName { get; init; }
    public PlanRulesDto Rules { get; init; } = new();
    public List<PlanDayDto> Days { get; init; } = [];
    public List<PrepSessionDto> PrepSessions { get; init; } = [];
    public List<PlanViolationDto> Violations { get; init; } = [];
    public int EntryCount { get; init; }
    public int LoggedCount { get; init; }

    /// <summary>Whether the viewer may change entries, sessions and rules.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Reserved for the prices step; null until then.</summary>
    public decimal? WeeklyCost { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>GET /api/MealPlans/day: the active plan's day for a date, in the same entry shape as the plan.</summary>
public record PlanDayViewDto
{
    public Guid PlanId { get; init; }
    public string? PlanName { get; init; }
    public DateOnly Date { get; init; }
    public PlanDayDto Day { get; init; } = new();
    public PlanRulesDto Rules { get; init; } = new();
}

/// <summary>A row of GET /api/MealPlans/templates. <c>Global</c> templates belong to no one and are visible to everyone.</summary>
public record TemplateDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public int DurationDays { get; init; }
    public bool Global { get; init; }
    public Guid? OwnerId { get; init; }
    public int EntryCount { get; init; }
    public PlanRulesDto Rules { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record MealPlanCreatedResult(Guid Id, string? Name, string Status, int EntryCount);

public record PlanEntryCreatedResult(Guid Id);

public record PrepSessionCreatedResult(Guid Id);

public record GenerateShoppingListResult(Guid ListId, int ItemCount, int NeedsReviewCount);
