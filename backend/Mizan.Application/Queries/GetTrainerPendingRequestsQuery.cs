using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetTrainerPendingRequestsQuery : IRequest<PagedResult<TrainerPendingRequestDto>>, IPagedQuery, ISortableQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record TrainerPendingRequestDto(
    Guid RelationshipId,
    Guid ClientId,
    string? ClientName,
    string? ClientEmail,
    DateTime RequestedAt
);

public class GetTrainerPendingRequestsQueryHandler : IRequestHandler<GetTrainerPendingRequestsQuery, PagedResult<TrainerPendingRequestDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ITrainerAuthorizationService _trainerAuthorization;

    public GetTrainerPendingRequestsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, ITrainerAuthorizationService trainerAuthorization)
    {
        _context = context;
        _currentUser = currentUser;
        _trainerAuthorization = trainerAuthorization;
    }

    public async Task<PagedResult<TrainerPendingRequestDto>> Handle(GetTrainerPendingRequestsQuery request, CancellationToken cancellationToken)
    {
        await _trainerAuthorization.EnsureTrainerAccessAsync(cancellationToken);

        var trainerId = _currentUser.UserId!.Value;

        var query = _context.TrainerClientRelationships
            .Include(r => r.Client)
            .Where(r => r.TrainerId == trainerId && r.Status == "pending");

        var totalCount = await query.CountAsync(cancellationToken);

        var pendingRequests = await query
            .OrderBy(r => r.CreatedAt)
            .ApplyPaging(request)
            .Select(r => new TrainerPendingRequestDto(
                r.Id,
                r.ClientId,
                r.Client.Name,
                r.Client.Email,
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<TrainerPendingRequestDto>
        {
            Items = pendingRequests,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
