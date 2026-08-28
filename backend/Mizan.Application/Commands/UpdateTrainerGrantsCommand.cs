using MediatR;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

/// <summary>
/// Lets the CLIENT change what a trainer may see, at any point in the
/// relationship, and end it outright.
///
/// The grant flags belong to the client: they are the subject of the data. The
/// trainer's accept/decline decides whether the relationship exists, not what
/// it exposes. Only fields that are supplied change; omitted fields are left
/// alone so a caller toggling one axis cannot silently widen another.
/// </summary>
public record UpdateTrainerGrantsCommand(
    Guid RelationshipId,
    bool? CanViewNutrition = null,
    bool? CanViewWorkouts = null,
    bool? CanViewMeasurements = null,
    bool? CanMessage = null,
    bool End = false
) : IRequest<bool>;

public class UpdateTrainerGrantsCommandHandler : IRequestHandler<UpdateTrainerGrantsCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly ITrainerAuthorizationService _trainerAuthorization;
    private readonly INotificationWriter? _notifications;

    public UpdateTrainerGrantsCommandHandler(
        IMizanDbContext context,
        ITrainerAuthorizationService trainerAuthorization,
        INotificationWriter? notifications = null)
    {
        _context = context;
        _trainerAuthorization = trainerAuthorization;
        _notifications = notifications;
    }

    public async Task<bool> Handle(UpdateTrainerGrantsCommand request, CancellationToken cancellationToken)
    {
        var relationship = await _trainerAuthorization.GetRelationshipForCurrentClientAsync(
            request.RelationshipId,
            requireActive: false,
            cancellationToken);

        if (request.End)
        {
            relationship.Status = "ended";
            relationship.EndedAt = DateTime.UtcNow;
            relationship.CanViewNutrition = false;
            relationship.CanViewWorkouts = false;
            relationship.CanViewMeasurements = false;
            relationship.CanMessage = false;

            if (_notifications is not null)
            {
                await _notifications.AddAsync(
                    relationship.TrainerId,
                    "trainer_relationship_ended",
                    "A client ended your coaching relationship",
                    linkUrl: "/coach",
                    cancellationToken: cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (request.CanViewNutrition.HasValue) relationship.CanViewNutrition = request.CanViewNutrition.Value;
        if (request.CanViewWorkouts.HasValue) relationship.CanViewWorkouts = request.CanViewWorkouts.Value;
        if (request.CanViewMeasurements.HasValue) relationship.CanViewMeasurements = request.CanViewMeasurements.Value;
        if (request.CanMessage.HasValue) relationship.CanMessage = request.CanMessage.Value;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
