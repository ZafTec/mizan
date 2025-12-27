namespace Mizan.Domain.Entities;

public class RecipeTag
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Tag { get; set; } = string.Empty;

    // Navigation property
    public virtual Recipe Recipe { get; set; } = null!;
}
