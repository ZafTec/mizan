using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

/// <summary>
/// The trainer accepts or declines. They do NOT set the grant flags - those are
/// the client's, set on the request and changeable afterwards via
/// <see cref="UpdateTrainerGrantsCommand"/>. A trainer choosing their own
/// access to someone else's body data is the wrong way round.
/// </summary>
public record RespondToTrainerRequestCommand(
    Guid RelationshipId,
    bool Accept
) : IRequest<bool>;

public class RespondToTrainerRequestCommandHandler : IRequestHandler<RespondToTrainerRequestCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly ITrainerAuthorizationService _trainerAuthorization;
    private readonly INotificationWriter? _notifications;

    public RespondToTrainerRequestCommandHandler(IMizanDbContext context, ITrainerAuthorizationService trainerAuthorization, INotificationWriter? notifications = null)
    {
        _context = context;
        _trainerAuthorization = trainerAuthorization;
        _notifications = notifications;
    }

    public async Task<bool> Handle(RespondToTrainerRequestCommand request, CancellationToken cancellationToken)
    {
        var relationship = await _trainerAuthorization.GetRelationshipForCurrentTrainerAsync(
            request.RelationshipId,
            requireActive: false,
            cancellationToken);

        if (request.Accept)
        {
            relationship.Status = "active";
            relationship.StartedAt = DateTime.UtcNow;

            var existingConv = await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.TrainerClientRelationshipId == relationship.Id, cancellationToken);

            if (existingConv == null)
            {
                _context.ChatConversations.Add(new Domain.Entities.ChatConversation
                {
                    Id = Guid.NewGuid(),
                    TrainerClientRelationshipId = relationship.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            relationship.Status = "rejected";
            relationship.EndedAt = DateTime.UtcNow;
        }

        if (_notifications is not null)
        {
            await _notifications.AddAsync(relationship.ClientId, "trainer_request_response", request.Accept ? "Trainer request accepted" : "Trainer request declined", linkUrl: "/trainers/my-trainer", cancellationToken: cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
