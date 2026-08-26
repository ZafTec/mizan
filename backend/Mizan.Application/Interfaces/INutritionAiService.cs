namespace Mizan.Application.Interfaces;

/// <summary>
/// The user-facing AI surfaces. Both go through the same three gates - quota,
/// consent, then the provider - because a surface that skips one is an
/// unmetered or unconsented call (docs/REFOCUS.md §10, §11).
/// </summary>
public interface INutritionAiService
{
    Task<string> GetNutritionAdviceAsync(Guid userId, string userMessage, CancellationToken cancellationToken = default);

    Task<FoodAnalysisResult> AnalyzeFoodImageAsync(
        Guid userId,
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken = default);
}

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
