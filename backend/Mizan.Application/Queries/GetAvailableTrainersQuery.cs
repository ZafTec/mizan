using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Queries;

public record GetAvailableTrainersQuery : IRequest<PagedResult<TrainerPublicDto>>, IPagedQuery, ISortableQuery
{
    public string? SearchTerm { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

/// <summary>
/// Who may be asked to coach: a person with the trainer role who is not banned. The trainer
/// endpoints (clients, requests, responding) need that role, so an administrator is not a trainer
/// unless given the role, and nobody is offered themselves.
/// </summary>
public static class AvailableTrainers
{
    public const string Role = "trainer";

    public static readonly Expression<Func<User, bool>> Rule = u => u.Role == Role && !u.Banned;
}

/// <summary>A trainer as anyone looking for one sees them: no contact details.</summary>
public record TrainerPublicDto(
    Guid Id,
    string? Name,
    string? Image,
    string? Bio,
    string? Specialties,
    int ClientCount
);

public class GetAvailableTrainersQueryHandler : IRequestHandler<GetAvailableTrainersQuery, PagedResult<TrainerPublicDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAvailableTrainersQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<TrainerPublicDto>> Handle(GetAvailableTrainersQuery request, CancellationToken cancellationToken)
    {
        var viewer = _currentUser.UserId;
        var query = _context.Users.Where(AvailableTrainers.Rule).Where(u => u.Id != viewer);

        // By name only: the list shows no email, so it cannot be searched by one.
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(u => u.Name != null && u.Name.ToLower().Contains(searchTerm));
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
