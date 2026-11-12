using Mizan.Contracts.Admin;

namespace Mizan.Application.Interfaces;

/// <summary>
/// The key Mizan signs end-user tokens with (docs/AI.md#behind-a-gateway). The private key never
/// leaves Infrastructure: rotation makes and stores it there.
/// </summary>
public interface IEndUserSigningKeys
{
    /// <summary>
    /// Makes a new P-256 key and signs with it from the next call. The key it replaces stays
    /// published as the previous key, so tokens it signed verify until they expire.
    /// </summary>
    Task<EndUserKeyRotationResult> RotateAsync(Guid? rotatedByUserId, CancellationToken cancellationToken = default);
}
