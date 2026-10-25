using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;
using Mizan.Domain.Features;

namespace Mizan.Application.Features;

/// <summary>Every catalog flag with the default in force and its override counts (docs/ADMIN.md#feature-flags).</summary>
public record ListFeatureFlagsQuery : IRequest<IReadOnlyList<FeatureFlagDto>>;

/// <summary>The households that have their own answer for one flag.</summary>
public record GetFeatureFlagOverridesQuery(string Flag) : IRequest<IReadOnlyList<FeatureFlagOverrideDto>>;

/// <summary>Sets a flag's default for every household without an override.</summary>
public record SetFeatureFlagDefaultCommand(string Flag, bool Enabled) : IRequest<FeatureFlagDto>;

/// <summary>Gives one household its own answer for one flag.</summary>
public record SetFeatureFlagOverrideCommand(string Flag, Guid HouseholdId, bool Enabled) : IRequest<FeatureFlagOverrideDto>;

/// <summary>Puts a household back on the default.</summary>
public record ClearFeatureFlagOverrideCommand(string Flag, Guid HouseholdId) : IRequest<Unit>;

internal static class FeatureFlagCatalog
{
    public static FeatureFlagDefinition Require(string flag) =>
        FeatureFlags.Find(flag) ?? throw new EntityNotFoundException($"There is no feature flag named '{flag}'.");

    public static async Task<FeatureFlagDto> DescribeAsync(IMizanDbContext context, FeatureFlagDefinition definition, CancellationToken ct)
    {
        var stored = await context.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Key == definition.Key, ct);
        var counts = await context.FeatureFlagOverrides.AsNoTracking()
            .Where(o => o.FlagKey == definition.Key)
            .GroupBy(o => o.Enabled)
            .Select(g => new { Enabled = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return ToDto(definition, stored, counts.Where(c => c.Enabled).Sum(c => c.Count), counts.Where(c => !c.Enabled).Sum(c => c.Count));
    }

    public static FeatureFlagDto ToDto(FeatureFlagDefinition definition, FeatureFlag? stored, int enabled, int disabled) => new()
    {
        Key = definition.Key,
        Description = definition.Description,
        DefaultEnabled = FeatureFlags.Resolve(definition.DefaultEnabled, stored?.DefaultEnabled, null),
        CatalogDefault = definition.DefaultEnabled,
        OverrideCount = enabled + disabled,
        EnabledOverrides = enabled,
        DisabledOverrides = disabled,
        UpdatedAt = stored?.UpdatedAt,
    };

    public static Task InvalidateAsync(HybridCache cache, CancellationToken ct) =>
        cache.RemoveByTagAsync(CacheTags.FeatureFlags, ct).AsTask();
}

public class ListFeatureFlagsQueryHandler : IRequestHandler<ListFeatureFlagsQuery, IReadOnlyList<FeatureFlagDto>>
{
    private readonly IMizanDbContext _context;

