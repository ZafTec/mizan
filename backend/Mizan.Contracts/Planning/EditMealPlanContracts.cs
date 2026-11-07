namespace Mizan.Contracts.Planning;

/// <summary>
/// One change in a batch edit of a plan. <c>Action</c> is add_entry, update_entry, add_prep or
/// update_prep: an entry change carries <see cref="Entry"/> (and <see cref="EntryId"/> to update),
/// a prep change carries <see cref="Session"/> (and <see cref="SessionId"/> to update).
/// </summary>
public sealed record MealPlanChange(
    string Action,
    Guid? EntryId = null,
    Guid? SessionId = null,
    PlanEntryRequest? Entry = null,
    PrepSessionRequest? Session = null);

/// <summary>
/// What a batch of planning changes (a plan's entries, a list's items) came to. The changes are
/// made one after another and are not one transaction: <see cref="Completed"/> are saved,
/// <see cref="Failed"/> says what stopped the batch, and <see cref="Remaining"/> lists the indexes
/// still to send, so a retry repeats only those.
/// </summary>
public sealed record PlanningBatchReceipt(
    bool Complete,
    IReadOnlyList<PlanningChangeDone> Completed,
    PlanningChangeFailed? Failed,
    IReadOnlyList<int> Remaining,
    string? Guidance);

/// <summary>A change that was saved, with the id of what it made or changed.</summary>
public sealed record PlanningChangeDone(int Index, string Action, Guid? Id);

/// <summary>The change that stopped the batch. When <see cref="MayHaveBeenSaved"/>, read it back before sending it again.</summary>
public sealed record PlanningChangeFailed(int Index, string Action, string Error, bool MayHaveBeenSaved);
