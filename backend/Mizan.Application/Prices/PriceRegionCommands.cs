using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Prices;
using Mizan.Domain.Entities;

namespace Mizan.Application.Prices;

/// <summary>The administrator check every price administration handler makes, on top of the controller's policy.</summary>
internal static class PriceAdmin
{
    public static Guid Require(ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        if (!currentUser.IsInRole("admin")) throw new ForbiddenAccessException("Only an administrator can manage prices.");
        return userId;
    }

    public const string RegionCodePattern = "^[A-Z]{2}(-[A-Z0-9]{1,6})?$";
    public const string CurrencyPattern = "^[A-Z]{3}$";
}

/// <summary>The price regions. Everyone sees the enabled ones; <c>IncludeDisabled</c> is for administrators.</summary>
public record GetPriceRegionsQuery(bool IncludeDisabled = false) : IRequest<List<PriceRegionDto>>;

public class GetPriceRegionsQueryHandler : IRequestHandler<GetPriceRegionsQuery, List<PriceRegionDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPriceRegionsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<PriceRegionDto>> Handle(GetPriceRegionsQuery request, CancellationToken cancellationToken)
    {
        if (request.IncludeDisabled) PriceAdmin.Require(_currentUser);
        return await _context.PriceRegions.AsNoTracking()
            .Where(r => request.IncludeDisabled || r.Enabled)
            .OrderBy(r => r.Name)
            .Select(r => new PriceRegionDto(r.Code, r.Name, r.Currency, r.Enabled))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>A new market. The code is the country, a hyphen and a local code, for example ET-AA.</summary>
public record CreatePriceRegionCommand(string RegionCode, string Name, string Currency, bool Enabled) : IRequest<PriceRegionDto>, IRedactedAudit
{
    /// <summary>A region code is a place name, not a credential; the explicit details say so.</summary>
    object IRedactedAudit.AuditDetails => new { RegionCode, Name, Currency, Enabled };
}

public class CreatePriceRegionCommandValidator : AbstractValidator<CreatePriceRegionCommand>
{
    public CreatePriceRegionCommandValidator()
    {
        RuleFor(x => x.RegionCode).NotEmpty().MaximumLength(16).Matches(PriceAdmin.RegionCodePattern)
            .WithMessage("A region code is two capital letters, optionally a hyphen and a local code: ET-AA.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Currency).NotEmpty().Matches(PriceAdmin.CurrencyPattern).WithMessage("The currency is a three-letter ISO code: ETB.");
    }
}

public class CreatePriceRegionCommandHandler : IRequestHandler<CreatePriceRegionCommand, PriceRegionDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreatePriceRegionCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PriceRegionDto> Handle(CreatePriceRegionCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        if (await _context.PriceRegions.AnyAsync(r => r.Code == request.RegionCode, cancellationToken))
            throw new ConflictException($"The region {request.RegionCode} already exists.");

        var region = new PriceRegion
        {
            Code = request.RegionCode,
            Name = request.Name.Trim(),
            Currency = request.Currency,
            Enabled = request.Enabled,
            CreatedAt = DateTime.UtcNow,
        };
        _context.PriceRegions.Add(region);
        await _context.SaveChangesAsync(cancellationToken);
        return new PriceRegionDto(region.Code, region.Name, region.Currency, region.Enabled);
    }
}

/// <summary>Renames a region, changes its currency, or turns it off. People in a disabled region see the default region's prices.</summary>
public record UpdatePriceRegionCommand(string RegionCode, string Name, string Currency, bool Enabled) : IRequest<PriceRegionDto>, IRedactedAudit
{
    object IRedactedAudit.AuditDetails => new { RegionCode, Name, Currency, Enabled };
}

public class UpdatePriceRegionCommandValidator : AbstractValidator<UpdatePriceRegionCommand>
{
    public UpdatePriceRegionCommandValidator()
    {
        RuleFor(x => x.RegionCode).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Currency).NotEmpty().Matches(PriceAdmin.CurrencyPattern).WithMessage("The currency is a three-letter ISO code: ETB.");
    }
}

public class UpdatePriceRegionCommandHandler : IRequestHandler<UpdatePriceRegionCommand, PriceRegionDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public UpdatePriceRegionCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<PriceRegionDto> Handle(UpdatePriceRegionCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var region = await _context.PriceRegions.FirstOrDefaultAsync(r => r.Code == request.RegionCode, cancellationToken)
            ?? throw new EntityNotFoundException("PriceRegion", request.RegionCode);

        region.Name = request.Name.Trim();
        region.Currency = request.Currency;
        region.Enabled = request.Enabled;
        await _context.SaveChangesAsync(cancellationToken);
        await PriceBookReader.InvalidateAsync(_cache, cancellationToken);
        return new PriceRegionDto(region.Code, region.Name, region.Currency, region.Enabled);
    }
}
