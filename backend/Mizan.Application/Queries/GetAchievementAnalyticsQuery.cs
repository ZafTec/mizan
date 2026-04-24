using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetAchievementAnalyticsQuery : IRequest<GetAchievementAnalyticsResult>, IPagedQuery, ISortableQuery
{
    public string? SearchTerm { get; init; }
    public string? Category { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record GetAchievementAnalyticsResult
{
    public int TotalAchievements { get; init; }
    public int TotalUsers { get; init; }
    public int TotalUnlocks { get; init; }
    public int UsersWithAtLeastOne { get; init; }
    public double AverageUnlocksPerUser { get; init; }
    public List<AchievementAnalyticsRow> Rows { get; init; } = new();
    public int RowsTotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)RowsTotalCount / PageSize) : 0;
    public List<CategoryBreakdown> Categories { get; init; } = new();
}

public record AchievementAnalyticsRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? CriteriaType { get; init; }
    public int Threshold { get; init; }
    public int Points { get; init; }
    public int UnlockedBy { get; init; }
    public double UnlockRate { get; init; }
    public DateTime? MostRecentUnlockAt { get; init; }
}

public record CategoryBreakdown
{
    public string Category { get; init; } = string.Empty;
    public int AchievementCount { get; init; }
    public int TotalUnlocks { get; init; }
    public int TotalPointsEarned { get; init; }
}

public class GetAchievementAnalyticsQueryHandler : IRequestHandler<GetAchievementAnalyticsQuery, GetAchievementAnalyticsResult>
{
    private readonly IMizanDbContext _context;

    public GetAchievementAnalyticsQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<GetAchievementAnalyticsResult> Handle(GetAchievementAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var totalAchievements = await _context.Achievements.CountAsync(cancellationToken);
        var totalUnlocks = await _context.UserAchievements.CountAsync(cancellationToken);
        var usersWithAtLeastOne = await _context.UserAchievements
            .Select(ua => ua.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Pull every achievement with its aggregated unlock stats once. Row count is
        // bounded by total achievements (typically <200), so filter/sort/paginate in
        // memory after the single DB hit, cheaper than repeating groupjoin per page.
        var perAchievement = await _context.Achievements
            .GroupJoin(
                _context.UserAchievements,
                a => a.Id,
                ua => ua.AchievementId,
                (a, uas) => new
                {
                    Achievement = a,
                    UnlockedBy = uas.Count(),
                    MostRecent = uas.OrderByDescending(x => x.EarnedAt).Select(x => (DateTime?)x.EarnedAt).FirstOrDefault()
                })
            .ToListAsync(cancellationToken);

        IEnumerable<AchievementAnalyticsRow> allRows = perAchievement.Select(x => new AchievementAnalyticsRow
        {
            Id = x.Achievement.Id,
            Name = x.Achievement.Name,
            Category = x.Achievement.Category,
            CriteriaType = x.Achievement.CriteriaType,
            Threshold = x.Achievement.Threshold,
            Points = x.Achievement.Points,
            UnlockedBy = x.UnlockedBy,
            UnlockRate = totalUsers > 0 ? Math.Round((double)x.UnlockedBy / totalUsers * 100, 1) : 0,
            MostRecentUnlockAt = x.MostRecent
        });

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var needle = request.SearchTerm.Trim().ToLowerInvariant();
            allRows = allRows.Where(r =>
                r.Name.ToLowerInvariant().Contains(needle) ||
                (r.Category ?? string.Empty).ToLowerInvariant().Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            allRows = allRows.Where(r => string.Equals(r.Category, request.Category, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = allRows.ToList();
        var rowsTotal = filtered.Count;

        var sortKey = (request.SortBy ?? "unlockedBy").ToLowerInvariant();
        var desc = !string.Equals(request.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        IEnumerable<AchievementAnalyticsRow> sorted = sortKey switch
        {
            "name" => desc ? filtered.OrderByDescending(r => r.Name) : filtered.OrderBy(r => r.Name),
            "category" => desc ? filtered.OrderByDescending(r => r.Category ?? string.Empty) : filtered.OrderBy(r => r.Category ?? string.Empty),
            "unlockrate" => desc ? filtered.OrderByDescending(r => r.UnlockRate) : filtered.OrderBy(r => r.UnlockRate),
            "mostrecentunlockat" => desc ? filtered.OrderByDescending(r => r.MostRecentUnlockAt ?? DateTime.MinValue) : filtered.OrderBy(r => r.MostRecentUnlockAt ?? DateTime.MinValue),
            "points" => desc ? filtered.OrderByDescending(r => r.Points) : filtered.OrderBy(r => r.Points),
            _ => desc ? filtered.OrderByDescending(r => r.UnlockedBy).ThenBy(r => r.Name) : filtered.OrderBy(r => r.UnlockedBy).ThenBy(r => r.Name),
        };

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Max(1, request.PageSize);
        var pagedRows = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var categories = perAchievement
            .GroupBy(x => x.Achievement.Category ?? "uncategorized")
            .Select(g => new CategoryBreakdown
            {
                Category = g.Key,
                AchievementCount = g.Count(),
                TotalUnlocks = g.Sum(x => x.UnlockedBy),
                TotalPointsEarned = g.Sum(x => x.UnlockedBy * x.Achievement.Points)
            })
            .OrderByDescending(c => c.TotalUnlocks)
            .ToList();

        return new GetAchievementAnalyticsResult
        {
            TotalAchievements = totalAchievements,
            TotalUsers = totalUsers,
            TotalUnlocks = totalUnlocks,
            UsersWithAtLeastOne = usersWithAtLeastOne,
            AverageUnlocksPerUser = totalUsers > 0 ? Math.Round((double)totalUnlocks / totalUsers, 2) : 0,
            Rows = pagedRows,
            RowsTotalCount = rowsTotal,
            Page = page,
            PageSize = pageSize,
            Categories = categories
        };
    }
}
