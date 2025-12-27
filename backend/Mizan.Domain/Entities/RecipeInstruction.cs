namespace Mizan.Domain.Entities;

public class RecipeInstruction
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int StepNumber { get; set; }
    public string Instruction { get; set; } = string.Empty;

    // Navigation property
    public virtual Recipe Recipe { get; set; } = null!;
}
