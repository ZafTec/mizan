namespace Mizan.Domain.Entities;

/// <summary>
/// Record of a processed Paddle webhook event. Used for idempotency: Paddle can
/// redeliver events, so the handler inserts the event id first and skips duplicates.
/// </summary>
public class PaddleWebhookEvent
{
    public Guid Id { get; set; }
    public string EventId { get; set; } = string.Empty;   // Paddle evt_...
    public string EventType { get; set; } = string.Empty; // e.g. subscription.activated
    public DateTime ReceivedAt { get; set; }
}
