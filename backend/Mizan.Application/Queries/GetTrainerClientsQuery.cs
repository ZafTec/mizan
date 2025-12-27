using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetTrainerClientsQuery : IRequest<List<TrainerClientDto>>;

public record TrainerClientDto(
    Guid RelationshipId,
    Guid ClientId,
    string? ClientName,
    string? ClientEmail,
    string Status,
    bool CanViewNutrition,
    bool CanViewWorkouts,
    bool CanMessage,
    DateTime StartedAt,
    DateTime? EndedAt
);

public class GetTrainerClientsQueryHandler : IRequestHandler<GetTrainerClientsQuery, List<TrainerClientDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTrainerClientsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<TrainerClientDto>> Handle(GetTrainerClientsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var trainerId = _currentUser.UserId.Value;

        // Get all relationships where current user is the trainer
        var relationships = await _context.TrainerClientRelationships
            .Where(r => r.TrainerId == trainerId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new TrainerClientDto(
                r.Id,
                r.ClientId,
                null, // Will be populated from users table if available
                null, // Will be populated from users table if available
                r.Status,
                r.CanViewNutrition,
                r.CanViewWorkouts,
                r.CanMessage,
                r.StartedAt ?? r.CreatedAt,
                r.EndedAt
            ))
            .ToListAsync(cancellationToken);

        return relationships;
    }
}
