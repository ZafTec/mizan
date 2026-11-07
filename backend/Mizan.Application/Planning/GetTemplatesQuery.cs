using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>The templates the viewer can apply: every global one, and their own.</summary>
public record GetTemplatesQuery : IRequest<List<TemplateDto>>;

public class GetTemplatesQueryHandler : IRequestHandler<GetTemplatesQuery, List<TemplateDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTemplatesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<TemplateDto>> Handle(GetTemplatesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        var rows = await _context.MealPlans.AsNoTracking()
            .Where(p => p.Kind == PlanKind.Template && (p.UserId == null || p.UserId == userId))
            .OrderBy(p => p.UserId == null ? 0 : 1)
            .ThenBy(p => p.Name)
            .Select(p => new { Plan = p, EntryCount = p.Entries.Count })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TemplateDto
        {
            Id = r.Plan.Id,
            Name = r.Plan.Name,
            Description = r.Plan.Description,
            DurationDays = r.Plan.DurationDays,
            Global = r.Plan.UserId is null,
            OwnerId = r.Plan.UserId,
            EntryCount = r.EntryCount,
            Rules = PlanRulesMapping.ToDto(r.Plan.Rules),
            CreatedAt = r.Plan.CreatedAt,
            UpdatedAt = r.Plan.UpdatedAt,
        }).ToList();
    }
}
