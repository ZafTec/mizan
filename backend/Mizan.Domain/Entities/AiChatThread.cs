namespace Mizan.Domain.Entities;

public class AiChatThread
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ThreadType { get; set; } = "nutrition"; // nutrition, workout, general
    public string ThreadData { get; set; } = "{}"; // JSON serialized thread state
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}
