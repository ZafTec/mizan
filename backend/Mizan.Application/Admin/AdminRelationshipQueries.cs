using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Admin;

public record AdminRelationshipDto
{
    public Guid Id { get; init; }
    public Guid TrainerId { get; init; }
    public string? TrainerName { get; init; }
    public string TrainerEmail { get; init; } = string.Empty;
    public Guid ClientId { get; init; }
    public string? ClientName { get; init; }
    public string ClientEmail { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// The per-axis grants, surfaced because they are the thing an admin is
    /// usually here to check: a support ticket that says "my coach can see my
    /// weight and shouldn't" is answered by this row (docs/REFOCUS.md §11).
    /// </summary>
    public bool CanViewNutrition { get; init; }

    public bool CanViewWorkouts { get; init; }
    public bool CanViewMeasurements { get; init; }
    public bool CanMessage { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ListAdminRelationshipsQuery
    : IRequest<PagedResult<AdminRelationshipDto>>, IPagedQuery, ISortableQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Matches either side's name or email - you rarely know which one you have.</summary>
    public string? Search { get; init; }

    public string? Status { get; init; }
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public class ListAdminRelationshipsQueryHandler
    : IRequestHandler<ListAdminRelationshipsQuery, PagedResult<AdminRelationshipDto>>
{
    private static readonly Dictionary<string, Expression<Func<TrainerClientRelationship, object>>> SortMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["trainer"] = r => r.Trainer.Email,
            ["client"] = r => r.Client.Email,
            ["status"] = r => r.Status,
            ["createdat"] = r => r.CreatedAt,
        };

    private readonly IMizanDbContext _context;

    public ListAdminRelationshipsQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<PagedResult<AdminRelationshipDto>> Handle(
        ListAdminRelationshipsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.TrainerClientRelationships.AsNoTracking()
            .Include(r => r.Trainer)
            .Include(r => r.Client)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(r =>
                r.Trainer.Email.ToLower().Contains(term)
                || r.Client.Email.ToLower().Contains(term)
                || (r.Trainer.Name != null && r.Trainer.Name.ToLower().Contains(term))
                || (r.Client.Name != null && r.Client.Name.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(r => r.Status == request.Status);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySorting(request, SortMappings, r => r.CreatedAt, defaultDescending: true)
            .ThenBy(r => r.Id)
            .ApplyPaging(request)
            .Select(r => new AdminRelationshipDto
            {
                Id = r.Id,
                TrainerId = r.TrainerId,
                TrainerName = r.Trainer.Name,
                TrainerEmail = r.Trainer.Email,
                ClientId = r.ClientId,
                ClientName = r.Client.Name,
                ClientEmail = r.Client.Email,
                Status = r.Status,
                CanViewNutrition = r.CanViewNutrition,
                CanViewWorkouts = r.CanViewWorkouts,
                CanViewMeasurements = r.CanViewMeasurements,
                CanMessage = r.CanMessage,
                StartedAt = r.StartedAt,
                EndedAt = r.EndedAt,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminRelationshipDto>
        {
            Items = items,
            TotalCount = total,
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 100),
        };
    }
}

/// <summary>
/// Ends a relationship on the client's behalf.
///
/// Admin gets to end one, and nothing else. Editing the per-axis grants from
/// here would be an admin deciding what a client shares with their coach,
/// which is exactly the super-user access §11 rules out - the client owns
/// those switches. Ending it is the support action that actually comes up:
/// "my coach still has access and I cannot reach them".
/// </summary>
public record EndAdminRelationshipCommand(Guid Id, string? Reason) : IRequest;

public class EndAdminRelationshipCommandHandler : IRequestHandler<EndAdminRelationshipCommand>
{
    private readonly IMizanDbContext _context;

    public EndAdminRelationshipCommandHandler(IMizanDbContext context) => _context = context;

    public async Task Handle(EndAdminRelationshipCommand request, CancellationToken cancellationToken)
    {
        var relationship = await _context.TrainerClientRelationships
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new Exceptions.EntityNotFoundException("Relationship", request.Id);

        if (relationship.Status == "ended") return;

        relationship.Status = "ended";
        relationship.EndedAt = DateTime.UtcNow;

        // Access follows status, but the grants are the client's record of what
        // they agreed to. Leaving them alone means re-accepting the same coach
        // restores what they had rather than silently starting from nothing.
        await _context.SaveChangesAsync(cancellationToken);
    }
}
