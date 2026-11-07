using FluentAssertions;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Xunit;

namespace Mizan.Tests.NotificationRules;

/// <summary>Who a campaign reaches, when it fires, what may be done to it and how its report adds up.</summary>
public class CampaignRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

    private static AudienceCandidate Person(
        string plan = CampaignPlans.Free,
        int signedUpDaysAgo = 30,
        int activeDaysAgo = 0,
        bool telegram = false,
        bool household = false,
        string? zone = null,
        bool banned = false) =>
        new(Guid.NewGuid(), Now.AddDays(-signedUpDaysAgo), plan, Now.AddDays(-activeDaysAgo), telegram, household, zone, banned);

    private static NotificationAudience Filters(
        string[]? plans = null,
        DateTime? from = null,
        DateTime? before = null,
        int? inactive = null,
        bool? telegram = null,
        bool? household = null,
        string[]? zones = null) => new()
        {
            Kind = NotificationAudienceKind.Segment,
            Plans = [.. plans ?? []],
            SignedUpFrom = from,
            SignedUpBefore = before,
            InactiveDays = inactive,
            TelegramLinked = telegram,
            HasHousehold = household,
            TimeZones = [.. zones ?? []],
        };

    public static TheoryData<string, NotificationAudience, AudienceCandidate, bool> Audiences => new()
    {
        { "no filters is everyone", Filters(), Person(), true },
        { "a suspended account is never included", Filters(), Person(banned: true), false },
        { "plan pro takes Pro", Filters(plans: ["pro"]), Person(CampaignPlans.Pro), true },
        { "plan pro leaves free out", Filters(plans: ["pro"]), Person(CampaignPlans.Free), false },
        { "plan pro leaves lifetime out", Filters(plans: ["pro"]), Person(CampaignPlans.Lifetime), false },
        { "several plans take any of them", Filters(plans: ["free", "lifetime"]), Person(CampaignPlans.Lifetime), true },
        { "signed up inside the range", Filters(from: Now.AddDays(-40), before: Now.AddDays(-20)), Person(signedUpDaysAgo: 30), true },
        { "signed up before the range", Filters(from: Now.AddDays(-20)), Person(signedUpDaysAgo: 30), false },
        { "the range end is exclusive", Filters(before: Now.AddDays(-30)), Person(signedUpDaysAgo: 30), false },
        { "quiet for 14 days, filter 7", Filters(inactive: 7), Person(activeDaysAgo: 14), true },
        { "active 3 days ago, filter 7", Filters(inactive: 7), Person(activeDaysAgo: 3), false },
        { "exactly 7 quiet days counts", Filters(inactive: 7), Person(activeDaysAgo: 7), true },
        { "linked to Telegram", Filters(telegram: true), Person(telegram: true), true },
        { "not linked when linked is asked", Filters(telegram: true), Person(telegram: false), false },
        { "not linked asked for", Filters(telegram: false), Person(telegram: false), true },
        { "in a household", Filters(household: true), Person(household: true), true },
        { "in no household", Filters(household: false), Person(household: true), false },
        { "time zone matches", Filters(zones: ["Africa/Addis_Ababa"]), Person(zone: "Africa/Addis_Ababa"), true },
        { "no zone counts as UTC", Filters(zones: ["UTC"]), Person(zone: null), true },
        { "another zone", Filters(zones: ["Europe/London"]), Person(zone: "Africa/Addis_Ababa"), false },
        { "every filter must match", Filters(plans: ["pro"], telegram: true), Person(CampaignPlans.Pro, telegram: false), false },
    };

    [Theory]
    [MemberData(nameof(Audiences))]
    public void APersonIsInTheAudienceWhenEveryFilterMatches(string story, NotificationAudience filters, AudienceCandidate person, bool included) =>
        CampaignAudienceRules.Matches(filters, person, Now).Should().Be(included, story);

    public static TheoryData<NotificationAudience, string?> Checks => new()
    {
        { Filters(), null },
        { Filters(plans: ["gold"]), "\"gold\" is not a plan. Use free, pro or lifetime." },
        { Filters(from: Now, before: Now.AddDays(-1)), "The sign-up range ends before it starts." },
        { Filters(inactive: 0), "Inactive days is between 1 and 365." },
        { Filters(inactive: 400), "Inactive days is between 1 and 365." },
        { Filters(zones: ["Mars/Olympus"]), "\"Mars/Olympus\" is not a time zone." },
        { new NotificationAudience { Kind = NotificationAudienceKind.User }, "A campaign's audience is a set of filters." },
    };

    [Theory]
    [MemberData(nameof(Checks))]
    public void FiltersThatCannotBeUsedAreRefused(NotificationAudience filters, string? error) =>
        CampaignAudienceRules.Check(filters).Should().Be(error);

    [Theory]
    // created, last seen, last logged day, expected last active
    [InlineData("2026-10-01T10:00:00Z", null, null, "2026-10-01T10:00:00Z")]
    [InlineData("2026-10-01T10:00:00Z", "2026-10-20T08:00:00Z", null, "2026-10-20T08:00:00Z")]
    [InlineData("2026-10-01T10:00:00Z", "2026-10-20T08:00:00Z", "2026-10-25", "2026-10-25T00:00:00Z")]
    [InlineData("2026-10-01T10:00:00Z", "2026-10-28T08:00:00Z", "2026-10-25", "2026-10-28T08:00:00Z")]
    public void LastActiveIsTheLatestOfSignUpVisitAndLog(string created, string? seen, string? logged, string expected)
    {
        static DateTime At(string s) => DateTime.Parse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        CampaignAudienceRules.LastActive(At(created), seen is null ? null : At(seen), logged is null ? null : DateOnly.Parse(logged))
            .Should().Be(At(expected));
    }

    [Theory]
    // event, days, offset in minutes or the error
    [InlineData("user.signed_up", 1, "1440")]
    [InlineData("user.signed_up", 0, "0")]
    [InlineData("user.inactive", 14, "20160")]
    [InlineData("user.inactive", 0, "error: Inactive needs at least one quiet day.")]
    [InlineData("subscription.pro_ends", 3, "-4320")]
    [InlineData("subscription.pro_ends", 400, "error: Days is between 0 and 365.")]
    [InlineData("user.birthday", 1, "error: The event is user.signed_up, user.inactive or subscription.pro_ends.")]
    public void AnEventTriggerFollowsOrPrecedesItsEvent(string name, int days, string expected)
    {
        var result = CampaignTriggerRules.ForEvent(name, days);
        if (expected.StartsWith("error: ", StringComparison.Ordinal))
        {
            result.Trigger.Should().BeNull();
            result.Error.Should().Be(expected["error: ".Length..]);
            return;
        }

        result.Trigger!.Kind.Should().Be(NotificationTriggerKind.EventOffset);
        result.Trigger.Event.Should().Be(name);
        result.Trigger.OffsetMinutes.Should().Be(int.Parse(expected));
        CampaignTriggerRules.EventDays(result.Trigger).Should().Be(days);
    }

    [Fact]
    public void AnOccurrenceIsTheEventPlusTheOffsetToTheMinute() =>
        CampaignTriggerRules.Occurrence(new DateTime(2026, 11, 1, 9, 14, 37, DateTimeKind.Utc), 24 * 60)
            .Should().Be(new DateTimeOffset(2026, 11, 2, 9, 14, 0, TimeSpan.Zero));

    [Theory]
    // lifetime, status, scheduled action, scheduled at, period end, expected end
    [InlineData(true, "active", "cancel", "2026-12-01", "2026-12-01", null)]
    [InlineData(false, "active", null, null, "2026-12-01", null)]
    [InlineData(false, "active", "cancel", "2026-11-20", "2026-12-01", "2026-11-20")]
    [InlineData(false, "active", "cancel", null, "2026-12-01", "2026-12-01")]
    [InlineData(false, "canceled", null, null, "2026-12-01", "2026-12-01")]
    [InlineData(false, "active", "pause", "2026-11-20", "2026-12-01", null)]
    public void ProEndsOnlyWhenTheSubscriptionIsSetToStop(bool lifetime, string status, string? action, string? at, string? periodEnd, string? expected)
    {
        static DateTime? Day(string? s) => s is null ? null : DateTime.SpecifyKind(DateTime.Parse(s), DateTimeKind.Utc);
        CampaignTriggerRules.ProEndsAt(lifetime, status, action, Day(at), Day(periodEnd)).Should().Be(Day(expected));
    }

    [Theory]
    [InlineData(CampaignStatus.Draft, CampaignAction.Edit, CampaignStatus.Draft, null)]
    [InlineData(CampaignStatus.Paused, CampaignAction.Edit, CampaignStatus.Paused, null)]
    [InlineData(CampaignStatus.Active, CampaignAction.Edit, null, "Pause the campaign before you change it.")]
    [InlineData(CampaignStatus.Draft, CampaignAction.Activate, CampaignStatus.Active, null)]
    [InlineData(CampaignStatus.Paused, CampaignAction.Activate, null, "Only a draft can be started.")]
    [InlineData(CampaignStatus.Active, CampaignAction.Pause, CampaignStatus.Paused, null)]
    [InlineData(CampaignStatus.Draft, CampaignAction.Pause, null, "Only an active campaign can be paused.")]
    [InlineData(CampaignStatus.Paused, CampaignAction.Resume, CampaignStatus.Active, null)]
    [InlineData(CampaignStatus.Active, CampaignAction.Resume, null, "Only a paused campaign can be resumed.")]
    [InlineData(CampaignStatus.Active, CampaignAction.Cancel, CampaignStatus.Cancelled, null)]
    [InlineData(CampaignStatus.Draft, CampaignAction.Cancel, CampaignStatus.Cancelled, null)]
    [InlineData(CampaignStatus.Cancelled, CampaignAction.Cancel, null, "The campaign is cancelled.")]
    [InlineData(CampaignStatus.Cancelled, CampaignAction.Edit, null, "The campaign is cancelled.")]
    public void ACampaignMovesBetweenStatusesOnlyAsAllowed(CampaignStatus from, CampaignAction action, CampaignStatus? next, string? error) =>
        CampaignRules.Apply(from, action).Should().Be((next, error));

    [Theory]
    [InlineData(CampaignStatus.Draft, NotificationTriggerKind.Once, true, "draft")]
    [InlineData(CampaignStatus.Active, NotificationTriggerKind.Once, true, "scheduled")]
    [InlineData(CampaignStatus.Active, NotificationTriggerKind.Once, false, "sent")]
    [InlineData(CampaignStatus.Active, NotificationTriggerKind.EventOffset, false, "live")]
    [InlineData(CampaignStatus.Paused, NotificationTriggerKind.Daily, true, "paused")]
    [InlineData(CampaignStatus.Cancelled, NotificationTriggerKind.Daily, false, "cancelled")]
    public void TheListShowsAPhase(CampaignStatus status, NotificationTriggerKind trigger, bool hasNext, string phase) =>
        CampaignRules.Phase(status, trigger, hasNext ? Now : null).Should().Be(phase);

    [Theory]
    // channel, index, minutes after the occurrence
    [InlineData(NotificationChannels.Email, 0, 0)]
    [InlineData(NotificationChannels.Email, 119, 0)]
    [InlineData(NotificationChannels.Email, 120, 1)]
    [InlineData(NotificationChannels.Email, 1000, 8)]
    [InlineData(NotificationChannels.Telegram, 1199, 0)]
    [InlineData(NotificationChannels.Telegram, 2400, 2)]
    [InlineData(NotificationChannels.InApp, 999, 0)]
    public void ALargeAudienceIsSpreadOverMinutesPerChannel(NotificationChannels channel, int index, int minutes) =>
        CampaignRules.RunAfter(Now, index, channel).Should().Be(Now.AddMinutes(minutes));

    [Theory]
    [InlineData("Hi {firstName}, see {appLink}", "Abebe Kebede", "Hi Abebe, see https://mizan.test/")]
    [InlineData("Hi {firstName}", null, "Hi there")]
    [InlineData("Hi {firstName}", "   ", "Hi there")]
    [InlineData("No placeholders", "Abebe", "No placeholders")]
    public void PlaceholdersAreFilledForEachRecipient(string text, string? name, string expected) =>
        NotificationPlaceholders.Render(text, NotificationPlaceholders.FirstNameOf(name), "https://mizan.test/").Should().Be(expected);

    [Theory]
    [InlineData("Hi {firstName} {appLink}", "")]
    [InlineData("Hi {name}", "{name}")]
    [InlineData("{first} and {last} and {first}", "{first},{last}")]
    public void UnknownPlaceholdersAreNamed(string text, string unknown) =>
        string.Join(',', NotificationPlaceholders.Unknown(text)).Should().Be(unknown);

    [Fact]
    public void TheInAppLineCountsRowsAndReads()
    {
        // Ten deliveries ran, two wait, one gave up; eight wrote a row (two were turned off), three were read.
        var tally = CampaignReport.Tally(NotificationChannels.InApp, new JobCounts(2, 10, 1), JobCounts.None, rows: 8, read: 3);
        tally.Should().Be(new CampaignChannelTally(NotificationChannels.InApp, 2, 8, 1, 2, 3));
    }

    [Fact]
    public void AnOutsideChannelCountsItsOwnJobs()
    {
        // Ten deliveries ran: seven queued a Telegram message, three were left out by the person's choices.
        // Of the seven, five went, one is retrying, one gave up. One delivery is still waiting.
        var tally = CampaignReport.Tally(NotificationChannels.Telegram, new JobCounts(1, 10, 0), new JobCounts(1, 5, 1));
        tally.Should().Be(new CampaignChannelTally(NotificationChannels.Telegram, 2, 5, 1, 3, null));
    }
}
