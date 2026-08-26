using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

/// <summary>
/// A one-time link to Paddle's hosted portal, where a subscriber cancels,
/// changes plan, or updates a card. Minted fresh on every call - the links
/// are single-use, so nothing here is ever cached (docs/REFOCUS.md §17).
/// </summary>
public record GetBillingPortalSessionQuery : IRequest<BillingPortalSessionDto?>;

public record BillingPortalSessionDto
{
    public string OverviewUrl { get; init; } = string.Empty;
    public string? CancelSubscriptionUrl { get; init; }
    public string? UpdatePaymentMethodUrl { get; init; }
}

public class GetBillingPortalSessionQueryHandler : IRequestHandler<GetBillingPortalSessionQuery, BillingPortalSessionDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaddleApiClient _paddle;

    public GetBillingPortalSessionQueryHandler(
        IMizanDbContext context, ICurrentUserService currentUser, IPaddleApiClient paddle)
    {
        _context = context;
        _currentUser = currentUser;
        _paddle = paddle;
    }

    public async Task<BillingPortalSessionDto?> Handle(
        GetBillingPortalSessionQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var sub = await _context.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => new { s.PaddleCustomerId, s.PaddleSubscriptionId })
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(sub?.PaddleCustomerId))
        {
            // A free user who has never checked out has no Paddle customer to
            // open a portal for - not an error, just nothing to show yet.
            throw new EntityNotFoundException("Billing account", _currentUser.UserId.Value);
        }

        var session = await _paddle.CreatePortalSessionAsync(
            sub.PaddleCustomerId, sub.PaddleSubscriptionId, cancellationToken);

        if (session is null)
        {
            // Not a 404 - the account exists, Paddle just did not answer. The
            // controller turns this into a 502 the frontend can retry.
            return null;
        }

        return new BillingPortalSessionDto
        {
            OverviewUrl = session.OverviewUrl,
            CancelSubscriptionUrl = session.CancelSubscriptionUrl,
            UpdatePaymentMethodUrl = session.UpdatePaymentMethodUrl
        };
    }
}
