using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMyTrainerQuery : IRequest<MyTrainerDto?>;

public record MyTrainerDto(
    Guid RelationshipId,
    Guid TrainerId,
    string? TrainerName,
    string? TrainerEmail,
    string? TrainerImage,
    string Status,
    bool CanViewNutrition,
    bool CanViewWorkouts,
    bool CanViewMeasurements,
    bool CanMessage,
    DateTime StartedAt,
    DateTime? EndedAt
);

public class GetMyTrainerQueryHandler : IRequestHandler<GetMyTrainerQuery, MyTrainerDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyTrainerQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<MyTrainerDto?> Handle(GetMyTrainerQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var clientId = _currentUser.UserId.Value;

        var relationship = await _context.TrainerClientRelationships
            .Include(r => r.Trainer)
            .Where(r => r.ClientId == clientId && r.Status == "active")
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (relationship == null)
        {
            return null;
        }

        return new MyTrainerDto(
            relationship.Id,
            relationship.TrainerId,
            relationship.Trainer.Name,
            relationship.Trainer.Email,
            relationship.Trainer.Image,
            relationship.Status,
            relationship.CanViewNutrition,
            relationship.CanViewWorkouts,
            relationship.CanViewMeasurements,
            relationship.CanMessage,
            relationship.StartedAt ?? relationship.CreatedAt,
            relationship.EndedAt
        );
    }
}
