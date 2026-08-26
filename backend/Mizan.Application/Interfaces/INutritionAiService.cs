namespace Mizan.Application.Interfaces;

/// <summary>
/// The user-facing AI surfaces. Both go through the same three gates - quota,
/// consent, then the provider - because a surface that skips one is an
/// unmetered or unconsented call (docs/REFOCUS.md §10, §11).
/// </summary>
public interface INutritionAiService
{
    /// <summary>
    /// One turn. <paramref name="history"/> is the earlier turns of the same
    /// thread, oldest first; the caller decides how far back to go, because it
    /// is the caller that pays for the tokens.
    /// </summary>
    Task<AiChatTurn> GetNutritionAdviceAsync(
        Guid userId,
        string userMessage,
        IReadOnlyList<AiChatHistoryTurn> history,
        CancellationToken cancellationToken = default);

    Task<FoodAnalysisResult> AnalyzeFoodImageAsync(
        Guid userId,
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<MealSuggestionResult> SuggestMealsAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Proposals, not records. There is deliberately no recipe id here: the model
/// cannot know one, and a fabricated id that renders as a link is worse than
/// no link at all.
/// </summary>
public record MealSuggestionResult
{
    public List<MealSuggestion> Suggestions { get; init; } = new();

    /// <summary>Why the list is short or empty - usually "nothing was shared".</summary>
    public string? Note { get; init; }
}

public record MealSuggestion
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Calories { get; init; }
    public decimal Protein { get; init; }
    public decimal Carbs { get; init; }
    public decimal Fat { get; init; }

    /// <summary>The gap this fills, in the user's own numbers.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>An earlier turn, replayed into the next call.</summary>
public record AiChatHistoryTurn(bool FromUser, string Content);

/// <summary>
/// The reply plus which published version produced it, so a bad answer is
/// traceable to the exact text that caused it.
/// </summary>
public record AiChatTurn(string Content, Guid? PromptVersionId);

public record FoodAnalysisResult
{
    public List<RecognizedFood> Foods { get; init; } = new();
    public decimal TotalCalories { get; init; }
    public decimal Confidence { get; init; }

    /// <summary>Caveats from the model. Never the payload - the numbers are.</summary>
    public string? Note { get; init; }
}

public record RecognizedFood
{
    public string Name { get; init; } = string.Empty;
    public decimal PortionGrams { get; init; }
    public decimal Calories { get; init; }
    public decimal Protein { get; init; }
    public decimal Carbs { get; init; }
    public decimal Fat { get; init; }
}
