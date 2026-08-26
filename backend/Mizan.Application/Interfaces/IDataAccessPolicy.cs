using Mizan.Domain.Ai;

namespace Mizan.Application.Interfaces;

/// <summary>
/// One question, one place: may this principal read this axis of this subject,
/// for this purpose?
///
/// It exists because the same rule was previously three ad-hoc checks in three
/// query handlers, and a fourth axis - measurements - was simply forgotten.
/// Adding the AI as a fourth consumer of that pattern would have guaranteed a
/// fourth miss. See docs/REFOCUS.md §11.
/// </summary>
public interface IDataAccessPolicy
{
    Task<bool> CanReadAsync(
        Guid principalId,
        Guid subjectId,
        DataAxis axis,
        AccessPurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The axes a principal may read of a subject. The AI context builder asks
    /// this and is handed only what comes back - it never receives the full log
    /// and filters afterwards.
    /// </summary>
    Task<IReadOnlySet<DataAxis>> ReadableAxesAsync(
        Guid principalId,
        Guid subjectId,
        AccessPurpose purpose,
        CancellationToken cancellationToken = default);
}
