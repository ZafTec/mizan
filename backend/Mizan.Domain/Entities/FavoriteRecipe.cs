using System.ComponentModel.DataAnnotations.Schema;

namespace Mizan.Domain.Entities;

[Table("favorite_recipes")]
public class FavoriteRecipe
{
    public Guid UserId { get; set; }
    public Guid RecipeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual Recipe Recipe { get; set; } = null!;
}
