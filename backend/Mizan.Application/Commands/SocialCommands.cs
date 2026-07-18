using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public static class SocialReactionSet
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string> { "👍", "❤️", "💪", "🔥", "👏", "🎉", "🏆" };
}

public record SaveSocialProfileCommand(
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    bool DefaultPublishWorkouts) : IRequest;

public sealed class SaveSocialProfileCommandValidator : AbstractValidator<SaveSocialProfileCommand>
{
    public SaveSocialProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Bio).MaximumLength(200);
        RuleFor(x => x.AvatarUrl).MaximumLength(500);
    }
}

public sealed class SaveSocialProfileCommandHandler : IRequestHandler<SaveSocialProfileCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaveSocialProfileCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(SaveSocialProfileCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var profile = await _context.SocialProfiles.FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (profile is null)
        {
            profile = new SocialProfile
            {
                UserId = userId,
                ShareToken = CreateToken(),
                CreatedAt = DateTime.UtcNow
            };
            _context.SocialProfiles.Add(profile);
        }

        profile.DisplayName = request.DisplayName;
        profile.Bio = request.Bio;
        profile.AvatarUrl = request.AvatarUrl;
        profile.DefaultPublishWorkouts = request.DefaultPublishWorkouts;
        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    internal static string CreateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

public record RotateSocialShareTokenCommand : IRequest<string>;

public sealed class RotateSocialShareTokenCommandHandler : IRequestHandler<RotateSocialShareTokenCommand, string>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RotateSocialShareTokenCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<string> Handle(RotateSocialShareTokenCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var profile = await _context.SocialProfiles.FirstOrDefaultAsync(item => item.UserId == userId, ct)
            ?? throw new EntityNotFoundException("Social profile not found");
        profile.ShareToken = SaveSocialProfileCommandHandler.CreateToken();
        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return profile.ShareToken;
    }
}

public record DeleteSocialProfileCommand : IRequest;

public sealed class DeleteSocialProfileCommandHandler : IRequestHandler<DeleteSocialProfileCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteSocialProfileCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteSocialProfileCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var profile = await _context.SocialProfiles.FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (profile is null) return;

        await _context.Follows
            .Where(follow => follow.FollowerUserId == userId || follow.FolloweeUserId == userId)
            .ExecuteDeleteAsync(ct);
        await _context.FeedItems.Where(item => item.UserId == userId).ExecuteDeleteAsync(ct);
        _context.SocialProfiles.Remove(profile);
        await _context.SaveChangesAsync(ct);
    }
}

public record RequestFollowCommand(string ShareToken) : IRequest<Guid>;

public sealed class RequestFollowCommandHandler : IRequestHandler<RequestFollowCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationWriter _notifications;

    public RequestFollowCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        INotificationWriter notifications)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Guid> Handle(RequestFollowCommand request, CancellationToken ct)
    {
        var followerUserId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var followeeUserId = await _context.SocialProfiles
            .Where(profile => profile.ShareToken == request.ShareToken)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(ct);
        if (followeeUserId == Guid.Empty || followeeUserId == followerUserId)
        {
            throw new EntityNotFoundException("Social profile not found");
        }

        var existing = await _context.Follows.FirstOrDefaultAsync(
            follow => follow.FollowerUserId == followerUserId && follow.FolloweeUserId == followeeUserId,
            ct);
        if (existing is not null) return existing.Id;

        var follow = new Follow
        {
            Id = Guid.NewGuid(),
            FollowerUserId = followerUserId,
            FolloweeUserId = followeeUserId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _context.Follows.Add(follow);
        await _notifications.AddAsync(
            followeeUserId,
            "follow_request",
            "New follow request",
            linkUrl: "/social/profile",
            cancellationToken: ct);
        await _context.SaveChangesAsync(ct);
        return follow.Id;
    }
}

public record RespondToFollowCommand(Guid Id, bool Accept) : IRequest;

