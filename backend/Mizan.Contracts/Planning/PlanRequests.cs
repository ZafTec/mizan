namespace Mizan.Contracts.Planning;

/// <summary>
/// The rules a plan is held to, as they cross the wire. Enumerations travel as
/// their names (<c>MacroMode</c>: None, Keto, HighProtein, Custom;
/// <c>PrepStyle</c>: Daily, Batch). See docs/PLANNING.md#rules.
/// </summary>
public record PlanRulesDto
{
    public string MacroMode { get; init; } = "None";
    public decimal? TargetCalories { get; init; }
    public decimal? TargetProtein { get; init; }
    public decimal? MaxCarbs { get; init; }
    public decimal? TargetFat { get; init; }
    public TimeOnly? EatingWindowStart { get; init; }
    public TimeOnly? EatingWindowEnd { get; init; }
    public List<Guid> AllowedFoodIds { get; init; } = [];
    public List<Guid> ForbiddenFoodIds { get; init; } = [];
    public decimal? WeeklyBudget { get; init; }
    public string? Currency { get; init; }
    public string PrepStyle { get; init; } = "Daily";
    public List<int> BatchDays { get; init; } = [];
}

/// <summary>
/// One entry of a plan as a client writes it. <c>Kind</c> is Recipe, Food or
/// Text. A recipe entry needs <c>RecipeId</c> and <c>Servings</c>; a food entry
/// <c>FoodId</c> and <c>Grams</c>; a text entry <c>Text</c>. On a plan or
/// template create, <c>PrepSessionIndex</c> points into the request's
/// <c>PrepSessions</c> list; on add or update, <c>PrepSessionId</c> names an
/// existing session.
/// </summary>
public record PlanEntryRequest
{
    public int DayIndex { get; init; }
    public string Slot { get; init; } = "DINNER";
    public string Kind { get; init; } = "Recipe";
    public Guid? RecipeId { get; init; }
    public decimal? Servings { get; init; }
    public Guid? FoodId { get; init; }
    public decimal? Grams { get; init; }
    public string? Text { get; init; }
    public string? Notes { get; init; }
    public int SortOrder { get; init; }
    public Guid? PrepSessionId { get; init; }
    public int? PrepSessionIndex { get; init; }
}

/// <summary>A cooking session of a plan that cooks in batch.</summary>
public record PrepSessionRequest
{
    public int DayIndex { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

/// <summary>Body of POST /api/MealPlans and of POST /api/Trainers/clients/{clientId}/meal-plans.</summary>
public record CreateMealPlanRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public DateOnly? StartDate { get; init; }
    public int DurationDays { get; init; } = 7;
    public Guid? HouseholdId { get; init; }
    public PlanRulesDto? Rules { get; init; }
    public List<PlanEntryRequest> Entries { get; init; } = [];
    public List<PrepSessionRequest> PrepSessions { get; init; } = [];
}

/// <summary>Body of PUT /api/MealPlans/{id}. Shrinking the plan below its last entry's day is refused.</summary>
public record UpdateMealPlanRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public DateOnly? StartDate { get; init; }
    public int DurationDays { get; init; } = 7;
    public PlanRulesDto? Rules { get; init; }
}

/// <summary>Body of POST /api/MealPlans/{id}/status. <c>Status</c> is Active or Archived.</summary>
public record SetMealPlanStatusRequest(string Status);

/// <summary>Body of POST /api/MealPlans/{id}/entries/{entryId}/log. Both fields default from the entry.</summary>
public record LogPlanEntryRequest
{
    public DateOnly? Date { get; init; }
    public string? MealType { get; init; }
}

/// <summary>Body of POST /api/MealPlans/{id}/shopping-list.</summary>
public record GenerateShoppingListRequest
{
    public string? Name { get; init; }

    /// <summary>An existing list whose items are replaced instead of creating a new list.</summary>
    public Guid? ReplaceListId { get; init; }
}

/// <summary>Body of POST /api/MealPlans/templates. <c>Global</c> publishes it for everyone and needs the admin role.</summary>
public record CreateTemplateRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int DurationDays { get; init; } = 7;
    public bool Global { get; init; }
    public PlanRulesDto? Rules { get; init; }
    public List<PlanEntryRequest> Entries { get; init; } = [];
    public List<PrepSessionRequest> PrepSessions { get; init; } = [];
}

/// <summary>Body of PUT /api/MealPlans/templates/{id}.</summary>
public record UpdateTemplateRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int DurationDays { get; init; } = 7;
    public PlanRulesDto? Rules { get; init; }
}

/// <summary>Body of POST /api/MealPlans/templates/{id}/apply.</summary>
public record ApplyTemplateRequest
{
    public DateOnly StartDate { get; init; }
    public Guid? HouseholdId { get; init; }
    public string? Name { get; init; }
}

/// <summary>Body of POST /api/Trainers/clients/{clientId}/meal-plans/from-template: a coach's template copied into a draft for the client.</summary>
public record ApplyTemplateForClientRequest
{
    public Guid TemplateId { get; init; }
    public DateOnly StartDate { get; init; }
    public string? Name { get; init; }
}
