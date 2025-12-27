using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record RespondToTrainerRequestCommand(
    Guid RelationshipId,
    bool Accept,
    bool? CanViewNutrition = null,
    bool? CanViewWorkouts = null,
    bool? CanViewMeasurements = null,
    bool? CanMessage = null
) : IRequest<bool>;

public class RespondToTrainerRequestCommandHandler : IRequestHandler<RespondToTrainerRequestCommand, bool>
{
    private readonly IMizanDbContext _context;

    public RespondToTrainerRequestCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RespondToTrainerRequestCommand request, CancellationToken cancellationToken)
    {
        var relationship = await _context.TrainerClientRelationships.FindAsync(new object[] { request.RelationshipId }, cancellationToken);
        
        if (relationship == null)
        {
            return false;
        }

        if (request.Accept)
        {
            relationship.Status = "active";
            relationship.StartedAt = DateTime.UtcNow;

            if (request.CanViewNutrition.HasValue)
            {
                relationship.CanViewNutrition = request.CanViewNutrition.Value;
            }

            if (request.CanViewWorkouts.HasValue)
            {
                relationship.CanViewWorkouts = request.CanViewWorkouts.Value;
            }

            if (request.CanViewMeasurements.HasValue)
            {
                relationship.CanViewMeasurements = request.CanViewMeasurements.Value;
            }

            if (request.CanMessage.HasValue)
            {
                relationship.CanMessage = request.CanMessage.Value;
            }

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

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
