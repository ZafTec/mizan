using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record DeleteBodyMeasurementCommand(Guid Id) : IRequest<DeleteBodyMeasurementResult>;

public record DeleteBodyMeasurementResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class DeleteBodyMeasurementCommandHandler : IRequestHandler<DeleteBodyMeasurementCommand, DeleteBodyMeasurementResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteBodyMeasurementCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteBodyMeasurementResult> Handle(DeleteBodyMeasurementCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var measurement = await _context.BodyMeasurements
            .FirstOrDefaultAsync(m => m.Id == request.Id && m.UserId == _currentUser.UserId, cancellationToken);

        if (measurement == null)
        {
            return new DeleteBodyMeasurementResult
            {
                Success = false,
                Message = "Measurement not found or access denied"
            };
        }

        _context.BodyMeasurements.Remove(measurement);
        await _context.SaveChangesAsync(cancellationToken);

        return new DeleteBodyMeasurementResult
        {
            Success = true,
            Message = "Measurement deleted successfully"
        };
    }
}
