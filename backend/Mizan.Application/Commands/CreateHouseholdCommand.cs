using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateHouseholdCommand(string Name, Guid CreatorUserId) : IRequest<Guid>;

public class CreateHouseholdCommandHandler : IRequestHandler<CreateHouseholdCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;

    public CreateHouseholdCommandHandler(IMizanDbContext context, IHouseholdAccess households)
    {
        _context = context;
        _households = households;
    }

    public async Task<Guid> Handle(CreateHouseholdCommand request, CancellationToken cancellationToken)
    {
        _households.EnsureGrantCovers(null);
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CreatedBy = request.CreatorUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Households.Add(household);

        // Add creator as an admin member
        var member = new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = request.CreatorUserId,
            Role = "admin",
            CanEditRecipes = true,
            CanEditShoppingList = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.HouseholdMembers.Add(member);

        await _context.SaveChangesAsync(cancellationToken);

        return household.Id;
    }
}
