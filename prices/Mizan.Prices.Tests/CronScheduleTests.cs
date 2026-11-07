using Xunit;
using FluentAssertions;
using Mizan.Prices.Scheduling;

namespace Mizan.Prices.Tests;

public class CronScheduleTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 3, 0, 30, DateTimeKind.Utc);

    [Theory]
    // Daily at 03:00 UTC: last ran yesterday, 03:00 today has passed.
    [InlineData("0 3 * * *", "2026-10-21T03:00:05Z", true)]
    // Already ran after today's occurrence.
    [InlineData("0 3 * * *", "2026-10-22T03:00:05Z", false)]
    // Ran a minute before the occurrence; the occurrence itself has since passed.
    [InlineData("0 3 * * *", "2026-10-22T02:59:00Z", true)]
    // Every six hours: last ran at midnight, 03:00 is not an occurrence, next is 06:00.
    [InlineData("0 */6 * * *", "2026-10-22T00:00:10Z", false)]
    // Weekly on Monday; 2026-10-22 is a Thursday and the last run was Tuesday.
    [InlineData("0 3 * * 1", "2026-10-20T03:00:00Z", false)]
    [InlineData("0 3 * * 1", "2026-10-18T03:00:00Z", true)]
    // Never ran: due now.
    [InlineData("0 3 * * *", null, true)]
    public void ASourceIsDue_WhenAnOccurrenceHasPassedSinceItsLastRun(string schedule, string? lastRun, bool expected)
    {
        DateTime? lastRunAt = lastRun is null ? null : DateTime.Parse(lastRun, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        CronSchedule.TryIsDue(schedule, lastRunAt, Now, out var due).Should().BeTrue();

        due.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("every day at three")]
    [InlineData("0 3 * *")]
    [InlineData("99 3 * * *")]
    public void AMissingOrInvalidSchedule_IsNeverDue(string? schedule)
    {
        CronSchedule.TryIsDue(schedule, null, Now, out var due).Should().BeFalse();

        due.Should().BeFalse();
    }

    [Fact]
    public void LastRunAtWithoutAKind_IsReadAsUtc()
    {
        var unspecified = new DateTime(2026, 10, 21, 3, 0, 5, DateTimeKind.Unspecified);

        CronSchedule.TryIsDue("0 3 * * *", unspecified, Now, out var due).Should().BeTrue();

        due.Should().BeTrue();
    }
}
