using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Prices;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Prices;

/// <summary>
/// The product names sources have shown. <c>Queue</c> keeps the review queue:
/// products with no food that nobody has ignored. Newest sightings first.
/// </summary>
public record GetPriceAliasesQuery(Guid? SourceId = null, bool Queue = false, string? Search = null, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<PriceSourceAliasDto>>;

public class GetPriceAliasesQueryHandler : IRequestHandler<GetPriceAliasesQuery, PagedResult<PriceSourceAliasDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PriceAliasSuggester _suggester;

    public GetPriceAliasesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, PriceAliasSuggester suggester)
    {
        _context = context;
        _currentUser = currentUser;
        _suggester = suggester;
    }

    public async Task<PagedResult<PriceSourceAliasDto>> Handle(GetPriceAliasesQuery request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _context.PriceSourceAliases.AsNoTracking()
            .Where(a => request.SourceId == null || a.SourceId == request.SourceId);
        if (request.Queue) query = query.Where(a => a.FoodId == null && !a.Ignored);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(a => a.ProductName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.LastSeenAt).ThenBy(a => a.ProductName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new PriceSourceAliasDto
            {
                Id = a.Id,
                SourceId = a.SourceId,
                SourceName = a.Source.Name,
                RegionCode = a.Source.RegionCode,
                Currency = a.Source.Region.Currency,
                ProductName = a.ProductName,
                ProductKey = a.ProductKey,
                FoodId = a.FoodId,
                FoodName = a.Food != null ? a.Food.Name : null,
                Quantity = a.Quantity,
                Unit = a.Unit,
                Ignored = a.Ignored,
                LastSeenPrice = a.LastSeenPrice,
                LastSeenAt = a.LastSeenAt,
                LastSeenUrl = a.LastSeenUrl,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        // Suggestions only for what still waits for review: an unmapped product nobody ignored.
        var waiting = items.Where(a => a.FoodId == null && !a.Ignored).Select(a => (a.Id, a.ProductName)).ToList();
        if (waiting.Count > 0)
        {
            var suggestions = await _suggester.SuggestAsync(waiting, cancellationToken);
            items = items.Select(a => suggestions.TryGetValue(a.Id, out var s)
                ? a with
                {
                    SuggestedFoodId = s.FoodId,
                    SuggestedFoodName = s.FoodName,
                    SuggestedQuantity = a.Quantity is null ? s.Quantity : null,
                    SuggestedUnit = a.Quantity is null ? s.Unit : null,
                }
                : a).ToList();
        }

        return new PagedResult<PriceSourceAliasDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }
}

/// <summary>
/// Maps a product to a catalogue food with the quantity one listing holds, or
/// marks it ignored. The next observation of the product stores a price.
/// </summary>
public record UpdatePriceAliasCommand(Guid Id, Guid? FoodId, decimal? Quantity, string? Unit, bool Ignored) : IRequest;

public class UpdatePriceAliasCommandValidator : AbstractValidator<UpdatePriceAliasCommand>
{
    public UpdatePriceAliasCommandValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity.HasValue);
        RuleFor(x => x.Unit).Must(PriceUnits.IsValid).When(x => x.Unit is not null)
            .WithMessage("The unit is g, kg, ml, l or piece.");
        RuleFor(x => x).Must(x => x.Quantity.HasValue == (x.Unit is not null))
            .WithMessage("Give the quantity and the unit together, or neither.");
        RuleFor(x => x).Must(x => !(x.Ignored && x.FoodId.HasValue))
            .WithMessage("An ignored product cannot also map to a food.");
    }
}

public class UpdatePriceAliasCommandHandler : IRequestHandler<UpdatePriceAliasCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdatePriceAliasCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdatePriceAliasCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var alias = await _context.PriceSourceAliases.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("PriceSourceAlias", request.Id);
        if (request.FoodId is { } foodId && !await _context.Foods.AnyAsync(f => f.Id == foodId && f.UserId == null, cancellationToken))
            throw new DomainValidationException("Map the product to a food in the shared catalogue.");

        alias.FoodId = request.FoodId;
        alias.Quantity = request.Quantity;
        alias.Unit = request.Unit is null ? null : PriceUnits.Normalize(request.Unit);
        alias.Ignored = request.Ignored;
        alias.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Accepts the review queue's suggestions for the given products. Each suggestion is worked out
/// again here, so what is stored is what the server suggests now. A product that is mapped or
/// ignored already, or has no suggested food, is skipped with its reason.
/// </summary>
public record AcceptPriceAliasSuggestionsCommand(List<Guid> AliasIds) : IRequest<AcceptPriceAliasSuggestionsResult>;

public class AcceptPriceAliasSuggestionsCommandValidator : AbstractValidator<AcceptPriceAliasSuggestionsCommand>
{
    public const int MaxAliases = 200;

    public AcceptPriceAliasSuggestionsCommandValidator()
    {
        RuleFor(x => x.AliasIds).NotEmpty().WithMessage("Choose at least one product.");
        RuleFor(x => x.AliasIds.Count).LessThanOrEqualTo(MaxAliases).WithMessage($"Accept at most {MaxAliases} products at once.");
    }
}

public class AcceptPriceAliasSuggestionsCommandHandler : IRequestHandler<AcceptPriceAliasSuggestionsCommand, AcceptPriceAliasSuggestionsResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PriceAliasSuggester _suggester;

    public AcceptPriceAliasSuggestionsCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, PriceAliasSuggester suggester)
    {
        _context = context;
        _currentUser = currentUser;
        _suggester = suggester;
    }

    public async Task<AcceptPriceAliasSuggestionsResult> Handle(AcceptPriceAliasSuggestionsCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var ids = request.AliasIds.Distinct().ToList();
        var aliases = await _context.PriceSourceAliases.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var waiting = aliases.Values.Where(a => a.FoodId == null && !a.Ignored).Select(a => (a.Id, a.ProductName)).ToList();
        var suggestions = waiting.Count == 0
            ? new Dictionary<Guid, PriceAliasSuggestion>()
            : await _suggester.SuggestAsync(waiting, cancellationToken);

        var accepted = new List<AcceptedPriceAliasDto>();
        var skipped = new List<SkippedPriceAliasDto>();
        var now = DateTime.UtcNow;
        foreach (var id in ids)
        {
            if (!aliases.TryGetValue(id, out var alias)) { skipped.Add(new(id, "not_found")); continue; }
            if (alias.FoodId is not null || alias.Ignored) { skipped.Add(new(id, "already_reviewed")); continue; }
            if (!suggestions.TryGetValue(id, out var s) || s.FoodId is not { } foodId) { skipped.Add(new(id, "no_suggestion")); continue; }

            alias.FoodId = foodId;
            if (alias.Quantity is null && s.Quantity is { } quantity && s.Unit is { } unit)
            {
                alias.Quantity = quantity;
                alias.Unit = unit;
            }
            alias.UpdatedAt = now;
            accepted.Add(new AcceptedPriceAliasDto(id, foodId, s.FoodName!, alias.Quantity, alias.Unit));
        }

        if (accepted.Count > 0) await _context.SaveChangesAsync(cancellationToken);
        return new AcceptPriceAliasSuggestionsResult(accepted, skipped);
    }
}
