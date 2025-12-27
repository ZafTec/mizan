using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record DeleteFoodDiaryEntryCommand : IRequest<DeleteFoodDiaryEntryResult>
{
    public Guid Id { get; init; }
}

public record DeleteFoodDiaryEntryResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class DeleteFoodDiaryEntryCommandHandler : IRequestHandler<DeleteFoodDiaryEntryCommand, DeleteFoodDiaryEntryResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteFoodDiaryEntryCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteFoodDiaryEntryResult> Handle(DeleteFoodDiaryEntryCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new DeleteFoodDiaryEntryResult
            {
                Success = false,
                Message = "User not authenticated"
            };
        }

        var entry = await _context.FoodDiaryEntries
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.UserId == _currentUser.UserId.Value, cancellationToken);

        if (entry == null)
        {
            return new DeleteFoodDiaryEntryResult
            {
                Success = false,
                Message = "Entry not found or you don't have permission to delete it"
            };
        }

        _context.FoodDiaryEntries.Remove(entry);
        await _context.SaveChangesAsync(cancellationToken);

        return new DeleteFoodDiaryEntryResult
        {
            Success = true,
            Message = "Entry deleted successfully"
        };
    }
}
