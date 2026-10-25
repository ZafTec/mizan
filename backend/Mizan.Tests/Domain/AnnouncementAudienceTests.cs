using FluentAssertions;
using Mizan.Domain.Admin;
using Xunit;

namespace Mizan.Tests.AdminRules;

public class AnnouncementAudienceTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Home = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Sam = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static AnnouncementCandidate Person(
        bool verified = true, bool banned = false, DateTime? banEnds = null, bool pro = false, bool inHome = false, Guid? id = null) =>
        new(id ?? Sam, "sam@example.test", verified, banned, banEnds, pro, inHome ? [Home] : []);

    public static TheoryData<string, string, AnnouncementCandidate, bool> Cases => new()
    {
        { "everyone reaches a confirmed account", "all", Person(), true },
        { "nobody reaches an unconfirmed address", "all", Person(verified: false), false },
        { "nobody reaches a suspended account", "all", Person(banned: true), false },
        { "a suspension in the future still counts", "all", Person(banned: true, banEnds: Now.AddDays(1)), false },
        { "an ended suspension is lifted", "all", Person(banned: true, banEnds: Now.AddMinutes(-1)), true },
        { "pro reaches a subscriber", "pro", Person(pro: true), true },
        { "pro skips a free account", "pro", Person(), false },
        { "free reaches a free account", "free", Person(), true },
        { "free skips a subscriber", "free", Person(pro: true), false },
        { "a household reaches its member", $"household:{Home}", Person(inHome: true), true },
        { "a household skips anyone else", $"household:{Home}", Person(), false },
        { "a user audience reaches that user", $"user:{Sam}", Person(), true },
        { "a user audience skips anyone else", $"user:{Sam}", Person(id: Guid.NewGuid()), false },
        { "a user audience still skips a suspended user", $"user:{Sam}", Person(banned: true), false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Includes_FollowsTheAudienceAndLeavesOutWhoCannotReceiveMail(string _, string audience, AnnouncementCandidate person, bool included)
    {
        AnnouncementAudience.TryParse(audience, out var parsed).Should().BeTrue();
        parsed.Includes(person, Now).Should().Be(included);
    }

    [Theory]
    [InlineData("all", true, "all")]
    [InlineData(" PRO ", true, "pro")]
    [InlineData("Free", true, "free")]
    [InlineData("household:11111111-1111-4111-8111-111111111111", true, "household:11111111-1111-4111-8111-111111111111")]
    [InlineData("user:22222222-2222-4222-8222-222222222222", true, "user:22222222-2222-4222-8222-222222222222")]
    [InlineData("everyone", false, null)]
    [InlineData("all:extra", false, null)]
    [InlineData("household:", false, null)]
    [InlineData("household:not-a-guid", false, null)]
    [InlineData("user:00000000-0000-0000-0000-000000000000", false, null)]
    [InlineData("", false, null)]
    [InlineData(null, false, null)]
    public void TryParse_AcceptsTheFiveAudiencesAndSpellsThemOneWay(string? text, bool valid, string? canonical)
    {
        AnnouncementAudience.TryParse(text, out var parsed).Should().Be(valid);
        if (valid) parsed.ToString().Should().Be(canonical);
    }

    [Fact]
    public void Resolve_SendsOneMessagePerAddress_InAStableOrder()
    {
        var a = Guid.Parse("00000000-0000-4000-8000-00000000000a");
        var b = Guid.Parse("00000000-0000-4000-8000-00000000000b");
        var people = new[]
        {
            new AnnouncementCandidate(b, "same@example.test", true, false, null, false, []),
            new AnnouncementCandidate(a, "SAME@example.test", true, false, null, false, []),
            new AnnouncementCandidate(Sam, "sam@example.test", false, false, null, false, []),
        };

        AnnouncementAudience.TryParse("all", out var all);
        all.Resolve(people, Now).Select(c => c.UserId).Should().Equal(a);
    }
}
