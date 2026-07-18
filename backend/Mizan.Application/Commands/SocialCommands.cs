using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public static class SocialReactionSet
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string> { "👍", "❤️", "💪", "🔥", "👏", "🎉", "🏆" };
}

public record SaveSocialProfileCommand(string DisplayName, string? Bio, string? AvatarUrl, bool DefaultPublishWorkouts) : IRequest;
public sealed class SaveSocialProfileCommandValidator : AbstractValidator<SaveSocialProfileCommand>
{
    public SaveSocialProfileCommandValidator() { RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100); RuleFor(x => x.Bio).MaximumLength(200); RuleFor(x => x.AvatarUrl).MaximumLength(500); }
}
public sealed class SaveSocialProfileCommandHandler : IRequestHandler<SaveSocialProfileCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public SaveSocialProfileCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(SaveSocialProfileCommand request, CancellationToken ct)
    {
        var id = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var profile = await _context.SocialProfiles.FirstOrDefaultAsync(p => p.UserId == id, ct);
        if (profile is null) { profile = new SocialProfile { UserId = id, ShareToken = Token(), CreatedAt = DateTime.UtcNow }; _context.SocialProfiles.Add(profile); }
        profile.DisplayName = request.DisplayName; profile.Bio = request.Bio; profile.AvatarUrl = request.AvatarUrl;
        profile.DefaultPublishWorkouts = request.DefaultPublishWorkouts; profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }
    internal static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
public record RotateSocialShareTokenCommand : IRequest<string>;
public sealed class RotateSocialShareTokenCommandHandler : IRequestHandler<RotateSocialShareTokenCommand, string>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public RotateSocialShareTokenCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<string> Handle(RotateSocialShareTokenCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); var p = await _context.SocialProfiles.FirstOrDefaultAsync(x => x.UserId == id, ct) ?? throw new InvalidOperationException("Social profile not found"); p.ShareToken = SaveSocialProfileCommandHandler.Token(); p.UpdatedAt = DateTime.UtcNow; await _context.SaveChangesAsync(ct); return p.ShareToken; }
}
public record DeleteSocialProfileCommand : IRequest;
public sealed class DeleteSocialProfileCommandHandler : IRequestHandler<DeleteSocialProfileCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public DeleteSocialProfileCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(DeleteSocialProfileCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); var p = await _context.SocialProfiles.FirstOrDefaultAsync(x => x.UserId == id, ct); if (p is null) return; await _context.Follows.Where(f => f.FollowerUserId == id || f.FolloweeUserId == id).ExecuteDeleteAsync(ct); await _context.FeedItems.Where(f => f.UserId == id).ExecuteDeleteAsync(ct); _context.SocialProfiles.Remove(p); await _context.SaveChangesAsync(ct); }
}

public record RequestFollowCommand(string ShareToken) : IRequest<Guid>;
public sealed class RequestFollowCommandHandler : IRequestHandler<RequestFollowCommand, Guid>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser; private readonly INotificationWriter _notifications;
    public RequestFollowCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, INotificationWriter notifications) { _context = context; _currentUser = currentUser; _notifications = notifications; }
    public async Task<Guid> Handle(RequestFollowCommand request, CancellationToken ct)
    {
        var follower = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var followee = await _context.SocialProfiles.Where(p => p.ShareToken == request.ShareToken).Select(p => p.UserId).FirstOrDefaultAsync(ct);
        if (followee == Guid.Empty || followee == follower) throw new InvalidOperationException("Profile not found");
        var existing = await _context.Follows.FirstOrDefaultAsync(f => f.FollowerUserId == follower && f.FolloweeUserId == followee, ct);
        if (existing is not null) return existing.Id;
        var row = new Follow { Id = Guid.NewGuid(), FollowerUserId = follower, FolloweeUserId = followee, Status = "Pending", CreatedAt = DateTime.UtcNow };
        _context.Follows.Add(row); await _notifications.AddAsync(followee, "follow_request", "New follow request", linkUrl: "/social/profile", cancellationToken: ct); await _context.SaveChangesAsync(ct); return row.Id;
    }
}
public record RespondToFollowCommand(Guid Id, bool Accept) : IRequest;
public sealed class RespondToFollowCommandHandler : IRequestHandler<RespondToFollowCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser; private readonly INotificationWriter _notifications; private readonly IAchievementEvaluator _achievements;
    public RespondToFollowCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, INotificationWriter notifications, IAchievementEvaluator achievements) { _context = context; _currentUser = currentUser; _notifications = notifications; _achievements = achievements; }
    public async Task Handle(RespondToFollowCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); var f = await _context.Follows.FirstOrDefaultAsync(x => x.Id == request.Id && x.FolloweeUserId == id && x.Status == "Pending", ct) ?? throw new InvalidOperationException("Follow request not found"); if (request.Accept) { f.Status = "Accepted"; f.RespondedAt = DateTime.UtcNow; await _notifications.AddAsync(f.FollowerUserId, "follow_accepted", "Follow request accepted", linkUrl: "/social", cancellationToken: ct); } else _context.Follows.Remove(f); await _context.SaveChangesAsync(ct); if (request.Accept) await _achievements.EvaluateAsync(ct); }
}
public record DeleteFollowCommand(Guid Id) : IRequest;
public sealed class DeleteFollowCommandHandler : IRequestHandler<DeleteFollowCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public DeleteFollowCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(DeleteFollowCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); var f = await _context.Follows.FirstOrDefaultAsync(x => x.Id == request.Id && (x.FollowerUserId == id || x.FolloweeUserId == id), ct) ?? throw new InvalidOperationException("Follow not found"); _context.Follows.Remove(f); await _context.SaveChangesAsync(ct); }
}

