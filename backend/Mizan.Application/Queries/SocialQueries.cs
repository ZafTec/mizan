using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record SocialProfileDto(Guid UserId, string DisplayName, string? Bio, string? AvatarUrl, bool DefaultPublishWorkouts, string? ShareToken);
public record GetMySocialProfileQuery : IRequest<SocialProfileDto?>;
public sealed class GetMySocialProfileQueryHandler : IRequestHandler<GetMySocialProfileQuery, SocialProfileDto?>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetMySocialProfileQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public Task<SocialProfileDto?> Handle(GetMySocialProfileQuery request, CancellationToken ct)
    {
        var id = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return _context.SocialProfiles.Where(p => p.UserId == id).Select(p => new SocialProfileDto(p.UserId, p.DisplayName, p.Bio, p.AvatarUrl, p.DefaultPublishWorkouts, p.ShareToken)).FirstOrDefaultAsync(ct);
    }
}

public record GetSharedSocialProfileQuery(string Token) : IRequest<SocialProfileDto?>;
public sealed class GetSharedSocialProfileQueryHandler : IRequestHandler<GetSharedSocialProfileQuery, SocialProfileDto?>
{
    private readonly IMizanDbContext _context; public GetSharedSocialProfileQueryHandler(IMizanDbContext context) => _context = context;
    public Task<SocialProfileDto?> Handle(GetSharedSocialProfileQuery request, CancellationToken ct) => _context.SocialProfiles.Where(p => p.ShareToken == request.Token)
        .Select(p => new SocialProfileDto(p.UserId, p.DisplayName, null, p.AvatarUrl, false, null)).FirstOrDefaultAsync(ct);
}

public record FollowDto(Guid Id, Guid UserId, string DisplayName, string? AvatarUrl, string Status, DateTime CreatedAt, DateTime? RespondedAt);
public record GetFollowsQuery(string Direction = "in", string? Status = null) : IRequest<IReadOnlyList<FollowDto>>;
public sealed class GetFollowsQueryHandler : IRequestHandler<GetFollowsQuery, IReadOnlyList<FollowDto>>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetFollowsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<IReadOnlyList<FollowDto>> Handle(GetFollowsQuery request, CancellationToken ct)
    {
        var id = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var incoming = !string.Equals(request.Direction, "out", StringComparison.OrdinalIgnoreCase);
        var query = _context.Follows.Where(f => incoming ? f.FolloweeUserId == id : f.FollowerUserId == id);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(f => f.Status == request.Status);
        return await query.OrderByDescending(f => f.CreatedAt).Select(f => new FollowDto(f.Id,
            incoming ? f.FollowerUserId : f.FolloweeUserId,
            incoming ? (f.Follower.Name ?? f.Follower.Email) : (f.Followee.Name ?? f.Followee.Email),
            incoming ? f.Follower.Image : f.Followee.Image, f.Status, f.CreatedAt, f.RespondedAt)).ToListAsync(ct);
    }
}

public record WorkoutFeedExerciseDto(string Name, string? MuscleGroup, int Sets, decimal TopWeightKg, int TopReps);
public record WorkoutFeedSummaryDto(Guid Id, string Name, DateOnly Date, int? DurationMinutes, decimal TotalVolumeKg, IReadOnlyList<WorkoutFeedExerciseDto> Exercises);
public record FeedReactionDto(Guid Id, Guid UserId, string Emoji);
public record FeedCommentDto(Guid Id, Guid UserId, string DisplayName, string Body, DateTime CreatedAt);
public record FeedItemDto(Guid Id, Guid UserId, string DisplayName, string? AvatarUrl, string Type, string? Caption, DateTime CreatedAt,
    WorkoutFeedSummaryDto? Workout, IReadOnlyList<FeedReactionDto> Reactions, IReadOnlyList<FeedCommentDto> Comments);
