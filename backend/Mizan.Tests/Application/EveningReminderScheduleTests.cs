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

    [Theory]
    // Plans in the order they are followed, each "name window start days" ("-" for no window); now is 13:00 in Addis Ababa.
    // A household plan alone: its window.
    [InlineData("home 20:00 2026-10-20 7", "home", "2026-10-22", "2026-10-22T16:30Z")]
    // The person's own plan comes first, with its own window.
    [InlineData("own 21:00 2026-10-20 7;home 20:00 2026-10-20 7", "own", "2026-10-22", "2026-10-22T17:30Z")]
    // Their own plan without a window still takes the day: no household reminder.
    [InlineData("own - 2026-10-20 7;home 20:00 2026-10-20 7", null, null, null)]
    // Their own plan ended yesterday: the household plan's.
    [InlineData("own - 2026-10-15 7;home 20:00 2026-10-20 7", "home", "2026-10-22", "2026-10-22T16:30Z")]
    // Their own plan ends today: tonight nothing, tomorrow the household plan's.
    [InlineData("own - 2026-10-16 7;home 20:00 2026-10-20 7", "home", "2026-10-23", "2026-10-23T16:30Z")]
    // No plan covers today or tomorrow.
    [InlineData("home 20:00 2026-10-25 7", null, null, null)]
    public void APersonIsRemindedAboutThePlanTheyFollowThatDay(string plans, string? expectedPlan, string? expectedDay, string? expectedAt)
    {
        var names = new Dictionary<Guid, string>();
        var followed = plans.Split(';').Select(text =>
        {
            var parts = text.Split(' ');
            var id = Guid.NewGuid();
            names[id] = parts[0];
            return new FollowedPlan(id, DateOnly.Parse(parts[2]), int.Parse(parts[3]), parts[1] == "-" ? null : TimeOnly.Parse(parts[1]), null);
        }).ToList();

        var next = EveningReminderSchedule.NextFor(
            followed, Mizan.Domain.Streaks.StreakClock.Zone(Addis), DateTimeOffset.Parse("2026-10-22T10:00Z"), null);

        if (expectedPlan is null)
        {
            next.Should().BeNull();
            return;
        }

        next.Should().NotBeNull();
        names[next!.Value.Plan.PlanId].Should().Be(expectedPlan);
        next.Value.Due.Day.Should().Be(DateOnly.Parse(expectedDay!));
        next.Value.Due.At.Should().Be(DateTimeOffset.Parse(expectedAt!));
    }
}
