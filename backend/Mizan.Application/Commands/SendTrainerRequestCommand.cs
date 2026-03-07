using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record SendTrainerRequestCommand(Guid ClientId, Guid TrainerId) : IRequest<Guid>;

public class SendTrainerRequestCommandHandler : IRequestHandler<SendTrainerRequestCommand, Guid>
{
    private readonly IMizanDbContext _context;

    public SendTrainerRequestCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(SendTrainerRequestCommand request, CancellationToken cancellationToken)
    {
        if (request.ClientId == request.TrainerId)
        {
            throw new DomainValidationException("You cannot send a trainer request to yourself");
        }

        var trainerExists = await _context.Users
            .AnyAsync(
                u => u.Id == request.TrainerId
                    && !u.Banned
                    && (u.Role == "trainer" || u.Role == "admin"),
                cancellationToken);

        if (!trainerExists)
        {
            throw new DomainValidationException("Selected user is not an available trainer");
        }

        // Check if relationship already exists
        var existing = await _context.TrainerClientRelationships
            .FirstOrDefaultAsync(r => r.ClientId == request.ClientId && r.TrainerId == request.TrainerId, cancellationToken);

        if (existing != null)
        {
            return existing.Id;
        }

        var relationship = new TrainerClientRelationship
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            TrainerId = request.TrainerId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.TrainerClientRelationships.Add(relationship);
        await _context.SaveChangesAsync(cancellationToken);

        return relationship.Id;
    }
}
