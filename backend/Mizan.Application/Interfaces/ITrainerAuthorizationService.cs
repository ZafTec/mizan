using Mizan.Domain.Entities;

namespace Mizan.Application.Interfaces;

public interface ITrainerAuthorizationService
{
    Guid GetCurrentUserId();
    Task EnsureTrainerAccessAsync(CancellationToken cancellationToken = default);
    Task<TrainerClientRelationship> GetRelationshipForCurrentTrainerAsync(Guid relationshipId, bool requireActive, CancellationToken cancellationToken = default);
    Task<TrainerClientRelationship> GetRelationshipForCurrentTrainerAndClientAsync(Guid clientId, bool requireActive, CancellationToken cancellationToken = default);
}
