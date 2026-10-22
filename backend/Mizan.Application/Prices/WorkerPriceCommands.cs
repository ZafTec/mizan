using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Prices;
using Mizan.Domain.Entities;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Prices;

/// <summary>GET /api/internal/prices/sources: the enabled sources, as the prices worker reads them.</summary>
public record GetWorkerPriceSourcesQuery : IRequest<List<PriceSourceDto>>;

public class GetWorkerPriceSourcesQueryHandler : IRequestHandler<GetWorkerPriceSourcesQuery, List<PriceSourceDto>>
{
    private readonly IMizanDbContext _context;

    public GetWorkerPriceSourcesQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<List<PriceSourceDto>> Handle(GetWorkerPriceSourcesQuery request, CancellationToken cancellationToken)
    {
        var rows = await _context.PriceSources.AsNoTracking()
            .Where(s => s.Enabled && s.Region.Enabled)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Kind, s.Url, s.ScraperKey, s.RegionCode, s.Region.Currency, s.Schedule, s.LastRunAt })
            .ToListAsync(cancellationToken);
        return rows
            .Select(s => new PriceSourceDto(s.Id, s.Name, s.Kind.ToString(), s.Url, s.ScraperKey, s.RegionCode, s.Currency, s.Schedule, s.LastRunAt))
            .ToList();
    }
}

/// <summary>
/// POST /api/internal/prices/observations. Each observation is matched to its
/// source's alias by product key, else by product name. A mapped alias with a
/// known quantity stores an Observed price; an ignored one is skipped; an
/// unknown product becomes an alias without a food, which is the review queue.
/// </summary>
public record RecordPriceObservationsCommand(Guid SourceId, IReadOnlyList<PriceObservation> Observations)
    : IRequest<PriceObservationReceipt>, IRedactedAudit
{
    /// <summary>A batch is up to a hundred rows of shop data; the log keeps how many, not what.</summary>
    object IRedactedAudit.AuditDetails => new { SourceId, Count = Observations?.Count ?? 0 };
}

public class RecordPriceObservationsCommandValidator : AbstractValidator<RecordPriceObservationsCommand>
{
    public const int MaxBatch = 100;

    public RecordPriceObservationsCommandValidator()
    {
        RuleFor(x => x.SourceId).NotEmpty();
        RuleFor(x => x.Observations).NotNull();
        RuleFor(x => x.Observations.Count).LessThanOrEqualTo(MaxBatch).When(x => x.Observations is not null)
            .WithMessage($"Send at most {MaxBatch} observations per batch.");
        RuleForEach(x => x.Observations).ChildRules(o =>
        {
            o.RuleFor(x => x.ProductName).NotEmpty().MaximumLength(255);
            o.RuleFor(x => x.ProductKey).MaximumLength(512);
            o.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            o.RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
            o.RuleFor(x => x.Url).MaximumLength(1024);
        });
    }
}

