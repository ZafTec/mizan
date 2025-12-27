using System.ComponentModel.DataAnnotations.Schema;

namespace Mizan.Domain.Entities;

[Table("audit_logs")]
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty; // e.g., "Create", "Update", "Delete"
    public string EntityType { get; set; } = string.Empty; // e.g., "Recipe", "Food"
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; } // JSON or description
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public virtual User? User { get; set; }
}
