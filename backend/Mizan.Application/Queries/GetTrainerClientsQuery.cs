using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetTrainerClientsQuery : IRequest<PagedResult<TrainerClientDto>>, IPagedQuery, ISortableQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

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

public class GetTrainerClientsQueryHandler : IRequestHandler<GetTrainerClientsQuery, PagedResult<TrainerClientDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTrainerClientsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<TrainerClientDto>> Handle(GetTrainerClientsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var trainerId = _currentUser.UserId.Value;

        var query = _context.TrainerClientRelationships
            .Where(r => r.TrainerId == trainerId);

        var totalCount = await query.CountAsync(cancellationToken);

        var relationships = await query
            .OrderByDescending(r => r.CreatedAt)
            .ApplyPaging(request)
            .Select(r => new TrainerClientDto(
                r.Id,
                r.ClientId,
                null,
                null,
                r.Status,
                r.CanViewNutrition,
                r.CanViewWorkouts,
                r.CanMessage,
                r.StartedAt ?? r.CreatedAt,
                r.EndedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<TrainerClientDto>
        {
            Items = relationships,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
