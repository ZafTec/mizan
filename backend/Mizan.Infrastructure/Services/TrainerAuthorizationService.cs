using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
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

    public Guid GetCurrentUserId()
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }
        return _currentUser.UserId.Value;
    }

    public Task EnsureTrainerAccessAsync(CancellationToken cancellationToken = default)
    {
        GetCurrentUserId();

        if (!_currentUser.IsInRole("trainer") && !_currentUser.IsInRole("admin"))
        {
            throw new ForbiddenAccessException("Trainer access required");
        }

        return Task.CompletedTask;
    }

    public async Task<TrainerClientRelationship> GetRelationshipForCurrentTrainerAsync(Guid relationshipId, bool requireActive, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();

        var relationship = await _context.TrainerClientRelationships
            .FirstOrDefaultAsync(r => r.Id == relationshipId, cancellationToken);

        if (relationship == null)
        {
            throw new EntityNotFoundException("TrainerClientRelationship", relationshipId);
        }

        if (relationship.TrainerId != currentUserId)
        {
            throw new ForbiddenAccessException("Relationship does not belong to the current trainer");
        }

        if (requireActive && !string.Equals(relationship.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Trainer relationship is not active");
        }

        return relationship;
    }

    public async Task<TrainerClientRelationship> GetRelationshipForCurrentTrainerAndClientAsync(Guid clientId, bool requireActive, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();

        var relationship = await _context.TrainerClientRelationships
            .Where(r => r.ClientId == clientId && r.TrainerId == currentUserId)
            .OrderByDescending(r => r.StartedAt ?? r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (relationship == null)
        {
            throw new EntityNotFoundException("No trainer relationship found for this client");
        }

        if (requireActive && !string.Equals(relationship.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("Trainer relationship is not active");
        }

        return relationship;
    }
}
