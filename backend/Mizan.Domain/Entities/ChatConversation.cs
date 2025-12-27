namespace Mizan.Domain.Entities;

public class ChatConversation
{
    public Guid Id { get; set; }
    public Guid TrainerClientRelationshipId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual TrainerClientRelationship Relationship { get; set; } = null!;
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
