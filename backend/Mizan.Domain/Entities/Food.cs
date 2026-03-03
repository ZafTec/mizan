namespace Mizan.Domain.Entities;

public class Food
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Barcode { get; set; }
    public decimal ServingSize { get; set; } = 100;
    public string ServingUnit { get; set; } = "g";
    public decimal CaloriesPer100g { get; set; }
    public decimal ProteinPer100g { get; set; }
    public decimal CarbsPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public decimal? FiberPer100g { get; set; }
    public decimal? SugarPer100g { get; set; }
    public decimal? SodiumPer100g { get; set; }
    public decimal ProteinCalorieRatio { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static decimal ComputeProteinCalorieRatio(decimal calories, decimal proteinGrams)
        => calories > 0 ? Math.Round(proteinGrams * 4m / calories * 100m, 2) : 0m;

    // Navigation properties
    public virtual ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    public virtual ICollection<FoodDiaryEntry> DiaryEntries { get; set; } = new List<FoodDiaryEntry>();
}
