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

    /// <summary>
    /// One turn of onboarding. The model may call allowlisted tools, which run
    /// as this user and come back in <see cref="OnboardingTurn.Performed"/> so
    /// the UI can say what it did (docs/REFOCUS.md §10).
    /// </summary>
    Task<OnboardingTurn> RunOnboardingTurnAsync(
        Guid userId,
        string userMessage,
        IReadOnlyList<AiChatHistoryTurn> history,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A coach asking about one client. Read-only by construction - no tools
    /// are offered, so there is nothing to enforce at call time. The context is
    /// the intersection of what the client granted the coach and what they
    /// consented to for AI (docs/REFOCUS.md §11).
    /// </summary>
    Task<TrainerAnswer> AskAboutClientAsync(
        Guid trainerId,
        Guid clientId,
        string question,
        IReadOnlyList<AiChatHistoryTurn> history,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The answer plus what it was allowed to see, so the coach can tell the
/// difference between "your client is doing fine on protein" and "your client
/// has not shared their nutrition".
/// </summary>
public record TrainerAnswer(
    string Content,
    Guid? PromptVersionId,
    IReadOnlyList<string> AxesSeen);

public record OnboardingTurn(
    string Content,
    Guid? PromptVersionId,
    IReadOnlyList<Ai.Tools.AiToolInvocation> Performed);

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
public record AiChatTurn(
    string Content,
    Guid? PromptVersionId,
    IReadOnlyList<Ai.Tools.AiToolInvocation> Performed);

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
