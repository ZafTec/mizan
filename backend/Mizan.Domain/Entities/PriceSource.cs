using Mizan.Domain.Pricing;

namespace Mizan.Domain.Entities;

/// <summary>Where prices for a region come from: an administrator, a scraped website, or accepted reports.</summary>
public class PriceSource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PriceSourceKind Kind { get; set; } = PriceSourceKind.Manual;

    /// <summary>The site a scraper reads.</summary>
    public string? Url { get; set; }

    /// <summary>Which scraper in the prices worker handles this source, for example <c>aradamart</c>.</summary>
    public string? ScraperKey { get; set; }

    public string RegionCode { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>Five-field cron expression, UTC. Null means the source never runs on its own.</summary>
    public string? Schedule { get; set; }

    public DateTime? LastRunAt { get; set; }
    public string? LastStatus { get; set; }
    public string? LastError { get; set; }
    public int LastObservations { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual PriceRegion Region { get; set; } = null!;
    public virtual ICollection<PriceSourceAlias> Aliases { get; set; } = new List<PriceSourceAlias>();
    public virtual ICollection<FoodPrice> Prices { get; set; } = new List<FoodPrice>();
}