public record PublishFeedItemCommand(string Type, Guid? WorkoutId, Guid? TemplateId, Guid? AchievementId, string? Caption) : IRequest<Guid>;
public sealed class PublishFeedItemCommandValidator : AbstractValidator<PublishFeedItemCommand>
{
    public PublishFeedItemCommandValidator() { RuleFor(x => x.Type).Must(v => new[] { "WorkoutCompleted", "AchievementUnlocked", "StreakMilestone", "TemplateShared" }.Contains(v)); RuleFor(x => x.Caption).MaximumLength(280); }
}
public sealed class PublishFeedItemCommandHandler : IRequestHandler<PublishFeedItemCommand, Guid>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser; private readonly IAchievementEvaluator _achievements;
    public PublishFeedItemCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, IAchievementEvaluator achievements) { _context = context; _currentUser = currentUser; _achievements = achievements; }
    public async Task<Guid> Handle(PublishFeedItemCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); if (!await _context.SocialProfiles.AnyAsync(p => p.UserId == id, ct)) throw new InvalidOperationException("Create a social profile first"); if (request.WorkoutId.HasValue && !await _context.Workouts.AnyAsync(w => w.Id == request.WorkoutId && w.UserId == id, ct)) throw new InvalidOperationException("Workout not found"); var row = new FeedItem { Id = Guid.NewGuid(), UserId = id, Type = request.Type, WorkoutId = request.WorkoutId, TemplateId = request.TemplateId, AchievementId = request.AchievementId, Caption = request.Caption, CreatedAt = DateTime.UtcNow }; _context.FeedItems.Add(row); await _context.SaveChangesAsync(ct); await _achievements.EvaluateAsync(ct); return row.Id; }
}
public record DeleteFeedItemCommand(Guid Id) : IRequest;
public sealed class DeleteFeedItemCommandHandler : IRequestHandler<DeleteFeedItemCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public DeleteFeedItemCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(DeleteFeedItemCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); var item = await _context.FeedItems.FirstOrDefaultAsync(x => x.Id == request.Id && (x.UserId == id || _currentUser.IsInRole("admin")), ct) ?? throw new InvalidOperationException("Feed item not found"); _context.FeedItems.Remove(item); await _context.SaveChangesAsync(ct); }
}

public record AddFeedReactionCommand(Guid FeedItemId, string Emoji) : IRequest<Guid>;
public sealed class AddFeedReactionCommandHandler : IRequestHandler<AddFeedReactionCommand, Guid>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser; private readonly INotificationWriter _notifications; private readonly IAchievementEvaluator _achievements;
    public AddFeedReactionCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, INotificationWriter notifications, IAchievementEvaluator achievements) { _context = context; _currentUser = currentUser; _notifications = notifications; _achievements = achievements; }
    public async Task<Guid> Handle(AddFeedReactionCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); if (!SocialReactionSet.Allowed.Contains(request.Emoji)) throw new InvalidOperationException("Unsupported reaction"); var item = await SocialAccess.FindAccessibleItem(_context, id, request.FeedItemId, ct); var existing = await _context.FeedReactions.FirstOrDefaultAsync(r => r.FeedItemId == request.FeedItemId && r.UserId == id && r.Emoji == request.Emoji, ct); if (existing is not null) return existing.Id; var row = new FeedReaction { Id = Guid.NewGuid(), FeedItemId = item.Id, UserId = id, Emoji = request.Emoji, CreatedAt = DateTime.UtcNow }; _context.FeedReactions.Add(row); if (item.UserId != id) await _notifications.AddAsync(item.UserId, "feed_reaction", "Someone reacted to your workout", linkUrl: "/social", cancellationToken: ct); await _context.SaveChangesAsync(ct); await _achievements.EvaluateAsync(ct); return row.Id; }
}
public record RemoveFeedReactionCommand(Guid FeedItemId, string Emoji) : IRequest;
public sealed class RemoveFeedReactionCommandHandler : IRequestHandler<RemoveFeedReactionCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public RemoveFeedReactionCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(RemoveFeedReactionCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); await _context.FeedReactions.Where(r => r.FeedItemId == request.FeedItemId && r.UserId == id && r.Emoji == request.Emoji).ExecuteDeleteAsync(ct); }
}