public sealed class RespondToFollowCommandHandler : IRequestHandler<RespondToFollowCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationWriter _notifications;
    private readonly IAchievementEvaluator _achievements;

    public RespondToFollowCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        INotificationWriter notifications,
        IAchievementEvaluator achievements)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
        _achievements = achievements;
    }

    public async Task Handle(RespondToFollowCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var follow = await _context.Follows.FirstOrDefaultAsync(
            item => item.Id == request.Id && item.FolloweeUserId == userId && item.Status == "Pending",
            ct) ?? throw new EntityNotFoundException("Follow request not found");

        if (request.Accept)
        {
            follow.Status = "Accepted";
            follow.RespondedAt = DateTime.UtcNow;
            await _notifications.AddAsync(
                follow.FollowerUserId,
                "follow_accepted",
                "Follow request accepted",
                linkUrl: "/social",
                cancellationToken: ct);
        }
        else
        {
            _context.Follows.Remove(follow);
        }

        await _context.SaveChangesAsync(ct);
        if (request.Accept) await _achievements.EvaluateAsync(ct, ["followers_count"]);
    }
}

public record DeleteFollowCommand(Guid Id) : IRequest;

public sealed class DeleteFollowCommandHandler : IRequestHandler<DeleteFollowCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteFollowCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteFollowCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var follow = await _context.Follows.FirstOrDefaultAsync(
            item => item.Id == request.Id && (item.FollowerUserId == userId || item.FolloweeUserId == userId),
            ct) ?? throw new EntityNotFoundException("Follow not found");
        _context.Follows.Remove(follow);
        await _context.SaveChangesAsync(ct);
    }
}

public record PublishFeedItemCommand(
    string Type,
    Guid? WorkoutId,
    Guid? TemplateId,
    Guid? AchievementId,
    string? Caption) : IRequest<Guid>;

public sealed class PublishFeedItemCommandValidator : AbstractValidator<PublishFeedItemCommand>
{
    public PublishFeedItemCommandValidator()
    {
        RuleFor(x => x.Type)
            .Must(value => new[] { "WorkoutCompleted", "AchievementUnlocked", "StreakMilestone", "TemplateShared" }.Contains(value));
        RuleFor(x => x.Caption).MaximumLength(280);
        RuleFor(x => x.WorkoutId).NotEmpty().When(x => x.Type == "WorkoutCompleted");
        RuleFor(x => x.TemplateId).NotEmpty().When(x => x.Type == "TemplateShared");
        RuleFor(x => x.AchievementId).NotEmpty().When(x => x.Type == "AchievementUnlocked");
    }
}

public sealed class PublishFeedItemCommandHandler : IRequestHandler<PublishFeedItemCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAchievementEvaluator _achievements;

    public PublishFeedItemCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IAchievementEvaluator achievements)
    {
        _context = context;
        _currentUser = currentUser;
        _achievements = achievements;
    }

    public async Task<Guid> Handle(PublishFeedItemCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!await _context.SocialProfiles.AnyAsync(profile => profile.UserId == userId, ct))
        {
            throw new DomainValidationException("Create a social profile first");
        }
        if (request.WorkoutId.HasValue &&
            !await _context.Workouts.AnyAsync(workout => workout.Id == request.WorkoutId && workout.UserId == userId, ct))
        {
            throw new EntityNotFoundException("Workout not found");
        }
        if (request.TemplateId.HasValue &&
            !await _context.WorkoutTemplates.AnyAsync(
                template => template.Id == request.TemplateId && (template.IsBuiltIn || template.UserId == userId),
                ct))
        {
            throw new EntityNotFoundException("Workout template not found");
        }
        if (request.AchievementId.HasValue &&
            !await _context.UserAchievements.AnyAsync(
                achievement => achievement.UserId == userId && achievement.AchievementId == request.AchievementId,
                ct))
        {
            throw new EntityNotFoundException("Achievement not found");
        }

        var feedItem = new FeedItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = request.Type,
            WorkoutId = request.WorkoutId,
            TemplateId = request.TemplateId,
            AchievementId = request.AchievementId,
            Caption = request.Caption,
            CreatedAt = DateTime.UtcNow
        };
        _context.FeedItems.Add(feedItem);
        await _context.SaveChangesAsync(ct);
        await _achievements.EvaluateAsync(ct, ["workouts_shared"]);
        return feedItem.Id;
    }
}

public record DeleteFeedItemCommand(Guid Id) : IRequest;

