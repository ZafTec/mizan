namespace Mizan.Domain.Entities;

/// <summary>
/// A product name as a source writes it, mapped to a catalogue food with the
/// quantity the source sells it in. An alias without a food is the review
/// queue: the worker saw the product, nobody has said what it is yet.
/// </summary>
public class PriceSourceAlias
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }

    /// <summary>The name exactly as the source shows it.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>The source's own id or URL for the product, when it has one.</summary>
    public string? ProductKey { get; set; }

    public Guid? FoodId { get; set; }

    /// <summary>How much one listing is, for example 1 kg or 12 pieces. Null until an administrator fills it in.</summary>
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }

    /// <summary>Set when an administrator decides the product is not a food worth pricing.</summary>
    public bool Ignored { get; set; }

    public decimal? LastSeenPrice { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? LastSeenUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual PriceSource Source { get; set; } = null!;
    public virtual Food? Food { get; set; }
}
