using Mizan.Domain.Entities;

namespace Mizan.Application.Interfaces;

public interface ITrainerAuthorizationService
{
    Guid GetCurrentUserId();
    Task EnsureTrainerAccessAsync(CancellationToken cancellationToken = default);
    Task<TrainerClientRelationship> GetRelationshipForCurrentTrainerAsync(Guid relationshipId, bool requireActive, CancellationToken cancellationToken = default);
    Task<TrainerClientRelationship> GetRelationshipForCurrentTrainerAndClientAsync(Guid clientId, bool requireActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a relationship where the CURRENT user is the client. Used by the
    /// paths where the subject of the data controls it, not the trainer.
    /// </summary>
    Task<TrainerClientRelationship> GetRelationshipForCurrentClientAsync(Guid relationshipId, bool requireActive, CancellationToken cancellationToken = default);
}
