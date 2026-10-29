using FluentAssertions;
using Mizan.Domain.Notifications;
using Xunit;

namespace Mizan.Tests.NotificationRules;

/// <summary>How a reminder request becomes a trigger on the person's clock, and what is refused.</summary>
public class ReminderRulesTests
{
    private static readonly DateTimeOffset SundayNoonAddis = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string, string> Timings => new()
    {
        // timing, zone, first firing after Sunday 2026-11-01 12:00 Addis (09:00 UTC) or the error, then the summary
        // Weekdays at 07:00 in Addis Ababa: Monday 04:00 UTC.
        { "days 07:00 mon,tue,wed,thu,fri", "Africa/Addis_Ababa", "2026-11-02T04:00:00Z", "Weekdays at 07:00" },
        { "days 18:30 sat,sun", "Africa/Addis_Ababa", "2026-11-01T15:30:00Z", "Weekends at 18:30" },
        { "days 07:00 mon,wed,fri", "Africa/Addis_Ababa", "2026-11-02T04:00:00Z", "Mon, Wed, Fri at 07:00" },
        { "daily 21:00", "Africa/Addis_Ababa", "2026-11-01T18:00:00Z", "Every day at 21:00" },
        // Once later today, and a time already past.
        { "once 2026-11-01 15:00", "Africa/Addis_Ababa", "2026-11-01T12:00:00Z", "Once, Sun 1 Nov at 15:00" },
        { "once 2026-11-01 08:00", "Africa/Addis_Ababa", "error: That time has already passed.", "" },
        // Every 3 days at 08:00 from today: today's 08:00 has passed, so Wednesday.
        { "every-days 3 08:00", "Africa/Addis_Ababa", "2026-11-04T05:00:00Z", "Every 3 days at 08:00" },
        { "every-days 7 08:00", "Africa/Addis_Ababa", "2026-11-08T05:00:00Z", "Every week at 08:00" },
        // Hourly from now, the smallest interval allowed; anything shorter is refused.
        { "every-hours 1", "Africa/Addis_Ababa", "2026-11-01T10:00:00Z", "Every hour" },
        { "every-minutes 90", "Africa/Addis_Ababa", "2026-11-01T10:30:00Z", "Every 90 minutes" },
        { "every-minutes 30", "Africa/Addis_Ababa", "error: A reminder can repeat at most once an hour.", "" },
        { "every-minutes 59", "UTC", "error: A reminder can repeat at most once an hour.", "" },
        // Malformed requests.
        { "days 07:00 ", "UTC", "error: Choose at least one day.", "" },
        { "days 07:00 funday", "UTC", "error: \"funday\" is not a day. Use mon, tue, wed, thu, fri, sat or sun.", "" },
        { "daily 7am", "UTC", "error: Give the time as HH:mm, such as 07:00.", "" },
        { "hourly 07:00", "UTC", "error: Repeat is once, daily, days, every or monthly.", "" },
        // Monthly: today is the first Sunday, and its 08:00 has passed, so December's.
        { "monthly-week first sun 08:00", "Africa/Addis_Ababa", "2026-12-06T05:00:00Z", "First Sunday of the month at 08:00" },
        { "monthly-week first sun 15:00", "Africa/Addis_Ababa", "2026-11-01T12:00:00Z", "First Sunday of the month at 15:00" },
        { "monthly-week last fri 20:00", "Africa/Addis_Ababa", "2026-11-27T17:00:00Z", "Last Friday of the month at 20:00" },
        { "monthly-day 15 08:00", "Africa/Addis_Ababa", "2026-11-15T05:00:00Z", "The 15th of each month at 08:00" },
        // The 31st in a 30-day month is its last day.
        { "monthly-day 31 08:00", "Africa/Addis_Ababa", "2026-11-30T05:00:00Z", "The last day of the month at 08:00" },
        { "monthly 08:00", "UTC", "error: Say which day of the month: a date such as the 15th, or a weekday such as the first Sunday.", "" },
        { "monthly-day 32 08:00", "UTC", "error: The day of the month is 1 to 31; 31 is the last day of every month.", "" },
        { "monthly-week fifth sun 08:00", "UTC", "error: The week is first, second, third, fourth or last.", "" },
        { "monthly-week first sat,sun 08:00", "UTC", "error: Choose one weekday, such as sun, for a monthly reminder.", "" },
    };