    public ListFeatureFlagsQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<IReadOnlyList<FeatureFlagDto>> Handle(ListFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var stored = await _context.FeatureFlags.AsNoTracking().ToDictionaryAsync(f => f.Key, cancellationToken);
        var counts = await _context.FeatureFlagOverrides.AsNoTracking()
            .GroupBy(o => new { o.FlagKey, o.Enabled })
            .Select(g => new { g.Key.FlagKey, g.Key.Enabled, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return FeatureFlags.All
            .Select(definition => FeatureFlagCatalog.ToDto(
                definition,
                stored.GetValueOrDefault(definition.Key),
                counts.Where(c => c.FlagKey == definition.Key && c.Enabled).Sum(c => c.Count),
                counts.Where(c => c.FlagKey == definition.Key && !c.Enabled).Sum(c => c.Count)))
            .ToList();
    }
}

public class GetFeatureFlagOverridesQueryHandler : IRequestHandler<GetFeatureFlagOverridesQuery, IReadOnlyList<FeatureFlagOverrideDto>>
{
    private readonly IMizanDbContext _context;

    public GetFeatureFlagOverridesQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<IReadOnlyList<FeatureFlagOverrideDto>> Handle(GetFeatureFlagOverridesQuery request, CancellationToken cancellationToken)
    {
        var definition = FeatureFlagCatalog.Require(request.Flag);

        return await _context.FeatureFlagOverrides.AsNoTracking()
            .Where(o => o.FlagKey == definition.Key)
            .OrderBy(o => o.Household!.Name)
            .Select(o => new FeatureFlagOverrideDto
            {
                HouseholdId = o.HouseholdId,
                HouseholdName = o.Household!.Name,
                MemberCount = _context.HouseholdMembers.Count(m => m.HouseholdId == o.HouseholdId),
                Enabled = o.Enabled,
                UpdatedAt = o.UpdatedAt,
                UpdatedByName = _context.Users.Where(u => u.Id == o.UpdatedByUserId).Select(u => u.Name ?? u.Email).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
    }
}

public class SetFeatureFlagDefaultCommandHandler : IRequestHandler<SetFeatureFlagDefaultCommand, FeatureFlagDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public SetFeatureFlagDefaultCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<FeatureFlagDto> Handle(SetFeatureFlagDefaultCommand request, CancellationToken cancellationToken)
    {
        var definition = FeatureFlagCatalog.Require(request.Flag);

        var stored = await _context.FeatureFlags.FirstOrDefaultAsync(f => f.Key == definition.Key, cancellationToken);
        if (stored is null)
        {
            stored = new FeatureFlag { Key = definition.Key };
            _context.FeatureFlags.Add(stored);
        }

        stored.Description = definition.Description;
        stored.DefaultEnabled = request.Enabled;
        stored.UpdatedAt = DateTime.UtcNow;
        stored.UpdatedByUserId = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
        await FeatureFlagCatalog.InvalidateAsync(_cache, cancellationToken);

        return await FeatureFlagCatalog.DescribeAsync(_context, definition, cancellationToken);
    }
}

public class SetFeatureFlagOverrideCommandHandler : IRequestHandler<SetFeatureFlagOverrideCommand, FeatureFlagOverrideDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public SetFeatureFlagOverrideCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<FeatureFlagOverrideDto> Handle(SetFeatureFlagOverrideCommand request, CancellationToken cancellationToken)
    {
        var definition = FeatureFlagCatalog.Require(request.Flag);
        var household = await _context.Households.AsNoTracking()
            .Where(h => h.Id == request.HouseholdId)
            .Select(h => new { h.Id, h.Name, Members = h.Members.Count })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Household", request.HouseholdId);

        var row = await _context.FeatureFlagOverrides
            .FirstOrDefaultAsync(o => o.FlagKey == definition.Key && o.HouseholdId == household.Id, cancellationToken);
        if (row is null)
        {
            row = new FeatureFlagOverride { Id = Guid.CreateVersion7(), FlagKey = definition.Key, HouseholdId = household.Id };
            _context.FeatureFlagOverrides.Add(row);
        }

        row.Enabled = request.Enabled;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedByUserId = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
        await FeatureFlagCatalog.InvalidateAsync(_cache, cancellationToken);

        var by = await _context.Users.AsNoTracking()
            .Where(u => u.Id == row.UpdatedByUserId)
            .Select(u => u.Name ?? u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        return new FeatureFlagOverrideDto
        {
            HouseholdId = household.Id,
            HouseholdName = household.Name,
            MemberCount = household.Members,
            Enabled = row.Enabled,
            UpdatedAt = row.UpdatedAt,
            UpdatedByName = by,
        };
    }
}

public class ClearFeatureFlagOverrideCommandHandler : IRequestHandler<ClearFeatureFlagOverrideCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public ClearFeatureFlagOverrideCommandHandler(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Unit> Handle(ClearFeatureFlagOverrideCommand request, CancellationToken cancellationToken)
    {
        var definition = FeatureFlagCatalog.Require(request.Flag);
        var removed = await _context.FeatureFlagOverrides
            .Where(o => o.FlagKey == definition.Key && o.HouseholdId == request.HouseholdId)
            .ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
        {
            throw new EntityNotFoundException("That household has no override for this flag.");
        }

        await FeatureFlagCatalog.InvalidateAsync(_cache, cancellationToken);
        return Unit.Value;
    }
}
