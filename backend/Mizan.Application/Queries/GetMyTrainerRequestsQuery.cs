using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMyTrainerRequestsQuery : IRequest<List<MyTrainerRequestDto>>;

public record MyTrainerRequestDto(
    Guid RelationshipId,
    Guid TrainerId,
    string? TrainerName,
    string? TrainerEmail,
    string? TrainerImage,
    string Status,
    DateTime RequestedAt
);

public class GetMyTrainerRequestsQueryHandler : IRequestHandler<GetMyTrainerRequestsQuery, List<MyTrainerRequestDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyTrainerRequestsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<MyTrainerRequestDto>> Handle(GetMyTrainerRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var clientId = _currentUser.UserId.Value;

        var requests = await _context.TrainerClientRelationships
            .Include(r => r.Trainer)
            .Where(r => r.ClientId == clientId && r.Status == "pending")
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new MyTrainerRequestDto(
                r.Id,
                r.TrainerId,
                r.Trainer.Name,
                r.Trainer.Email,
                r.Trainer.Image,
                r.Status,
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return requests;
    }
}