public record AddFeedCommentCommand(Guid FeedItemId, string Body) : IRequest<Guid>;
public sealed class AddFeedCommentCommandValidator : AbstractValidator<AddFeedCommentCommand> { public AddFeedCommentCommandValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(500); }
public sealed class AddFeedCommentCommandHandler : IRequestHandler<AddFeedCommentCommand, Guid>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser; private readonly INotificationWriter _notifications; private readonly IAchievementEvaluator _achievements;
    public AddFeedCommentCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, INotificationWriter notifications, IAchievementEvaluator achievements) { _context = context; _currentUser = currentUser; _notifications = notifications; _achievements = achievements; }
    public async Task<Guid> Handle(AddFeedCommentCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); var item = await SocialAccess.FindAccessibleItem(_context, id, request.FeedItemId, ct); var row = new FeedComment { Id = Guid.NewGuid(), FeedItemId = item.Id, UserId = id, Body = request.Body, CreatedAt = DateTime.UtcNow }; _context.FeedComments.Add(row); if (item.UserId != id) await _notifications.AddAsync(item.UserId, "feed_comment", "New comment on your workout", linkUrl: "/social", cancellationToken: ct); await _context.SaveChangesAsync(ct); await _achievements.EvaluateAsync(ct); return row.Id; }
}
public record DeleteFeedCommentCommand(Guid Id) : IRequest;
public sealed class DeleteFeedCommentCommandHandler : IRequestHandler<DeleteFeedCommentCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public DeleteFeedCommentCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(DeleteFeedCommentCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); var c = await _context.FeedComments.FirstOrDefaultAsync(x => x.Id == request.Id && (x.UserId == id || _currentUser.IsInRole("admin")), ct) ?? throw new InvalidOperationException("Comment not found"); c.DeletedAt = DateTime.UtcNow; c.DeletedByUserId = id; await _context.SaveChangesAsync(ct); }
}

public record ReportContentCommand(string TargetType, Guid TargetId, string Reason) : IRequest<Guid>;
public sealed class ReportContentCommandValidator : AbstractValidator<ReportContentCommand> { public ReportContentCommandValidator() { RuleFor(x => x.TargetType).Must(v => new[] { "FeedItem", "FeedComment", "SocialProfile", "Exercise" }.Contains(v)); RuleFor(x => x.Reason).NotEmpty().MaximumLength(500); } }
public sealed class ReportContentCommandHandler : IRequestHandler<ReportContentCommand, Guid>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public ReportContentCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<Guid> Handle(ReportContentCommand request, CancellationToken ct) { var id = _currentUser.UserId ?? throw new UnauthorizedAccessException(); var row = new ContentReport { Id = Guid.NewGuid(), ReporterUserId = id, TargetType = request.TargetType, TargetId = request.TargetId, Reason = request.Reason, CreatedAt = DateTime.UtcNow }; _context.ContentReports.Add(row); await _context.SaveChangesAsync(ct); return row.Id; }
}
public record ResolveContentReportCommand(Guid Id, string Action, string? Note) : IRequest;
public sealed class ResolveContentReportCommandHandler : IRequestHandler<ResolveContentReportCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser; private readonly INotificationWriter _notifications;
    public ResolveContentReportCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, INotificationWriter notifications) { _context = context; _currentUser = currentUser; _notifications = notifications; }
    public async Task Handle(ResolveContentReportCommand request, CancellationToken ct) { var admin = _currentUser.UserId ?? throw new UnauthorizedAccessException(); if (!_currentUser.IsInRole("admin")) throw new UnauthorizedAccessException(); var report = await _context.ContentReports.FirstOrDefaultAsync(r => r.Id == request.Id && r.Status == "Open", ct) ?? throw new InvalidOperationException("Report not found"); report.Status = request.Action == "dismiss" ? "Dismissed" : "Actioned"; report.ResolvedAt = DateTime.UtcNow; report.ResolvedByUserId = admin; report.ResolutionNote = request.Note; if (request.Action == "delete" && report.TargetType == "FeedComment") { var comment = await _context.FeedComments.FirstOrDefaultAsync(c => c.Id == report.TargetId, ct); if (comment is not null) { comment.DeletedAt = DateTime.UtcNow; comment.DeletedByUserId = admin; } } if (request.Action == "delete" && report.TargetType == "FeedItem") await _context.FeedItems.Where(i => i.Id == report.TargetId).ExecuteDeleteAsync(ct); if (report.ReporterUserId.HasValue) await _notifications.AddAsync(report.ReporterUserId.Value, "content_report_actioned", "Your report was reviewed", request.Note, "/social", ct); await _context.SaveChangesAsync(ct); }
}

internal static class SocialAccess
{
    public static async Task<FeedItem> FindAccessibleItem(IMizanDbContext context, Guid userId, Guid itemId, CancellationToken ct)
    {
        var item = await context.FeedItems.FirstOrDefaultAsync(i => i.Id == itemId, ct) ?? throw new InvalidOperationException("Feed item not found");
        if (item.UserId == userId) return item;
        var follows = await context.Follows.AnyAsync(f => f.FollowerUserId == userId && f.FolloweeUserId == item.UserId && f.Status == "Accepted", ct);
        if (!follows) throw new UnauthorizedAccessException();
        return item;
    }
}
