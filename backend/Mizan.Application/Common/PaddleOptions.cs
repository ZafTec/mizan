namespace Mizan.Application.Common;

/// <summary>
/// Paddle billing configuration. Bound from the "Paddle" configuration section.
/// Secrets (ApiKey, WebhookSecret) come from environment at runtime, never committed.
/// </summary>
public class PaddleOptions
{
    public const string SectionName = "Paddle";

    public string Environment { get; set; } = "sandbox"; // sandbox | production
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int GraceDays { get; set; } = 3;

    /// <summary>Price id for the one-time Lifetime purchase, used to detect lifetime grants from transaction.completed.</summary>
    public string LifetimePriceId { get; set; } = string.Empty;
}