public record SocialFeedResult(IReadOnlyList<FeedItemDto> Items, int TotalCount, int Page, int PageSize);
public record GetSocialFeedQuery(int Page = 1, int PageSize = 20) : IRequest<SocialFeedResult>;
public sealed class GetSocialFeedQueryHandler : IRequestHandler<GetSocialFeedQuery, SocialFeedResult>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetSocialFeedQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<SocialFeedResult> Handle(GetSocialFeedQuery request, CancellationToken ct)
    {
        var id = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var hasProfile = await _context.SocialProfiles.AnyAsync(p => p.UserId == id, ct);
        if (!hasProfile) return new SocialFeedResult([], 0, 1, Math.Clamp(request.PageSize, 1, 100));
        var allowed = _context.Follows.Where(f => f.FollowerUserId == id && f.Status == "Accepted").Select(f => f.FolloweeUserId);
        var query = _context.FeedItems.Where(item => item.UserId == id || allowed.Contains(item.UserId));
        var total = await query.CountAsync(ct); var page = Math.Max(1, request.Page); var size = Math.Clamp(request.PageSize, 1, 100);
        var items = await query.OrderByDescending(item => item.CreatedAt).Skip((page - 1) * size).Take(size)
            .Include(item => item.User).Include(item => item.Workout)!.ThenInclude(workout => workout.Exercises).ThenInclude(exercise => exercise.Exercise)
            .Include(item => item.Workout)!.ThenInclude(workout => workout.Exercises).ThenInclude(exercise => exercise.Sets)
            .Include(item => item.Reactions).Include(item => item.Comments).ThenInclude(comment => comment.User).ToListAsync(ct);
        return new SocialFeedResult(items.Select(item => new FeedItemDto(item.Id, item.UserId, item.User.Name ?? item.User.Email, item.User.Image,
            item.Type, item.Caption, item.CreatedAt, item.Workout is null ? null : new WorkoutFeedSummaryDto(item.Workout.Id,
                item.Workout.Name ?? "Workout", item.Workout.WorkoutDate, item.Workout.DurationMinutes,
                item.Workout.Exercises.SelectMany(e => e.Sets).Sum(s => (s.WeightKg ?? 0) * (s.Reps ?? 0)),
                item.Workout.Exercises.OrderBy(e => e.SortOrder).Select(e => new WorkoutFeedExerciseDto(e.Exercise.Name, e.Exercise.MuscleGroup,
                    e.Sets.Count, e.Sets.Max(s => s.WeightKg ?? 0), e.Sets.Max(s => s.Reps ?? 0))).ToList()),
            item.Reactions.Select(r => new FeedReactionDto(r.Id, r.UserId, r.Emoji)).ToList(),
            item.Comments.Where(c => c.DeletedAt == null).OrderBy(c => c.CreatedAt).Select(c => new FeedCommentDto(c.Id, c.UserId,
                c.User.Name ?? c.User.Email, c.Body, c.CreatedAt)).ToList())).ToList(), total, page, size);
    }
}

public record ContentReportDto(Guid Id, Guid? ReporterUserId, string TargetType, Guid TargetId, string Reason, string Status, DateTime CreatedAt, DateTime? ResolvedAt, Guid? ResolvedByUserId, string? ResolutionNote);
public record ContentReportListResult(IReadOnlyList<ContentReportDto> Items, int TotalCount, int Page, int PageSize);
public record GetContentReportsQuery(string Status = "Open", int Page = 1, int PageSize = 20) : IRequest<ContentReportListResult>;
public sealed class GetContentReportsQueryHandler : IRequestHandler<GetContentReportsQuery, ContentReportListResult>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetContentReportsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<ContentReportListResult> Handle(GetContentReportsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsInRole("admin")) throw new UnauthorizedAccessException();
        var query = _context.ContentReports.Where(r => r.Status == request.Status); var total = await query.CountAsync(ct);
        var items = await query.OrderBy(r => r.CreatedAt).Skip((Math.Max(1, request.Page) - 1) * Math.Clamp(request.PageSize, 1, 100)).Take(Math.Clamp(request.PageSize, 1, 100))
            .Select(r => new ContentReportDto(r.Id, r.ReporterUserId, r.TargetType, r.TargetId, r.Reason, r.Status, r.CreatedAt, r.ResolvedAt, r.ResolvedByUserId, r.ResolutionNote)).ToListAsync(ct);
        return new ContentReportListResult(items, total, Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100));
    }
}

public record SocialAnalyticsDto(int Profiles, int PendingFollows, int AcceptedFollows, int FeedItems, int OpenReports, int ActionedReports);
public record GetSocialAnalyticsQuery : IRequest<SocialAnalyticsDto>;
public sealed class GetSocialAnalyticsQueryHandler : IRequestHandler<GetSocialAnalyticsQuery, SocialAnalyticsDto>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetSocialAnalyticsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<SocialAnalyticsDto> Handle(GetSocialAnalyticsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsInRole("admin")) throw new UnauthorizedAccessException();
        return new SocialAnalyticsDto(await _context.SocialProfiles.CountAsync(ct), await _context.Follows.CountAsync(f => f.Status == "Pending", ct),
            await _context.Follows.CountAsync(f => f.Status == "Accepted", ct), await _context.FeedItems.CountAsync(ct),
            await _context.ContentReports.CountAsync(r => r.Status == "Open", ct), await _context.ContentReports.CountAsync(r => r.Status == "Actioned", ct));
    }
}
