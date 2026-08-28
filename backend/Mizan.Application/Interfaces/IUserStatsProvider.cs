using Mizan.Domain.Achievements;

namespace Mizan.Application.Interfaces;

/// <summary>
/// The one place that answers "where does this user stand" for achievements.
///
/// Two copies of this used to exist - one in the evaluator, one in the
/// achievements query - and they disagreed about streaks. One implementation
/// means the badge you see progress toward is the badge you get.
/// </summary>
public interface IUserStatsProvider
{
    /// <summary>
    /// Only the criteria types asked for are computed; the rest stay zero.
    /// The expensive ones - lifetime volume, personal records - are never run
    /// on a path that does not need them.
    /// </summary>
    Task<UserActivityStats> BuildAsync(
        Guid userId,
        IReadOnlySet<string> criteriaTypes,
        CancellationToken cancellationToken = default);
}
