namespace Mizan.Contracts.Foods;

/// <summary>Body of POST /api/Foods. Nutrition is always per 100 g.</summary>
public record CreateFoodRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? Barcode { get; init; }
    public decimal CaloriesPer100g { get; init; }
    public decimal ProteinPer100g { get; init; }
    public decimal CarbsPer100g { get; init; }
    public decimal FatPer100g { get; init; }
    public decimal? FiberPer100g { get; init; }
    public decimal? SugarPer100g { get; init; }
    public decimal? SodiumPer100g { get; init; }
    public decimal ServingSize { get; init; } = 100;
    public string ServingUnit { get; init; } = "g";
    public bool IsVerified { get; init; }
}

/// <summary>
/// Body of PUT /api/Foods/{id}. Id is carried in the body as well as the route
/// because the command validates it; the controller keeps the two in step.
/// </summary>
public record UpdateFoodRequest : CreateFoodRequest
{
    public Guid Id { get; init; }
}
