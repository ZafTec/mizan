using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetAchievementsQuery : IRequest<GetAchievementsResult>, IPagedQuery, ISortableQuery
{
    public string? Category { get; init; }
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
}

public record AchievementDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public int Points { get; init; }
    public string? Category { get; init; }
    public bool IsEarned { get; init; }
    public DateTime? EarnedAt { get; init; }
}

public class GetAchievementsQueryHandler : IRequestHandler<GetAchievementsQuery, GetAchievementsResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAchievementsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetAchievementsResult> Handle(GetAchievementsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var query = _context.Achievements.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(a => a.Category == request.Category);
        }

        var userAchievements = await _context.UserAchievements
            .Where(ua => ua.UserId == _currentUser.UserId)
            .ToListAsync(cancellationToken);

        var userAchievementDict = userAchievements.ToDictionary(ua => ua.AchievementId, ua => ua.EarnedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var allAchievements = await query
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Points)
            .ApplyPaging(request)
            .Select(a => new AchievementDto
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                IconUrl = a.IconUrl,
                Points = a.Points,
                Category = a.Category,
                IsEarned = userAchievementDict.ContainsKey(a.Id),
                EarnedAt = userAchievementDict.ContainsKey(a.Id) ? userAchievementDict[a.Id] : null
            })
            .ToListAsync(cancellationToken);

        return new GetAchievementsResult
        {
            Items = allAchievements,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPoints = allAchievements.Where(a => a.IsEarned).Sum(a => a.Points),
            EarnedCount = allAchievements.Count(a => a.IsEarned)
        };
    }
}
