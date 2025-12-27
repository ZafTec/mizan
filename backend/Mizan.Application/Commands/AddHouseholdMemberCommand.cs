using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

// Simplification: Adding by email for now, assuming user exists.
public record AddHouseholdMemberCommand(Guid HouseholdId, string UserEmail, Guid RequestingUserId) : IRequest<bool>;

public class AddHouseholdMemberCommandHandler : IRequestHandler<AddHouseholdMemberCommand, bool>
{
    private readonly IMizanDbContext _context;

    public AddHouseholdMemberCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(AddHouseholdMemberCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify requesting user has permission (is admin of household)
        var requester = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == request.RequestingUserId, cancellationToken);
        
        if (requester == null || requester.Role != "admin")
        {
            // Or return specific error
            return false;
        }

        // 2. Find user to add
        var userToAdd = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.UserEmail, cancellationToken);
        if (userToAdd == null)
        {
            return false;
        }

        // 3. Check if already a member
        var existingMember = await _context.HouseholdMembers
            .AnyAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == userToAdd.Id, cancellationToken);
        
        if (existingMember)
        {
            return false; 
        }

        // 4. Add member
        var newMember = new HouseholdMember
        {
            HouseholdId = request.HouseholdId,
            UserId = userToAdd.Id,
            Role = "member",
            CanEditRecipes = true,
            CanEditShoppingList = true,
            CanViewNutrition = false,
            JoinedAt = DateTime.UtcNow
        };

        _context.HouseholdMembers.Add(newMember);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
