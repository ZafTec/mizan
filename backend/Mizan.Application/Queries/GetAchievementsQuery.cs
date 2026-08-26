using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetAchievementsQuery : IRequest<GetAchievementsResult>, IPagedQuery, ISortableQuery
{
    public string? Category { get; init; }
    public string? SearchTerm { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record GetAchievementsResult
{
    public List<AchievementDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public int TotalPoints { get; init; }
    public int EarnedCount { get; init; }
    public int Level { get; init; }
    public string LevelName { get; init; } = "Rookie";
    public int LevelFloor { get; init; }
    public int? NextLevelAt { get; init; }
}

public record AchievementDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public int Points { get; init; }
    public string? Category { get; init; }
    public string? CriteriaType { get; init; }
    public int Threshold { get; init; }
    public int Progress { get; init; }
    public bool IsEarned { get; init; }
    public DateTime? EarnedAt { get; init; }
}

public class GetAchievementsQueryHandler : IRequestHandler<GetAchievementsQuery, GetAchievementsResult>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.Achievement, object>>> SortMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = a => a.Name,
            ["category"] = a => a.Category ?? string.Empty,
            ["points"] = a => a.Points,
            ["threshold"] = a => a.Threshold,
            ["criteriatype"] = a => a.CriteriaType ?? string.Empty,
        };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserStatsProvider _stats;

    public GetAchievementsQueryHandler(
        IMizanDbContext context, ICurrentUserService currentUser, IUserStatsProvider stats)
    {
        _context = context;
        _currentUser = currentUser;
        _stats = stats;
    }

    public async Task<GetAchievementsResult> Handle(GetAchievementsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var userId = _currentUser.UserId.Value;

        var query = _context.Achievements.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(a => a.Category == request.Category);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Name.ToLower().Contains(searchTerm) ||
                (a.Description != null && a.Description.ToLower().Contains(searchTerm)) ||
                (a.Category != null && a.Category.ToLower().Contains(searchTerm)));
        }

        var userAchievements = await _context.UserAchievements
            .Where(ua => ua.UserId == userId)
            .ToListAsync(cancellationToken);

        var userAchievementDict = userAchievements.ToDictionary(ua => ua.AchievementId, ua => ua.EarnedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        // Append Id as tie-breaker so pages don't overlap when many rows share the
        // primary sort key (e.g. 30 achievements with distinct categories but
        // several in each, without this the DB may return the same row twice).
        var sortedQuery = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: a => a.Category ?? string.Empty,
            defaultDescending: false)
            .ThenBy(a => a.Id);

        var pageAchievements = await sortedQuery
            .ApplyPaging(request)
            .ToListAsync(cancellationToken);

        // The same provider the evaluator uses. These were two separate
        // methods that disagreed, so a bar could sit at 30/30 without the
        // badge unlocking, or the reverse.
        var stats = await _stats.BuildAsync(
            userId,
            pageAchievements.Where(a => a.CriteriaType is not null).Select(a => a.CriteriaType!).ToHashSet(),
            cancellationToken);

        var items = pageAchievements.Select(a => new AchievementDto
        {
            Id = a.Id,
            Name = a.Name,
            Description = a.Description,
            IconUrl = a.IconUrl,
            Points = a.Points,
            Category = a.Category,
            CriteriaType = a.CriteriaType,
            Threshold = a.Threshold,
            Progress = (int)stats.Value(a.CriteriaType),
            IsEarned = userAchievementDict.ContainsKey(a.Id),
            EarnedAt = userAchievementDict.TryGetValue(a.Id, out var earnedAt) ? earnedAt : null
        }).ToList();

        var earnedPoints = items.Where(a => a.IsEarned).Sum(a => a.Points);
        var (level, levelName, levelFloor, nextLevelAt) = ComputeLevel(earnedPoints);

        return new GetAchievementsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPoints = earnedPoints,
            EarnedCount = items.Count(a => a.IsEarned),
            Level = level,
            LevelName = levelName,
            LevelFloor = levelFloor,
            NextLevelAt = nextLevelAt
        };
    }

    private static (int Level, string Name, int Floor, int? NextAt) ComputeLevel(int points) => points switch
    {
        < 100 => (1, "Rookie", 0, 100),
        < 500 => (2, "Bronze", 100, 500),
        < 1500 => (3, "Silver", 500, 1500),
        < 5000 => (4, "Gold", 1500, 5000),
        _ => (5, "Platinum", 5000, null)
    };
}
