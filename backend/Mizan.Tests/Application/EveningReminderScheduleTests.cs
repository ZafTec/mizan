using FluentAssertions;
using Mizan.Application.Planning;
using Xunit;

namespace Mizan.Tests.Application;

public class EveningReminderScheduleTests
{
    private const string Addis = "Africa/Addis_Ababa";

    [Theory]
    // Before, at and after the reminder time, while the window is still open.
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-22T10:00Z", null, "2026-10-22", "2026-10-22T16:30Z")]
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-22T16:30Z", null, "2026-10-22", "2026-10-22T16:30Z")]
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-22T16:45Z", null, "2026-10-22", "2026-10-22T16:30Z")]
    // The window has closed: tomorrow's.
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-22T17:00Z", null, "2026-10-23", "2026-10-23T16:30Z")]
    // Sent today already: tomorrow's. Sent yesterday: today's.
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-22T16:40Z", "2026-10-22T16:30Z", "2026-10-23", "2026-10-23T16:30Z")]
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-22T10:00Z", "2026-10-21T16:30Z", "2026-10-22", "2026-10-22T16:30Z")]
    // The plan's last window has closed, or the plan has not started: nothing, or its first day.
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-26T17:30Z", null, null, null)]
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-17T10:00Z", null, null, null)]
    [InlineData("20:00", Addis, "2026-10-20", 7, "2026-10-19T18:00Z", null, "2026-10-20", "2026-10-20T16:30Z")]
    // No window, no reminder.
    [InlineData(null, Addis, "2026-10-20", 7, "2026-10-22T10:00Z", null, null, null)]
    // A window that ends after midnight is reminded about late the evening before.
    [InlineData("00:15", Addis, "2026-10-20", 7, "2026-10-22T19:00Z", null, "2026-10-23", "2026-10-22T20:45Z")]
    // The local date is ahead of UTC's.
    [InlineData("20:00", "Pacific/Kiritimati", "2026-10-20", 7, "2026-10-22T08:00Z", null, "2026-10-23", "2026-10-23T05:30Z")]
    // The local date is behind UTC's, and on the day summer time ends.
    [InlineData("20:00", "America/New_York", "2026-10-26", 14, "2026-10-31T12:00Z", null, "2026-10-31", "2026-10-31T23:30Z")]
    [InlineData("20:00", "America/New_York", "2026-10-26", 14, "2026-11-01T12:00Z", null, "2026-11-01", "2026-11-02T00:30Z")]
    // An unknown zone is read as UTC.
    [InlineData("20:00", "Mars/Olympus", "2026-10-20", 7, "2026-10-22T10:00Z", null, "2026-10-22", "2026-10-22T19:30Z")]
    public void TheNextReminder_IsHalfAnHourBeforeTheWindowCloses_InThePersonsZone(
        string? windowEnd, string zone, string planStart, int durationDays, string now, string? lastSent, string? expectedDay, string? expectedAt)
    {
        var next = EveningReminderSchedule.Next(
            windowEnd is null ? null : TimeOnly.Parse(windowEnd),
            DateOnly.Parse(planStart),
            durationDays,
            Mizan.Domain.Streaks.StreakClock.Zone(zone),
            DateTimeOffset.Parse(now),
            lastSent is null ? null : DateTimeOffset.Parse(lastSent));

        if (expectedDay is null)
        {
            next.Should().BeNull();
            return;
        }

        next.Should().NotBeNull();
        next!.Value.Day.Should().Be(DateOnly.Parse(expectedDay));
        next.Value.At.Should().Be(DateTimeOffset.Parse(expectedAt!));
        next.Value.ClosesAt.Should().Be(next.Value.At + EveningReminderSchedule.Lead);
    }

    [Theory]
    [InlineData("2026-10-22", "2026-10-22T16:30Z", "2026-10-22", "2026-10-22T16:30Z", "2026-10-22T16:30Z", true)]
    [InlineData("2026-10-22", "2026-10-22T16:30Z", "2026-10-22", "2026-10-22T16:30Z", "2026-10-22T16:50Z", true)]
    // Run early, or for a window that has since moved (that one has a job of its own).
    [InlineData("2026-10-22", "2026-10-22T16:30Z", "2026-10-22", "2026-10-22T16:30Z", "2026-10-22T16:20Z", false)]
    [InlineData("2026-10-22", "2026-10-22T17:30Z", "2026-10-22", "2026-10-22T16:30Z", "2026-10-22T16:30Z", false)]
    // The day's window closed or was already reminded about, so the next one is tomorrow's.
    [InlineData("2026-10-23", "2026-10-23T16:30Z", "2026-10-22", "2026-10-22T16:30Z", "2026-10-22T17:05Z", false)]
    // The plan ended or lost its window.
    [InlineData(null, null, "2026-10-22", "2026-10-22T16:30Z", "2026-10-22T16:30Z", false)]
    public void AQueuedReminder_SendsOnlyWhileItIsStillTheNextOne(
        string? nextDay, string? nextAt, string jobDay, string jobAt, string now, bool expected)
    {
        EveningReminderDue? next = nextDay is null
            ? null
            : new EveningReminderDue(DateOnly.Parse(nextDay), DateTimeOffset.Parse(nextAt!), DateTimeOffset.Parse(nextAt!) + EveningReminderSchedule.Lead);

        EveningReminderSchedule.IsDue(next, DateOnly.Parse(jobDay), DateTimeOffset.Parse(jobAt), DateTimeOffset.Parse(now))
            .Should().Be(expected);
    }
}
