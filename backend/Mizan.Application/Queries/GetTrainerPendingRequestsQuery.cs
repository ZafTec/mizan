using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetTrainerPendingRequestsQuery : IRequest<List<TrainerPendingRequestDto>>;

public record TrainerPendingRequestDto(
    Guid RelationshipId,
    Guid ClientId,
    string? ClientName,
    string? ClientEmail,
    DateTime RequestedAt
);

public class GetTrainerPendingRequestsQueryHandler : IRequestHandler<GetTrainerPendingRequestsQuery, List<TrainerPendingRequestDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTrainerPendingRequestsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<TrainerPendingRequestDto>> Handle(GetTrainerPendingRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var trainerId = _currentUser.UserId.Value;

        // Get all pending relationships where current user is the trainer
        var pendingRequests = await _context.TrainerClientRelationships
            .Where(r => r.TrainerId == trainerId && r.Status == "pending")
            .OrderBy(r => r.CreatedAt)
            .Select(r => new TrainerPendingRequestDto(
                r.Id,
                r.ClientId,
                null, // Will be populated from users table if available
                null, // Will be populated from users table if available
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return pendingRequests;
    }
}
