namespace Mizan.Domain.Admin;

public enum AudienceKind
{
    All,
    Pro,
    Free,
    Household,
    User,
}

/// <summary>Who an announcement reaches, as the database knows them.</summary>
public sealed record AnnouncementCandidate(
    Guid UserId,
    string Email,
    bool EmailVerified,
    bool Banned,
    DateTime? BanExpires,
    bool IsPro,
    IReadOnlyCollection<Guid> HouseholdIds);

/// <summary>
/// The audience an administrator picks for an announcement: <c>all</c>,
/// <c>pro</c>, <c>free</c>, <c>household:{id}</c> or <c>user:{id}</c>.
///
/// Every audience leaves out an address nobody confirmed and an account that
/// is suspended right now, because a mass email to either is how a sender
/// earns a spam reputation. A suspension that has ended counts as lifted.
/// </summary>
public sealed record AnnouncementAudience(AudienceKind Kind, Guid? Id)
{
    public static bool TryParse(string? value, out AnnouncementAudience audience)
    {
        audience = new AnnouncementAudience(AudienceKind.All, null);
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        var colon = text.IndexOf(':');
        var head = (colon < 0 ? text : text[..colon]).ToLowerInvariant();
        var tail = colon < 0 ? null : text[(colon + 1)..];

        switch (head)
        {
            case "all" when tail is null:
                audience = new(AudienceKind.All, null);
                return true;
            case "pro" when tail is null:
                audience = new(AudienceKind.Pro, null);
                return true;
            case "free" when tail is null:
                audience = new(AudienceKind.Free, null);
                return true;
            case "household" when Guid.TryParse(tail, out var householdId) && householdId != Guid.Empty:
                audience = new(AudienceKind.Household, householdId);
                return true;
            case "user" when Guid.TryParse(tail, out var userId) && userId != Guid.Empty:
                audience = new(AudienceKind.User, userId);
                return true;
            default:
                return false;
        }
    }

    /// <summary>The canonical spelling, which is what is stored.</summary>
    public override string ToString() => Kind switch
    {
        AudienceKind.Household => $"household:{Id}",
        AudienceKind.User => $"user:{Id}",
        _ => Kind.ToString().ToLowerInvariant(),
    };

    /// <summary>Everyone, which is the audience the hourly limit applies to.</summary>
    public bool IsEveryone => Kind == AudienceKind.All;

    public bool Includes(AnnouncementCandidate candidate, DateTime now)
    {
        if (!candidate.EmailVerified || string.IsNullOrWhiteSpace(candidate.Email)) return false;
        if (candidate.Banned && (candidate.BanExpires is null || candidate.BanExpires > now)) return false;

        return Kind switch
        {
            AudienceKind.All => true,
            AudienceKind.Pro => candidate.IsPro,
            AudienceKind.Free => !candidate.IsPro,
            AudienceKind.Household => candidate.HouseholdIds.Contains(Id!.Value),
            AudienceKind.User => candidate.UserId == Id,
            _ => false,
        };
    }

    /// <summary>One message per address, in a stable order, so a retry queues the same set.</summary>
    public IReadOnlyList<AnnouncementCandidate> Resolve(IEnumerable<AnnouncementCandidate> candidates, DateTime now) =>
        candidates
            .Where(c => Includes(c, now))
            .GroupBy(c => c.Email.Trim().ToLowerInvariant())
            .Select(g => g.OrderBy(c => c.UserId).First())
            .OrderBy(c => c.UserId)
            .ToList();
}
