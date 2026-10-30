using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Queries;

// Admin-only. Lists every household with counts for the /admin/households page.
// Search matches the household's name or any member's email or name.
public record GetAllHouseholdsQuery : IRequest<PagedResult<AdminHouseholdSummary>>, IPagedQuery, ISortableQuery
{
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record AdminHouseholdSummary(
    Guid Id,
    string Name,
    Guid CreatedBy,
    string? CreatedByName,
    string? CreatedByEmail,
    DateTime CreatedAt,
    int MemberCount,
    int PendingInviteCount
);

public class GetAllHouseholdsQueryHandler : IRequestHandler<GetAllHouseholdsQuery, PagedResult<AdminHouseholdSummary>>
{
    private static readonly Dictionary<string, Expression<Func<Household, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = h => h.Name,
        ["createdat"] = h => h.CreatedAt,
        ["members"] = h => h.Members.Count
    };

    private readonly IMizanDbContext _context;

    public GetAllHouseholdsQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AdminHouseholdSummary>> Handle(GetAllHouseholdsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Households.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var needle = request.Search.Trim().ToLower();
            query = query.Where(h => h.Name.ToLower().Contains(needle)
                || _context.HouseholdMembers.Any(m => m.HouseholdId == h.Id
                    && (m.User.Email.ToLower().Contains(needle) || (m.User.Name != null && m.User.Name.ToLower().Contains(needle)))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sorted = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: h => h.CreatedAt,
            defaultDescending: true)
            .ThenBy(h => h.Id);

        var page = await sorted
            .ApplyPaging(request)
            .Select(h => new
            {
                h.Id,
                h.Name,
                h.CreatedBy,
                h.CreatedAt,
                MemberCount = _context.HouseholdMembers.Count(m => m.HouseholdId == h.Id),
                PendingInviteCount = _context.HouseholdInvitations.Count(i => i.HouseholdId == h.Id && i.Status == "pending")
            })
            .ToListAsync(cancellationToken);

        var creatorIds = page.Select(p => p.CreatedBy).Distinct().ToList();
        var creators = await _context.Users.AsNoTracking()
            .Where(u => creatorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var items = page.Select(p => new AdminHouseholdSummary(
            p.Id,
            p.Name,
            p.CreatedBy,
            creators.TryGetValue(p.CreatedBy, out var c) ? c.Name : null,
            creators.TryGetValue(p.CreatedBy, out var c2) ? c2.Email : null,
            p.CreatedAt,
            p.MemberCount,
            p.PendingInviteCount)).ToList();

        return new PagedResult<AdminHouseholdSummary>
        {
            Items = items,
            TotalCount = totalCount,
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
    }
}
