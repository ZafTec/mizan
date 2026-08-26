namespace Mizan.Domain.Entities;

/// <summary>
/// The binding between a Telegram account and a Mizan one.
///
/// A chat id is not an identity - anyone can send the bot a message claiming
/// to be anyone. This row is the only thing that turns an incoming chat into a
/// user, and it can only be created by consuming a single-use code the user
/// generated while signed in on the web (docs/REFOCUS.md §13).
///
/// One-to-one in both directions. A second Telegram account cannot attach
/// itself to a linked user, and a Telegram account that switches Mizan users
/// replaces its link rather than accumulating one.
/// </summary>
public class TelegramLink
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Telegram's own user id. 64-bit; theirs, not ours.</summary>
    public long TelegramUserId { get; set; }

    /// <summary>Display only, and optional - a Telegram account need not have one.</summary>
    public string? TelegramUsername { get; set; }

    public DateTime LinkedAt { get; set; }

    /// <summary>Last message the bot handled for this link. Shown in settings so
    /// "is this still connected?" has an answer.</summary>
    public DateTime? LastSeenAt { get; set; }

    public virtual User? User { get; set; }
}