public class RecordPriceObservationsCommandHandler : IRequestHandler<RecordPriceObservationsCommand, PriceObservationReceipt>
{
    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public RecordPriceObservationsCommandHandler(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PriceObservationReceipt> Handle(RecordPriceObservationsCommand request, CancellationToken cancellationToken)
    {
        var source = await _context.PriceSources.Include(s => s.Region)
            .FirstOrDefaultAsync(s => s.Id == request.SourceId, cancellationToken)
            ?? throw new EntityNotFoundException("PriceSource", request.SourceId);

        var names = request.Observations.Select(o => o.ProductName.Trim()).Distinct().ToList();
        var keys = request.Observations.Where(o => !string.IsNullOrWhiteSpace(o.ProductKey)).Select(o => o.ProductKey!.Trim()).Distinct().ToList();
        var aliases = await _context.PriceSourceAliases
            .Where(a => a.SourceId == source.Id && (names.Contains(a.ProductName) || (a.ProductKey != null && keys.Contains(a.ProductKey))))
            .ToListAsync(cancellationToken);

        int stored = 0, queued = 0, ignored = 0;
        var now = DateTime.UtcNow;
        foreach (var observation in request.Observations)
        {
            var name = observation.ProductName.Trim();
            var key = string.IsNullOrWhiteSpace(observation.ProductKey) ? null : observation.ProductKey.Trim();
            var alias = (key is null ? null : aliases.FirstOrDefault(a => a.ProductKey == key))
                ?? aliases.FirstOrDefault(a => a.ProductName == name);

            if (alias is null)
            {
                alias = new PriceSourceAlias
                {
                    Id = Guid.NewGuid(),
                    SourceId = source.Id,
                    ProductName = name,
                    ProductKey = key,
                    CreatedAt = now,
                };
                _context.PriceSourceAliases.Add(alias);
                aliases.Add(alias);
            }

            alias.ProductKey ??= key;
            alias.LastSeenPrice = observation.Price;
            alias.LastSeenAt = observation.ObservedAt;
            alias.LastSeenUrl = observation.Url;
            alias.UpdatedAt = now;

            if (alias.Ignored || !string.Equals(observation.Currency, source.Region.Currency, StringComparison.OrdinalIgnoreCase))
            {
                // A price in another currency cannot be compared with the region's; it is skipped, not converted.
                ignored++;
                continue;
            }

            var (quantity, unit) = alias.Quantity is { } q && PriceUnits.IsValid(alias.Unit)
                ? (q, alias.Unit!)
                : (observation.Quantity ?? 0m, observation.Unit ?? string.Empty);
            if (alias.FoodId is not { } foodId || quantity <= 0 || !PriceUnits.IsValid(unit))
            {
                queued++;
                continue;
            }

            _context.FoodPrices.Add(new FoodPrice
            {
                Id = Guid.NewGuid(),
                FoodId = foodId,
                RegionCode = source.RegionCode,
                SourceId = source.Id,
                Price = observation.Price,
                Quantity = quantity,
                Unit = PriceUnits.Normalize(unit),
                Confidence = PriceConfidence.Observed,
                ObservedAt = DateTime.SpecifyKind(observation.ObservedAt, DateTimeKind.Utc),
                SourceUrl = observation.Url,
                CreatedAt = now,
            });
            stored++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (stored > 0) await PriceBookReader.InvalidateAsync(_cache, cancellationToken);
        return new PriceObservationReceipt(stored, queued, ignored);
    }
}

/// <summary>POST /api/internal/prices/sources/{id}/runs: how the worker's last run of a source ended.</summary>
public record RecordPriceSourceRunCommand(Guid SourceId, string Status, string? Error, int Observations, DateTime FinishedAt) : IRequest;

public class RecordPriceSourceRunCommandValidator : AbstractValidator<RecordPriceSourceRunCommand>
{
    public RecordPriceSourceRunCommandValidator()
    {
        RuleFor(x => x.SourceId).NotEmpty();
        RuleFor(x => x.Status).Must(s => s is PriceRunStatus.Succeeded or PriceRunStatus.Failed or PriceRunStatus.Skipped)
            .WithMessage("Status is succeeded, failed or skipped.");
        RuleFor(x => x.Observations).GreaterThanOrEqualTo(0);
    }
}

public class RecordPriceSourceRunCommandHandler : IRequestHandler<RecordPriceSourceRunCommand>
{
    private const int MaxError = 2000;
    private readonly IMizanDbContext _context;

    public RecordPriceSourceRunCommandHandler(IMizanDbContext context) => _context = context;

    public async Task Handle(RecordPriceSourceRunCommand request, CancellationToken cancellationToken)
    {
        var source = await _context.PriceSources.FirstOrDefaultAsync(s => s.Id == request.SourceId, cancellationToken)
            ?? throw new EntityNotFoundException("PriceSource", request.SourceId);

        source.LastRunAt = DateTime.SpecifyKind(request.FinishedAt, DateTimeKind.Utc);
        source.LastStatus = request.Status;
        source.LastError = request.Error is { Length: > MaxError } text ? text[..MaxError] : request.Error;
        source.LastObservations = request.Observations;
        source.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
