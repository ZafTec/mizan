using Mizan.Domain.Entities;

namespace Mizan.Application.Interfaces;

/// <summary>
/// The picture a notification about an achievement carries. The achievement's own icon when it has
/// one; otherwise a branded card rendered once, stored through <see cref="IStorageService"/> and
/// remembered on the achievement. Null when neither can be had, for example when storage has no
/// public address Telegram could fetch the card from.
/// </summary>
public interface IAchievementCards
{
    Task<string?> ImageUrlAsync(Achievement achievement, CancellationToken cancellationToken = default);
}