    public static TheoryData<string, string, string, string> MonthlyFirings => new()
    {
        // monthly timing, zone, after (UTC), next firing (UTC)
        // A 31st in February is the 28th, or the 29th in a leap year.
        { "monthly-day 31 08:00", "UTC", "2027-01-31T08:00:00Z", "2027-02-28T08:00:00Z" },
        { "monthly-day 31 08:00", "UTC", "2028-02-01T00:00:00Z", "2028-02-29T08:00:00Z" },
        { "monthly-day 30 08:00", "UTC", "2027-02-01T00:00:00Z", "2027-02-28T08:00:00Z" },
        { "monthly-day 31 08:00", "UTC", "2026-12-01T00:00:00Z", "2026-12-31T08:00:00Z" },
        // The last Sunday, after November's has fired: December's, the 27th.
        { "monthly-week last sun 08:00", "UTC", "2026-11-29T08:00:00Z", "2026-12-27T08:00:00Z" },
        // 08:00 New York on the morning the clocks go back is 13:00 UTC, not 12:00.
        { "monthly-week first sun 08:00", "America/New_York", "2026-10-05T00:00:00Z", "2026-11-01T13:00:00Z" },
        { "monthly-week first sun 08:00", "America/New_York", "2026-09-01T00:00:00Z", "2026-09-06T12:00:00Z" },
        // 02:30 on the spring-forward night does not happen: it fires at 03:30.
        { "monthly-week second sun 02:30", "America/New_York", "2026-03-01T00:00:00Z", "2026-03-08T07:30:00Z" },
    };

    [Theory]
    [MemberData(nameof(MonthlyFirings))]
    public void AMonthlyReminderFindsItsDayInEveryMonth(string timing, string zone, string after, string next)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var trigger = ReminderRules.Trigger(Parse(timing), tz, SundayNoonAddis).Trigger!;

