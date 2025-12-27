namespace Mizan.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text"; // text, image, workout_share, recipe_share
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }

    // Navigation properties
    public virtual ChatConversation Conversation { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
}
