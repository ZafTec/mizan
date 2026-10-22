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

/// <summary>Every source with its last run and its alias and price counts, for /admin/prices.</summary>
public record GetPriceSourcesQuery(string? RegionCode = null) : IRequest<List<PriceSourceAdminDto>>;

public class GetPriceSourcesQueryHandler : IRequestHandler<GetPriceSourcesQuery, List<PriceSourceAdminDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPriceSourcesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<PriceSourceAdminDto>> Handle(GetPriceSourcesQuery request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var rows = await _context.PriceSources.AsNoTracking()
            .Where(s => request.RegionCode == null || s.RegionCode == request.RegionCode)
            .OrderBy(s => s.RegionCode).ThenBy(s => s.Name)
            .Select(s => new
            {
                Source = s,
                s.Region.Currency,
                AliasCount = s.Aliases.Count(),
                Unresolved = s.Aliases.Count(a => a.FoodId == null && !a.Ignored),
                PriceCount = s.Prices.Count(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PriceSourceAdminDto
        {
            Id = r.Source.Id,
            Name = r.Source.Name,
            Kind = r.Source.Kind.ToString(),
            Url = r.Source.Url,
            ScraperKey = r.Source.ScraperKey,
            RegionCode = r.Source.RegionCode,
            Currency = r.Currency,
            Enabled = r.Source.Enabled,
            Schedule = r.Source.Schedule,
            LastRunAt = r.Source.LastRunAt,
            LastStatus = r.Source.LastStatus,
            LastError = r.Source.LastError,
            LastObservations = r.Source.LastObservations,
            AliasCount = r.AliasCount,
            UnresolvedCount = r.Unresolved,
            PriceCount = r.PriceCount,
            CreatedAt = r.Source.CreatedAt,
            UpdatedAt = r.Source.UpdatedAt,
        }).ToList();
    }
}

/// <summary>Creates a source, or with <c>Id</c> updates one. A scraper source names the worker's scraper and a five-field cron schedule.</summary>
public record SavePriceSourceCommand(
    Guid? Id,
    string Name,
    string Kind,
    string RegionCode,
    string? Url,
    string? ScraperKey,
    string? Schedule,
    bool Enabled) : IRequest<Guid>, IRedactedAudit
{
    /// <summary>The scraper key names a parser in the worker and the region code a place; neither is a credential.</summary>
    object IRedactedAudit.AuditDetails => new { Id, Name, Kind, RegionCode, Url, ScraperKey, Schedule, Enabled };
}

public class SavePriceSourceCommandValidator : AbstractValidator<SavePriceSourceCommand>
{
    public SavePriceSourceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Kind).Must(k => Enum.TryParse<PriceSourceKind>(k, ignoreCase: true, out _))
            .WithMessage("Kind is Manual, Scraper or Report.");
        RuleFor(x => x.RegionCode).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Url).MaximumLength(512)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .When(x => !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("The URL must be an absolute http or https address.");
        RuleFor(x => x.ScraperKey).MaximumLength(50);
        RuleFor(x => x.ScraperKey).NotEmpty()
            .When(x => string.Equals(x.Kind, nameof(PriceSourceKind.Scraper), StringComparison.OrdinalIgnoreCase))
            .WithMessage("A scraper source names the scraper that reads it.");
        RuleFor(x => x.Schedule).MaximumLength(100)
            .Must(s => s!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 5)
            .When(x => !string.IsNullOrWhiteSpace(x.Schedule))
            .WithMessage("The schedule is a five-field cron expression in UTC, for example 0 3 * * *.");
    }
}

public class SavePriceSourceCommandHandler : IRequestHandler<SavePriceSourceCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PriceBookReader _prices;

    public SavePriceSourceCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, PriceBookReader prices)
    {
        _context = context;
        _currentUser = currentUser;
        _prices = prices;
    }

    public async Task<Guid> Handle(SavePriceSourceCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        await _prices.RequireRegionAsync(request.RegionCode, cancellationToken);
        var name = request.Name.Trim();
        if (await _context.PriceSources.AnyAsync(s => s.RegionCode == request.RegionCode && s.Name == name && s.Id != request.Id, cancellationToken))
            throw new ConflictException($"The region already has a source called {name}.");

        var now = DateTime.UtcNow;
        PriceSource source;
        if (request.Id is { } id)
        {
            source = await _context.PriceSources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                ?? throw new EntityNotFoundException("PriceSource", id);
            if (source.RegionCode != request.RegionCode && await _context.FoodPrices.AnyAsync(p => p.SourceId == id, cancellationToken))
                throw new DomainValidationException("A source with stored prices cannot move to another region; create a new source instead.");
        }
        else
        {
            source = new PriceSource { Id = Guid.NewGuid(), CreatedAt = now };
            _context.PriceSources.Add(source);
        }

        source.Name = name;
        source.Kind = Enum.Parse<PriceSourceKind>(request.Kind, ignoreCase: true);
        source.RegionCode = request.RegionCode;
        source.Url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim();
        source.ScraperKey = string.IsNullOrWhiteSpace(request.ScraperKey) ? null : request.ScraperKey.Trim();
        source.Schedule = string.IsNullOrWhiteSpace(request.Schedule) ? null : request.Schedule.Trim();
        source.Enabled = request.Enabled;
        source.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        return source.Id;
    }
}

/// <summary>Removes a source and its aliases. Its stored prices stay, without a source.</summary>
public record DeletePriceSourceCommand(Guid Id) : IRequest;

public class DeletePriceSourceCommandHandler : IRequestHandler<DeletePriceSourceCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public DeletePriceSourceCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task Handle(DeletePriceSourceCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var source = await _context.PriceSources.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("PriceSource", request.Id);
        _context.PriceSources.Remove(source);
        await _context.SaveChangesAsync(cancellationToken);
        await PriceBookReader.InvalidateAsync(_cache, cancellationToken);
    }
}

/// <summary>Run now: asks the prices worker to run a scraper source at once. The result arrives later as a run record.</summary>
public record RunPriceSourceCommand(Guid Id) : IRequest<PriceSourceRunStartedDto>;

public class RunPriceSourceCommandHandler : IRequestHandler<RunPriceSourceCommand, PriceSourceRunStartedDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPricesWorkerClient _worker;

    public RunPriceSourceCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, IPricesWorkerClient worker)
    {
        _context = context;
        _currentUser = currentUser;
        _worker = worker;
    }

    public async Task<PriceSourceRunStartedDto> Handle(RunPriceSourceCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var source = await _context.PriceSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("PriceSource", request.Id);
        if (source.Kind != PriceSourceKind.Scraper)
            throw new DomainValidationException("Only a scraper source can run; manual and report sources are filled by people.");
        if (!source.Enabled)
            throw new DomainValidationException("Enable the source before running it.");
        if (!_worker.IsConfigured)
            throw new ConflictException("The prices worker is not configured. Set Prices:WorkerUrl on the API to use Run now.");

        var status = await _worker.RunAsync(source.Id, cancellationToken);
        return new PriceSourceRunStartedDto(source.Id, status);
    }
}
