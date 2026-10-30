using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

/// <summary>
/// What deleting the caller's social profile removes, for them to see before they confirm: the
/// profile, every follow in either direction, and their feed posts with the comments and
/// reactions on them. Counts only, so the same state gives the same preview.
/// </summary>
public sealed record GetSocialProfileDeletePreviewQuery : IRequest<SocialProfileDeletePreview>;

public sealed record SocialProfileDeletePreview(
    string DisplayName,
    int Followers,
    int Following,
    int FeedItems,
    int CommentsOnFeedItems,
    int ReactionsOnFeedItems);

public sealed class GetSocialProfileDeletePreviewQueryHandler : IRequestHandler<GetSocialProfileDeletePreviewQuery, SocialProfileDeletePreview>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSocialProfileDeletePreviewQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SocialProfileDeletePreview> Handle(GetSocialProfileDeletePreviewQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var name = await _context.SocialProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("There is no social profile to delete.");

        var followers = await _context.Follows.CountAsync(f => f.FolloweeUserId == userId, cancellationToken);
        var following = await _context.Follows.CountAsync(f => f.FollowerUserId == userId, cancellationToken);
        var items = await _context.FeedItems.CountAsync(i => i.UserId == userId, cancellationToken);
        var comments = await _context.FeedComments.CountAsync(c => c.FeedItem.UserId == userId && c.DeletedAt == null, cancellationToken);
        var reactions = await _context.FeedReactions.CountAsync(r => r.FeedItem.UserId == userId, cancellationToken);
        return new SocialProfileDeletePreview(name, followers, following, items, comments, reactions);
    }
}
