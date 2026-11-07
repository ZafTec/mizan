namespace Mizan.Application.Interfaces;

/// <summary>
/// The one answer to "may the current caller use this household". It is true
/// only when the user belongs to the household and, if the caller is a
/// connected app, the user also allowed that app to see it.
///
/// Handlers used to repeat the membership query inline, which left no place to
/// add the second condition. Anything that decides access to household data goes
/// through here (docs/ARCHITECTURE.md#households).
/// </summary>
public interface IHouseholdAccess
{
    Task<bool> CanAccessAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the caller may use a record that has an owner and may belong to a
    /// household. A connected app is held to its household setting even for
    /// records the user owns, because a list filed under a household is household
    /// data. The web app keeps the older rule: the owner always has access.
    /// </summary>
    Task<bool> CanAccessRecordAsync(Guid ownerId, Guid? householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses a household write that a connected app's grant does not cover, naming the
    /// permission: no household access at all, a household outside the selected ones, or (with
    /// <paramref name="householdId"/> null) a new household, which only a grant over all
    /// households could see afterwards. The web app is never refused here.
    /// </summary>
    void EnsureGrantCovers(Guid? householdId);

    /// <summary>Households the caller may use, as ids. Empty for an anonymous caller.</summary>
    Task<IReadOnlyList<Guid>> AccessibleIdsAsync(CancellationToken cancellationToken = default);
}
