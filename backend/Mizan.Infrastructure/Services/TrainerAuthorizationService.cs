using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Services;

public class TrainerAuthorizationService : ITrainerAuthorizationService
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TrainerAuthorizationService(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public Task EnsureTrainerAccessAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        if (_currentUser.IsInRole("trainer"))
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Trainer access required");
    }

    public async Task<TrainerClientRelationship> GetRelationshipForCurrentTrainerAsync(Guid relationshipId, bool requireActive, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAccessAsync(cancellationToken);

        var currentUserId = _currentUser.UserId!.Value;
        var relationship = await _context.TrainerClientRelationships
            .FirstOrDefaultAsync(r => r.Id == relationshipId, cancellationToken);

        if (relationship == null)
        {
            throw new KeyNotFoundException("Trainer relationship not found");
        }

        if (relationship.TrainerId != currentUserId)
        {
            throw new UnauthorizedAccessException("Relationship does not belong to the current trainer");
        }

        if (requireActive && !string.Equals(relationship.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Trainer relationship is not active");
        }

        return relationship;
    }

    public async Task<TrainerClientRelationship> GetRelationshipForCurrentTrainerAndClientAsync(Guid clientId, bool requireActive, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAccessAsync(cancellationToken);

        var currentUserId = _currentUser.UserId!.Value;
        var query = _context.TrainerClientRelationships
            .Where(r => r.ClientId == clientId && r.TrainerId == currentUserId);

        var relationship = await query
            .OrderByDescending(r => r.StartedAt ?? r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (relationship == null)
        {
            throw new UnauthorizedAccessException("No trainer relationship found for this client");
        }

        if (requireActive && !string.Equals(relationship.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Trainer relationship is not active");
        }

        return relationship;
    }
}
