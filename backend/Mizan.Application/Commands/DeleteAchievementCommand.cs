using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record DeleteAchievementCommand(Guid Id) : IRequest<Unit>;

public class DeleteAchievementCommandHandler : IRequestHandler<DeleteAchievementCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly IAchievementCatalogue _catalogue;

    public DeleteAchievementCommandHandler(IMizanDbContext context, IAchievementCatalogue catalogue)
    {
        _context = context;
        _catalogue = catalogue;
    }

    public async Task<Unit> Handle(DeleteAchievementCommand request, CancellationToken cancellationToken)
    {
        var achievement = await _context.Achievements
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Achievement", request.Id);

        // Cascade drops user_achievement rows via FK on delete behavior.
        _context.Achievements.Remove(achievement);
        await _context.SaveChangesAsync(cancellationToken);

        // The catalogue is cached for the logging path; without this an edit
        // takes a cache lifetime to start unlocking.
        await _catalogue.InvalidateAsync(cancellationToken);
        return Unit.Value;
    }
}