        NotificationOccurrences.Next(trigger, tz, DateTimeOffset.Parse(after)).Should().Be(DateTimeOffset.Parse(next));
    }

    [Theory]
    [MemberData(nameof(Timings))]
    public void ATimingBecomesATriggerOnThePersonsClock(string timing, string zone, string expected, string summary)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var result = ReminderRules.Trigger(Parse(timing), tz, SundayNoonAddis);

        if (expected.StartsWith("error: ", StringComparison.Ordinal))
        {
            result.Trigger.Should().BeNull();
            result.Error.Should().Be(expected["error: ".Length..]);
            return;
        }

        result.Error.Should().BeNull();
        var first = result.Trigger!.Kind is NotificationTriggerKind.Once or NotificationTriggerKind.Interval
            ? new DateTimeOffset(result.Trigger.At!.Value)
            : NotificationOccurrences.Next(result.Trigger, tz, SundayNoonAddis);
        first.Should().Be(DateTimeOffset.Parse(expected));
        NotificationOccurrences.Next(result.Trigger, tz, SundayNoonAddis).Should().Be(DateTimeOffset.Parse(expected));
        ReminderRules.Describe(result.Trigger, tz).Should().Be(summary);
    }

    public static TheoryData<int, string, string, string> Intervals => new()
    {
        // every N minutes, first firing (UTC), after (UTC), next (UTC)
        { 60, "2026-11-01T10:00:00Z", "2026-11-01T10:00:00Z", "2026-11-01T11:00:00Z" },
        { 120, "2026-11-01T10:00:00Z", "2026-11-01T13:59:00Z", "2026-11-01T14:00:00Z" },
        // Every day at 07:00 New York keeps 07:00 across the clocks going back.
        { 1440, "2026-10-31T11:00:00Z", "2026-10-31T11:00:00Z", "2026-11-01T12:00:00Z" },
        { 4320, "2026-10-30T11:00:00Z", "2026-11-01T00:00:00Z", "2026-11-02T12:00:00Z" },
    };

    [Theory]
    [MemberData(nameof(Intervals))]
    public void AnIntervalOfWholeDaysKeepsTheWallClockTime(int minutes, string first, string after, string next)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var start = DateTimeOffset.Parse(first);
        var trigger = NotificationTrigger.Every(start.UtcDateTime, minutes, minutes % 1440 == 0 ? TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime) : null);

        NotificationOccurrences.Next(trigger, zone, DateTimeOffset.Parse(after)).Should().Be(DateTimeOffset.Parse(next));
    }

    public static TheoryData<string, string, int, string> PreCooks => new()
    {
        // plan day, prep time, lead minutes, expected (UTC) or error; now is Sunday 12:00 Addis
        { "2026-11-02", "18:00", 60, "2026-11-02T14:00:00Z" },
        { "2026-11-01", "18:00", 0, "2026-11-01T15:00:00Z" },
        { "2026-11-01", "12:30", 60, "error: That prep session is too soon, or already past, to remind you about." },
        { "2026-11-02", "18:00", 2000, "error: Remind between 0 and 24 hours before the prep." },
    };

    [Theory]
    [MemberData(nameof(PreCooks))]
    public void APreCookReminderIsReadFromThePlanDay(string day, string time, int lead, string expected)
    {
        var result = ReminderRules.PreCook(DateOnly.Parse(day), TimeOnly.Parse(time), lead, TimeZoneInfo.FindSystemTimeZoneById("Africa/Addis_Ababa"), SundayNoonAddis);

        if (expected.StartsWith("error: ", StringComparison.Ordinal)) result.Error.Should().Be(expected["error: ".Length..]);
        else new DateTimeOffset(result.Trigger!.At!.Value).Should().Be(DateTimeOffset.Parse(expected));
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 30)]
    public void TheActiveCapDependsOnThePlan(bool isPro, int limit) => ReminderRules.ActiveLimit(isPro).Should().Be(limit);

    [Theory]
    [InlineData(30, null)]
    [InlineData(5, null)]
    [InlineData(4, "Snooze for between 5 minutes and 12 hours.")]
    [InlineData(721, "Snooze for between 5 minutes and 12 hours.")]
    public void SnoozeLengths(int minutes, string? error) => ReminderRules.CheckSnooze(minutes).Should().Be(error);

    [Fact]
    public void SkippingTodayHoldsUntilThePersonsMidnight() =>
        ReminderRules.EndOfToday(SundayNoonAddis, TimeZoneInfo.FindSystemTimeZoneById("Africa/Addis_Ababa"))
            .Should().Be(DateTimeOffset.Parse("2026-11-01T21:00:00Z"));

    private static ReminderTiming Parse(string timing)
    {
        var parts = timing.Split(' ');
        return parts[0] switch
        {
            "days" => new ReminderTiming("days", parts[1], parts.Length > 2 && parts[2].Length > 0 ? parts[2].Split(',') : []),
            "once" => new ReminderTiming("once", parts[2], Date: parts[1]),
            "every-days" => new ReminderTiming("every", parts[2], EveryDays: int.Parse(parts[1])),
            "every-hours" => new ReminderTiming("every", EveryHours: int.Parse(parts[1])),
            "every-minutes" => new ReminderTiming("every", EveryMinutes: int.Parse(parts[1])),
            "monthly-day" => new ReminderTiming("monthly", parts[2], DayOfMonth: int.Parse(parts[1])),
            "monthly-week" => new ReminderTiming("monthly", parts[3], parts[2].Split(','), Week: parts[1]),
            _ => new ReminderTiming(parts[0], parts[1]),
        };
    }
}
