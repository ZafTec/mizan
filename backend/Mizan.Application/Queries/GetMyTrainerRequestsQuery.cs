using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMyTrainerRequestsQuery : IRequest<PagedResult<MyTrainerRequestDto>>, IPagedQuery, ISortableQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record MyTrainerRequestDto(
    Guid RelationshipId,
    Guid TrainerId,
    string? TrainerName,
    string? TrainerEmail,
    string? TrainerImage,
    string Status,
    DateTime RequestedAt
);

public class GetMyTrainerRequestsQueryHandler : IRequestHandler<GetMyTrainerRequestsQuery, PagedResult<MyTrainerRequestDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyTrainerRequestsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<MyTrainerRequestDto>> Handle(GetMyTrainerRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var clientId = _currentUser.UserId.Value;

        var query = _context.TrainerClientRelationships
            .Include(r => r.Trainer)
            .Where(r => r.ClientId == clientId && r.Status == "pending");

        var totalCount = await query.CountAsync(cancellationToken);

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .ApplyPaging(request)
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

        return new PagedResult<MyTrainerRequestDto>
        {
            Items = requests,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
