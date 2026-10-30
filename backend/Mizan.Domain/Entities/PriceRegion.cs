namespace Mizan.Domain.Entities;

/// <summary>A market whose prices are kept apart, such as Addis Ababa (<c>ET-AA</c>). Prices are in its currency.</summary>
public class PriceRegion
{
    /// <summary>Country code, a hyphen, and a local code: ET-AA.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>ISO 4217 code, for example ETB.</summary>
    public string Currency { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<PriceSource> Sources { get; set; } = new List<PriceSource>();
}
