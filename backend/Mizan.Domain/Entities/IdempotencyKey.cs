namespace Mizan.Domain.Entities;

/// <summary>
/// The answer to a write the client may send twice. A phone that loses signal after
/// sending "log this meal" retries with the same key, and gets the first answer back
/// instead of a second meal.
/// </summary>
public class IdempotencyKey
{
    public Guid UserId { get; set; }

    /// <summary>Chosen by the client, scoped to the user.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Method, path, query and body, so the same key cannot be reused for a different request.</summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>Null while the first request is still running.</summary>
    public int? StatusCode { get; set; }
    public string? ContentType { get; set; }
    public byte[]? ResponseBody { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
