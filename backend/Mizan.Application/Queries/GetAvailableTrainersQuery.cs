using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetAvailableTrainersQuery : IRequest<List<TrainerPublicDto>>;

public record TrainerPublicDto(
    Guid Id,
    string? Name,
    string Email,
    string? Image,
    string? Bio,
    string? Specialties,
    int ClientCount
);

public class GetAvailableTrainersQueryHandler : IRequestHandler<GetAvailableTrainersQuery, List<TrainerPublicDto>>
{
    private readonly IMizanDbContext _context;

    public GetAvailableTrainersQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<List<TrainerPublicDto>> Handle(GetAvailableTrainersQuery request, CancellationToken cancellationToken)
    {
        var trainers = await _context.Users
            .Where(u => (u.Role == "trainer" || u.Role == "admin") && !u.Banned)
            .Select(u => new
            {
                User = u,
                ClientCount = u.TrainerRelationships.Count(r => r.Status == "active")
            })
            .ToListAsync(cancellationToken);

        return trainers.Select(t => new TrainerPublicDto(
            t.User.Id,
            t.User.Name,
            t.User.Email,
            t.User.Image,
            null,
            null,
            t.ClientCount
        )).ToList();
    }
}
