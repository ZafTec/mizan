using MediatR;
using Microsoft.EntityFrameworkCore;
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
