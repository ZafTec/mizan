using FluentAssertions;
using Mizan.Domain.Notifications;
using Xunit;

namespace Mizan.Tests.NotificationRules;

/// <summary>When a timed notification trigger next fires, on the recipient's wall clock.</summary>
public class NotificationOccurrencesTests
{
    public static TheoryData<string, string, string, string> Cases => new()
    {
        // trigger, zone, after (UTC), expected (UTC or "none")
        // Addis Ababa is UTC+3 all year: 07:00 is 04:00 UTC.
        { "daily 07:00", "Africa/Addis_Ababa", "2026-11-02T03:00:00Z", "2026-11-02T04:00:00Z" },
        { "daily 07:00", "Africa/Addis_Ababa", "2026-11-02T04:00:00Z", "2026-11-03T04:00:00Z" },
        // Tokyo, UTC+9: 07:00 local on the 3rd is 22:00 UTC on the 2nd.
        { "daily 07:00", "Asia/Tokyo", "2026-11-02T12:00:00Z", "2026-11-02T22:00:00Z" },
        // Weekdays Mon-Fri 07:00 Addis: from Saturday, the next is Monday.
        { "mon-fri 07:00", "Africa/Addis_Ababa", "2026-10-31T10:00:00Z", "2026-11-02T04:00:00Z" },
        { "mon-fri 07:00", "Africa/Addis_Ababa", "2026-11-06T05:00:00Z", "2026-11-09T04:00:00Z" },
        // New York moves with daylight saving: 07:00 EDT is 11:00 UTC, 07:00 EST is 12:00 UTC.
        { "daily 07:00", "America/New_York", "2026-10-31T12:00:00Z", "2026-11-01T12:00:00Z" },
        { "daily 07:00", "America/New_York", "2026-10-30T12:00:00Z", "2026-10-31T11:00:00Z" },
        // 02:30 does not exist on 2027-03-14 in New York: it fires at 03:30 EDT, 07:30 UTC.
        { "daily 02:30", "America/New_York", "2027-03-13T12:00:00Z", "2027-03-14T07:30:00Z" },
        // 01:30 happens twice on 2026-11-01: the first, EDT, 05:30 UTC.
        { "daily 01:30", "America/New_York", "2026-10-31T12:00:00Z", "2026-11-01T05:30:00Z" },
        // Once: in the future, or never again.
        { "once 2026-11-05T09:00:00Z", "Africa/Addis_Ababa", "2026-11-01T00:00:00Z", "2026-11-05T09:00:00Z" },
        { "once 2026-11-05T09:00:00Z", "Africa/Addis_Ababa", "2026-11-05T09:00:00Z", "none" },
        // No days chosen, or an event trigger: no timed occurrence.
        { "days-none 07:00", "UTC", "2026-11-01T00:00:00Z", "none" },
        { "event", "UTC", "2026-11-01T00:00:00Z", "none" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void NextOccurrence(string trigger, string zone, string after, string expected)
    {
        var next = NotificationOccurrences.Next(Parse(trigger), TimeZoneInfo.FindSystemTimeZoneById(zone), DateTimeOffset.Parse(after));

        if (expected == "none") next.Should().BeNull();
        else next.Should().Be(DateTimeOffset.Parse(expected));
    }

    private static NotificationTrigger Parse(string trigger)
    {
        var parts = trigger.Split(' ');
        return parts[0] switch
        {
            "daily" => NotificationTrigger.Daily(TimeOnly.Parse(parts[1])),
            "mon-fri" => NotificationTrigger.OnDays(TimeOnly.Parse(parts[1]), DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday),
            "days-none" => NotificationTrigger.OnDays(TimeOnly.Parse(parts[1])),
            "once" => NotificationTrigger.Once(DateTimeOffset.Parse(parts[1]).UtcDateTime),
            _ => NotificationTrigger.AfterEvent("plan.eating_window_end", -30),
        };
    }
}

/// <summary>Quiet hours hold Telegram, push and email until they end, on the person's clock.</summary>
public class QuietHoursTests
{
    [Theory]
    // start, end, now (UTC), zone, expected release (UTC)
    [InlineData("22:00", "07:00", "2026-11-02T20:00:00Z", "Africa/Addis_Ababa", "2026-11-03T04:00:00Z")] // 23:00 local
    [InlineData("22:00", "07:00", "2026-11-02T00:00:00Z", "Africa/Addis_Ababa", "2026-11-02T04:00:00Z")] // 03:00 local
    [InlineData("22:00", "07:00", "2026-11-02T10:00:00Z", "Africa/Addis_Ababa", "2026-11-02T10:00:00Z")] // 13:00 local, not quiet
    [InlineData("22:00", "07:00", "2026-11-02T04:00:00Z", "Africa/Addis_Ababa", "2026-11-02T04:00:00Z")] // 07:00 local, the end itself
    [InlineData("13:00", "15:00", "2026-11-02T11:00:00Z", "Africa/Addis_Ababa", "2026-11-02T12:00:00Z")] // 14:00 local
    [InlineData("22:00", "22:00", "2026-11-02T20:00:00Z", "Africa/Addis_Ababa", "2026-11-02T20:00:00Z")] // equal ends: none
    [InlineData("22:00", "07:00", "2026-11-01T04:00:00Z", "America/New_York", "2026-11-01T12:00:00Z")] // 00:00 EDT, ends 07:00 EST
    public void Release(string start, string end, string now, string zone, string expected)
    {
        QuietHours.Release(DateTimeOffset.Parse(now), TimeZoneInfo.FindSystemTimeZoneById(zone), TimeOnly.Parse(start), TimeOnly.Parse(end))
            .Should().Be(DateTimeOffset.Parse(expected));
    }

    [Fact]
    public void WithoutQuietHours_NothingWaits()
    {
        var now = DateTimeOffset.Parse("2026-11-02T20:00:00Z");
        QuietHours.Release(now, TimeZoneInfo.Utc, null, null).Should().Be(now);
    }
}

/// <summary>Every notification type belongs to a kind a person can turn off; marketing starts off.</summary>
public class NotificationKindsTests
{
    [Theory]
    [InlineData("achievement_unlocked", NotificationKinds.Achievements)]
    [InlineData("streak_milestone", NotificationKinds.Achievements)]
    [InlineData("evening_reminder", NotificationKinds.Reminders)]
    [InlineData("meal_plan_drafted", NotificationKinds.Planning)]
    [InlineData("trainer_request", NotificationKinds.Coaching)]
    [InlineData("household_invite", NotificationKinds.Household)]
    [InlineData("feed_comment", NotificationKinds.Social)]
    [InlineData("price_report", NotificationKinds.Account)]
    [InlineData("something_new", NotificationKinds.Account)]
    public void ATypeBelongsToAKind(string type, string kind) => NotificationKinds.ForType(type).Should().Be(kind);

    [Fact]
    public void MarketingStartsOffEverywhere_AndTheRestStartOnExceptEmail()
    {
        NotificationKinds.Info(NotificationKinds.Marketing).Defaults.Should().Be(NotificationChannels.None);
        NotificationKinds.All.Where(k => k.Kind != NotificationKinds.Marketing)
            .Should().OnlyContain(k => k.Defaults == (NotificationChannels.InApp | NotificationChannels.Telegram | NotificationChannels.Push));
    }
}