public sealed class DeleteFeedItemCommandHandler : IRequestHandler<DeleteFeedItemCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteFeedItemCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteFeedItemCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var item = await _context.FeedItems.FirstOrDefaultAsync(
            feedItem => feedItem.Id == request.Id && (feedItem.UserId == userId || _currentUser.IsInRole("admin")),
            ct) ?? throw new EntityNotFoundException("Feed item not found");
        _context.FeedItems.Remove(item);
        await _context.SaveChangesAsync(ct);
    }
}

public record AddFeedReactionCommand(Guid FeedItemId, string Emoji) : IRequest<Guid>;

public sealed class AddFeedReactionCommandHandler : IRequestHandler<AddFeedReactionCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationWriter _notifications;
    private readonly IAchievementEvaluator _achievements;

    public AddFeedReactionCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        INotificationWriter notifications,
        IAchievementEvaluator achievements)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
        _achievements = achievements;
    }

    public async Task<Guid> Handle(AddFeedReactionCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!SocialReactionSet.Allowed.Contains(request.Emoji))
        {
            throw new DomainValidationException("Unsupported reaction");
        }

        var item = await SocialAccess.FindAccessibleItem(_context, userId, request.FeedItemId, ct);
        var existing = await _context.FeedReactions.FirstOrDefaultAsync(
            reaction => reaction.FeedItemId == request.FeedItemId &&
                reaction.UserId == userId &&
                reaction.Emoji == request.Emoji,
            ct);
        if (existing is not null) return existing.Id;

        var reaction = new FeedReaction
        {
            Id = Guid.NewGuid(),
            FeedItemId = item.Id,
            UserId = userId,
            Emoji = request.Emoji,
            CreatedAt = DateTime.UtcNow
        };
        _context.FeedReactions.Add(reaction);
        if (item.UserId != userId)
        {
            await _notifications.AddAsync(
                item.UserId,
                "feed_reaction",
                "Someone reacted to your workout",
                linkUrl: "/social",
                cancellationToken: ct);
        }
        await _context.SaveChangesAsync(ct);
        await _achievements.EvaluateAsync(ct, ["reactions_given"]);
        return reaction.Id;
    }
}

public record RemoveFeedReactionCommand(Guid FeedItemId, string Emoji) : IRequest;

public sealed class RemoveFeedReactionCommandHandler : IRequestHandler<RemoveFeedReactionCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveFeedReactionCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(RemoveFeedReactionCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        await _context.FeedReactions
            .Where(reaction => reaction.FeedItemId == request.FeedItemId &&
                reaction.UserId == userId &&
                reaction.Emoji == request.Emoji)
            .ExecuteDeleteAsync(ct);
    }
}

public record AddFeedCommentCommand(Guid FeedItemId, string Body) : IRequest<Guid>;

public sealed class AddFeedCommentCommandValidator : AbstractValidator<AddFeedCommentCommand>
{
    public AddFeedCommentCommandValidator()
        => RuleFor(x => x.Body).NotEmpty().MaximumLength(500);
}

public sealed class AddFeedCommentCommandHandler : IRequestHandler<AddFeedCommentCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationWriter _notifications;
    private readonly IAchievementEvaluator _achievements;

    public AddFeedCommentCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        INotificationWriter notifications,
        IAchievementEvaluator achievements)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
        _achievements = achievements;
    }

    public async Task<Guid> Handle(AddFeedCommentCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var item = await SocialAccess.FindAccessibleItem(_context, userId, request.FeedItemId, ct);
        var comment = new FeedComment
        {
            Id = Guid.NewGuid(),
            FeedItemId = item.Id,
            UserId = userId,
            Body = request.Body,
            CreatedAt = DateTime.UtcNow
        };
        _context.FeedComments.Add(comment);
        if (item.UserId != userId)
        {
            await _notifications.AddAsync(
                item.UserId,
                "feed_comment",
                "New comment on your workout",
                linkUrl: "/social",
                cancellationToken: ct);
        }
        await _context.SaveChangesAsync(ct);
        await _achievements.EvaluateAsync(ct, ["comments_made"]);
        return comment.Id;
    }
}

public record DeleteFeedCommentCommand(Guid Id) : IRequest;

