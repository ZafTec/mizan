using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;
using Mizan.Domain.Entities;

namespace Mizan.Application.OAuth;

public record McpConnectionHouseholdDto(Guid Id, string Name);

/// <summary>One app a user connected, as the MCP page lists it.</summary>
public record McpConnectionDto
{
    public Guid Id { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? VerifiedHost { get; init; }
    public string? LogoUri { get; init; }
    public bool IsFirstParty { get; init; }
    public List<string> Scopes { get; init; } = new();
    public string HouseholdMode { get; init; } = OAuthGrant.HouseholdsNone;
    public List<McpConnectionHouseholdDto> Households { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public int Calls30Days { get; init; }
    public int Failed30Days { get; init; }
}

public record ListMcpConnectionsQuery : IRequest<List<McpConnectionDto>>;

public class ListMcpConnectionsQueryHandler : IRequestHandler<ListMcpConnectionsQuery, List<McpConnectionDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ListMcpConnectionsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<McpConnectionDto>> Handle(ListMcpConnectionsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var since = DateTime.UtcNow.AddDays(-30);

        var grants = await _context.OAuthGrants.AsNoTracking()
            .Where(g => g.UserId == userId && g.RevokedAt == null)
            .Include(g => g.Client)
            .OrderByDescending(g => g.LastUsedAt ?? g.CreatedAt)
            .ToListAsync(cancellationToken);

        var usage = await _context.McpUsageLogs.AsNoTracking()
            .Where(l => l.UserId == userId && l.GrantId != null && l.Timestamp >= since)
            .GroupBy(l => l.GrantId!.Value)
            .Select(g => new { GrantId = g.Key, Calls = g.Count(), Failed = g.Count(l => !l.Success) })
            .ToDictionaryAsync(g => g.GrantId, cancellationToken);

        var householdIds = grants.SelectMany(g => g.HouseholdIds).Distinct().ToList();
        var names = householdIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Households.AsNoTracking()
                .Where(h => householdIds.Contains(h.Id))
                .ToDictionaryAsync(h => h.Id, h => h.Name, cancellationToken);

        return grants.Select(g => new McpConnectionDto
        {
            Id = g.Id,
            ClientName = g.Client.Name,
            Source = g.Client.Source,
            VerifiedHost = g.Client.VerifiedHost,
            LogoUri = g.Client.LogoUri,
            IsFirstParty = g.Client.IsFirstParty,
            Scopes = g.Scopes,
            HouseholdMode = g.HouseholdMode,
            Households = g.HouseholdIds
                .Where(names.ContainsKey)
                .Select(id => new McpConnectionHouseholdDto(id, names[id]))
                .ToList(),
            CreatedAt = g.CreatedAt,
            LastUsedAt = g.LastUsedAt,
            Calls30Days = usage.TryGetValue(g.Id, out var u) ? u.Calls : 0,
            Failed30Days = usage.TryGetValue(g.Id, out var f) ? f.Failed : 0,
        }).ToList();
    }
}

/// <summary>
/// Narrows what a connected app may do. It can only remove access: asking for
/// more means the app has to go through consent again, where the user sees what
/// they are agreeing to. Tokens read the grant on every request, so a reduction
/// takes effect on the app's next call.
/// </summary>
public record UpdateMcpConnectionCommand(
    Guid Id,
    IReadOnlyList<string>? Scopes,
    string? HouseholdMode,
    IReadOnlyList<Guid>? HouseholdIds) : IRequest<Unit>;

public class UpdateMcpConnectionCommandHandler : IRequestHandler<UpdateMcpConnectionCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateMcpConnectionCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateMcpConnectionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var grant = await _context.OAuthGrants.Include(g => g.Client)
            .FirstOrDefaultAsync(g => g.Id == request.Id && g.UserId == userId && g.RevokedAt == null, cancellationToken)
            ?? throw new EntityNotFoundException("Connection", request.Id);

        if (grant.Client.IsFirstParty)
            throw new DomainValidationException("This is a Mizan app. Sign out of it instead of changing its access.");

        if (request.Scopes is not null)
        {
            var scopes = McpScopes.Normalize(request.Scopes, _currentUser.IsInRole("admin"));
            if (scopes.Count == 0)
                throw new DomainValidationException("Keep at least one permission, or disconnect the app.");
            if (scopes.Any(s => !McpScopes.Allows(grant.Scopes, s)))
                throw new DomainValidationException("A connection can only lose access here. To add access, connect the app again.");
            grant.Scopes = scopes;
        }

        if (request.HouseholdMode is not null)
        {
            var oldRank = Rank(grant.HouseholdMode);
            var mode = request.HouseholdMode;
            if (mode is not (OAuthGrant.HouseholdsNone or OAuthGrant.HouseholdsSelected or OAuthGrant.HouseholdsAll))
                throw new DomainValidationException("Unknown household setting.");
            if (Rank(mode) > oldRank)
                throw new DomainValidationException("A connection can only lose access here. To add access, connect the app again.");

            var ids = mode == OAuthGrant.HouseholdsSelected ? (request.HouseholdIds ?? []).Distinct().ToList() : new List<Guid>();
            if (mode == OAuthGrant.HouseholdsSelected)
            {
                var allowed = grant.HouseholdMode == OAuthGrant.HouseholdsAll
                    ? await _context.HouseholdMembers.Where(m => m.UserId == userId).Select(m => m.HouseholdId).ToListAsync(cancellationToken)
                    : grant.HouseholdIds;
                if (ids.Any(id => !allowed.Contains(id)))
                    throw new DomainValidationException("A connection can only lose access here. To add access, connect the app again.");
                if (ids.Count == 0) mode = OAuthGrant.HouseholdsNone;
            }

            grant.HouseholdMode = mode;
            grant.HouseholdIds = ids;
        }

        grant.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private static int Rank(string mode) => mode switch
    {
        OAuthGrant.HouseholdsAll => 2,
        OAuthGrant.HouseholdsSelected => 1,
        _ => 0,
    };
}

/// <summary>Disconnects an app. Every token it holds stops working at once.</summary>
public record RevokeMcpConnectionCommand(Guid Id) : IRequest<Unit>;

public class RevokeMcpConnectionCommandHandler : IRequestHandler<RevokeMcpConnectionCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RevokeMcpConnectionCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RevokeMcpConnectionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;

        var revoked = await _context.OAuthGrants
            .Where(g => g.Id == request.Id && g.UserId == userId && g.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, now).SetProperty(g => g.UpdatedAt, now), cancellationToken);
        if (revoked == 0) throw new EntityNotFoundException("Connection", request.Id);

        await _context.OAuthTokens
            .Where(t => t.GrantId == request.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        return Unit.Value;
    }
}
