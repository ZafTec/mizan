using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateHouseholdCommand(string Name, Guid CreatorUserId) : IRequest<Guid>;

public class CreateHouseholdCommandHandler : IRequestHandler<CreateHouseholdCommand, Guid>
{
    private readonly IMizanDbContext _context;

    public CreateHouseholdCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateHouseholdCommand request, CancellationToken cancellationToken)
    {
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
            CanViewNutrition = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.HouseholdMembers.Add(member);

        await _context.SaveChangesAsync(cancellationToken);

        return household.Id;
    }
}
