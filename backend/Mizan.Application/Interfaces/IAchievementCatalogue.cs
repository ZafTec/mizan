using Mizan.Domain.Entities;

namespace Mizan.Application.Interfaces;

/// <summary>
/// The achievement catalogue, cached.
///
/// It is seed data plus whatever an admin has edited - a few dozen rows that
/// change a handful of times a year. Reading the table on every meal log to
/// find out whether "log 100 meals" exists is a scan nobody needs.
/// </summary>
public interface IAchievementCatalogue
{
    /// <summary>Everything with a criteria type, so everything a user can progress toward.</summary>
    Task<IReadOnlyList<Achievement>> MeasurableAsync(CancellationToken cancellationToken = default);

    /// <summary>Called by the admin write paths. Without this an edit takes a cache lifetime to appear.</summary>
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
