using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetHouseholdQuery(Guid HouseholdId) : IRequest<HouseholdDto?>;

public record HouseholdDto(
    Guid Id,
    string Name,
    Guid? CreatedBy,
    DateTime CreatedAt,
    List<HouseholdMemberDto> Members
);

public record HouseholdMemberDto(
    Guid UserId,
    string? Name,
    string? Email,
    string? Role,
    DateTime JoinedAt
);

public class GetHouseholdQueryHandler : IRequestHandler<GetHouseholdQuery, HouseholdDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetHouseholdQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<HouseholdDto?> Handle(GetHouseholdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return null;
        }

        var household = await _context.Households
            .Include(h => h.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(h => h.Id == request.HouseholdId, cancellationToken);

        if (household == null)
        {
            return null;
        }

        // Authorization: User must be a member of the household
        var isMember = household.Members.Any(m => m.UserId == _currentUser.UserId.Value);
        if (!isMember)
        {
            return null;
        }

        return new HouseholdDto(
            household.Id,
            household.Name,
            household.CreatedBy,
            household.CreatedAt,
            household.Members.Select(m => new HouseholdMemberDto(
                m.UserId,
                m.User?.Name,
                m.User?.Email,
                m.Role,
                m.JoinedAt
            )).ToList()
        );
    }
}
