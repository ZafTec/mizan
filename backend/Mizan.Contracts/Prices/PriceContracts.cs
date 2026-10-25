namespace Mizan.Contracts.Prices;

/// <summary>
/// A scraper source as the prices worker needs it: GET /api/internal/prices/sources
/// returns the enabled ones. The worker holds no database access and learns
/// everything from this.
/// </summary>
public record PriceSourceDto(
    Guid Id,
    string Name,
    string Kind,
    string? Url,
    string? ScraperKey,
    string RegionCode,
    string Currency,
    string? Schedule,
    DateTime? LastRunAt);

/// <summary>One price the worker read: the product as the source names it, and what it cost.</summary>
public record PriceObservation(
    string ProductName,
    string? ProductKey,
    decimal Price,
    string Currency,
    /// <summary>The listing's quantity and unit when the page states them, for example 1 kg. Null when only the alias knows.</summary>
    decimal? Quantity,
    string? Unit,
    DateTime ObservedAt,
    string? Url);

/// <summary>Body of POST /api/internal/prices/observations.</summary>
public record PriceObservationBatch(Guid SourceId, IReadOnlyList<PriceObservation> Observations);

/// <summary>What the API did with a batch: stored prices for known aliases, queued unknown products for review, skipped ignored ones.</summary>
public record PriceObservationReceipt(int Stored, int Queued, int Ignored);

/// <summary>Body of POST /api/internal/prices/sources/{id}/runs, sent when a run ends.</summary>
public record PriceSourceRunResult(string Status, string? Error, int Observations, DateTime FinishedAt);

/// <summary>Statuses a run can end with.</summary>
public static class PriceRunStatus
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}
