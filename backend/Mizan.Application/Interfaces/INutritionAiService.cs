namespace Mizan.Application.Interfaces;

public interface INutritionAiService
{
    Task<string> GetNutritionAdviceAsync(Guid userId, string userMessage, CancellationToken cancellationToken = default);
    Task<FoodAnalysisResult> AnalyzeFoodImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}

public record FoodAnalysisResult
{
    public List<RecognizedFood> Foods { get; init; } = new();
    public decimal TotalCalories { get; init; }
    public decimal Confidence { get; init; }
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