public sealed class DeleteFeedCommentCommandHandler : IRequestHandler<DeleteFeedCommentCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteFeedCommentCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteFeedCommentCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var comment = await _context.FeedComments.FirstOrDefaultAsync(
            item => item.Id == request.Id && (item.UserId == userId || _currentUser.IsInRole("admin")),
            ct) ?? throw new EntityNotFoundException("Comment not found");
        comment.DeletedAt = DateTime.UtcNow;
        comment.DeletedByUserId = userId;
        await _context.SaveChangesAsync(ct);
    }
}

public record ReportContentCommand(string TargetType, Guid TargetId, string Reason) : IRequest<Guid>;

public sealed class ReportContentCommandValidator : AbstractValidator<ReportContentCommand>
{
    public ReportContentCommandValidator()
    {
        RuleFor(x => x.TargetType)
            .Must(value => new[] { "FeedItem", "FeedComment", "SocialProfile", "Exercise" }.Contains(value));
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReportContentCommandHandler : IRequestHandler<ReportContentCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReportContentCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(ReportContentCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var report = new ContentReport
        {
            Id = Guid.NewGuid(),
            ReporterUserId = userId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow
        };
        _context.ContentReports.Add(report);
        await _context.SaveChangesAsync(ct);
        return report.Id;
    }
}

public record ResolveContentReportCommand(Guid Id, string Action, string? Note) : IRequest;

public sealed class ResolveContentReportCommandValidator : AbstractValidator<ResolveContentReportCommand>
{
    public ResolveContentReportCommandValidator()
    {
        RuleFor(x => x.Action).Must(action => action is "dismiss" or "delete");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class ResolveContentReportCommandHandler : IRequestHandler<ResolveContentReportCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationWriter _notifications;

    public ResolveContentReportCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        INotificationWriter notifications)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task Handle(ResolveContentReportCommand request, CancellationToken ct)
    {
        var adminUserId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!_currentUser.IsInRole("admin")) throw new UnauthorizedAccessException();
        var report = await _context.ContentReports.FirstOrDefaultAsync(
            item => item.Id == request.Id && item.Status == "Open",
            ct) ?? throw new EntityNotFoundException("Report not found");
        if (request.Action == "delete" && report.TargetType is not ("FeedComment" or "FeedItem"))
        {
            throw new DomainValidationException($"Delete action is not supported for {report.TargetType} reports");
        }

        report.Status = request.Action == "dismiss" ? "Dismissed" : "Actioned";
        report.ResolvedAt = DateTime.UtcNow;
        report.ResolvedByUserId = adminUserId;
        report.ResolutionNote = request.Note;
        if (request.Action == "delete" && report.TargetType == "FeedComment")
        {
            var comment = await _context.FeedComments.FirstOrDefaultAsync(item => item.Id == report.TargetId, ct);
            if (comment is not null)
            {
                comment.DeletedAt = DateTime.UtcNow;
                comment.DeletedByUserId = adminUserId;
            }
        }
        if (request.Action == "delete" && report.TargetType == "FeedItem")
        {
            await _context.FeedItems.Where(item => item.Id == report.TargetId).ExecuteDeleteAsync(ct);
        }
        if (report.ReporterUserId.HasValue)
        {
            await _notifications.AddAsync(
                report.ReporterUserId.Value,
                "content_report_actioned",
                "Your report was reviewed",
                request.Note,
                "/social",
                ct);
        }
        await _context.SaveChangesAsync(ct);
    }
}

internal static class SocialAccess
{
    public static async Task<FeedItem> FindAccessibleItem(
        IMizanDbContext context,
        Guid userId,
        Guid itemId,
        CancellationToken ct)
    {
        var item = await context.FeedItems.FirstOrDefaultAsync(feedItem => feedItem.Id == itemId, ct)
            ?? throw new EntityNotFoundException("Feed item not found");
        if (item.UserId == userId) return item;

        var follows = await context.Follows.AnyAsync(
            follow => follow.FollowerUserId == userId &&
                follow.FolloweeUserId == item.UserId &&
                follow.Status == "Accepted",
            ct);
        if (!follows) throw new ForbiddenAccessException();
        return item;
    }
}
