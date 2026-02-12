using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetAvailableTrainersQuery : IRequest<PagedResult<TrainerPublicDto>>, IPagedQuery, ISortableQuery
{
    public string? SearchTerm { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record TrainerPublicDto(
    Guid Id,
    string? Name,
    string Email,
    string? Image,
    string? Bio,
    string? Specialties,
    int ClientCount
);

public class GetAvailableTrainersQueryHandler : IRequestHandler<GetAvailableTrainersQuery, PagedResult<TrainerPublicDto>>
{
    private readonly IMizanDbContext _context;

    public GetAvailableTrainersQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<TrainerPublicDto>> Handle(GetAvailableTrainersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users
            .Where(u => (u.Role == "trainer" || u.Role == "admin") && !u.Banned);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(searchTerm)) ||
                u.Email.ToLower().Contains(searchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var trainers = await query
            .OrderBy(u => u.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new
            {
                User = u,
                ClientCount = u.TrainerRelationships.Count(r => r.Status == "active")
            })
            .ToListAsync(cancellationToken);

        var items = trainers.Select(t => new TrainerPublicDto(
            t.User.Id,
            t.User.Name,
            t.User.Email,
            t.User.Image,
            null,
            null,
            t.ClientCount
        )).ToList();

        return new PagedResult<TrainerPublicDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
