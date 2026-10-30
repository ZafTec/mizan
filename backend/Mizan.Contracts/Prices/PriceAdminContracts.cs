namespace Mizan.Contracts.Prices;

/// <summary>A market with its own prices. GET /api/Prices/regions lists the enabled ones; administrators see all under /api/admin/prices/regions.</summary>
public record PriceRegionDto(string Code, string Name, string Currency, bool Enabled);

/// <summary>Body of POST /api/admin/prices/regions.</summary>
public record CreatePriceRegionRequest(string Code, string Name, string Currency, bool Enabled = true);

/// <summary>Body of PUT /api/admin/prices/regions/{code}.</summary>
public record UpdatePriceRegionRequest(string Name, string Currency, bool Enabled);

/// <summary>A source as the administrator sees it: its configuration plus the last run and how many of its products are mapped.</summary>
public record PriceSourceAdminDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = "Manual";
    public string? Url { get; init; }
    public string? ScraperKey { get; init; }
    public string RegionCode { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string? Schedule { get; init; }
    public DateTime? LastRunAt { get; init; }
    public string? LastStatus { get; init; }
    public string? LastError { get; init; }
    public int LastObservations { get; init; }
    public int AliasCount { get; init; }

    /// <summary>Products the worker saw that nobody has mapped to a food or ignored yet.</summary>
    public int UnresolvedCount { get; init; }
    public int PriceCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Body of POST and PUT /api/admin/prices/sources. <c>Kind</c> is Manual, Scraper or Report.</summary>
public record SavePriceSourceRequest(
    string Name,
    string Kind,
    string RegionCode,
    string? Url = null,
    string? ScraperKey = null,
    string? Schedule = null,
    bool Enabled = true);

public record PriceSourceCreatedResult(Guid Id);

/// <summary>What POST /api/admin/prices/sources/{id}/run got back from the worker. The run's result arrives later as an ordinary run record.</summary>
public record PriceSourceRunStartedDto(Guid SourceId, string Status);

/// <summary>A product name as a source writes it and the food it maps to. <c>FoodId</c> null and not <c>Ignored</c> means it waits for review.</summary>
public record PriceSourceAliasDto
{
    public Guid Id { get; init; }
    public Guid SourceId { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public string RegionCode { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ProductKey { get; init; }
    public Guid? FoodId { get; init; }
    public string? FoodName { get; init; }
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }
    public bool Ignored { get; init; }
    public decimal? LastSeenPrice { get; init; }
    public DateTime? LastSeenAt { get; init; }
    public string? LastSeenUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>For a product waiting for review: the shared food its name matches, when one does.</summary>
    public Guid? SuggestedFoodId { get; init; }
    public string? SuggestedFoodName { get; init; }

    /// <summary>For a product with no quantity yet: the package size read from its name, when there is one.</summary>
    public decimal? SuggestedQuantity { get; init; }
    public string? SuggestedUnit { get; init; }
}

/// <summary>Body of POST /api/admin/prices/aliases/accept-suggestions: up to 200 alias ids.</summary>
public record AcceptPriceAliasSuggestionsRequest(List<Guid> AliasIds);

/// <summary>One product mapped from its suggestion.</summary>
public record AcceptedPriceAliasDto(Guid AliasId, Guid FoodId, string FoodName, decimal? Quantity, string? Unit);

/// <summary>One product left as it was. <c>Reason</c> is not_found, already_reviewed or no_suggestion.</summary>
public record SkippedPriceAliasDto(Guid AliasId, string Reason);

/// <summary>Response of POST /api/admin/prices/aliases/accept-suggestions.</summary>
public record AcceptPriceAliasSuggestionsResult(List<AcceptedPriceAliasDto> Accepted, List<SkippedPriceAliasDto> Skipped);

/// <summary>
/// Body of PUT /api/admin/prices/aliases/{id}. Every field is written as sent;
/// a null food clears the mapping. Quantity and unit may stay null when the
/// source's listings state them.
/// </summary>
public record UpdatePriceAliasRequest(Guid? FoodId, decimal? Quantity, string? Unit, bool Ignored = false);

/// <summary>One stored price row, as the history and the admin screens show it.</summary>
public record FoodPriceDto
{
    public Guid Id { get; init; }
    public Guid FoodId { get; init; }
    public string? FoodName { get; init; }
    public string RegionCode { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public Guid? SourceId { get; init; }
    public string? SourceName { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Confidence { get; init; } = "Observed";
    public DateTime ObservedAt { get; init; }
    public string? SourceUrl { get; init; }
    public Guid? ReportedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Body of POST /api/admin/prices/prices. <c>Confidence</c> is Estimated (the
/// default) or Observed. The price is filed under the region's Manual source,
/// which is created the first time.
/// </summary>
public record CreateManualPriceRequest(
    Guid FoodId,
    string RegionCode,
    decimal Price,
    decimal Quantity,
    string Unit,
    string? Confidence = null,
    DateTime? ObservedAt = null);

public record FoodPriceCreatedResult(Guid Id);

/// <summary>A person's report that a shown price is wrong, with the price they saw for comparison.</summary>
public record PriceReportDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? UserName { get; init; }
    public Guid FoodId { get; init; }
    public string? FoodName { get; init; }
    public string RegionCode { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal SuggestedPrice { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string? Note { get; init; }

    /// <summary>The price per kilogram the person saw when they reported, when there was one.</summary>
    public decimal? ShownPricePerKg { get; init; }

    /// <summary>The suggested price per kilogram, when its unit converts for the food.</summary>
    public decimal? SuggestedPricePerKg { get; init; }
    public string Status { get; init; } = "Open";
    public Guid? ResolvedByUserId { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>Body of POST /api/admin/prices/reports/{id}/accept and /reject.</summary>
public record ResolvePriceReportRequest(string? Note = null);

/// <summary>What POST /api/admin/prices/seed-staples did: prices written, staples already priced, and staples with no catalogue food of that name.</summary>
public record SeedStaplePricesResult(int Stored, int AlreadyPriced, List<string> Missing);

/// <summary>
/// Response of POST /api/admin/prices/import-starter-foods. <c>Created</c> lists the names added;
/// <c>Existing</c> counts the starter foods the catalogue already had.
/// </summary>
public record ImportStarterFoodsResult(int Created, int Existing, List<string> CreatedNames);
