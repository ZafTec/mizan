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

    public GetPriceAliasesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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
