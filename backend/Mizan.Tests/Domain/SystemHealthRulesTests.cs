using FluentAssertions;
using Mizan.Domain.Admin;
using Xunit;

namespace Mizan.Tests.AdminRules;

public class SystemHealthRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, HealthVerdict, SystemHealthStatus> Cases => new()
    {
        { "a quick round trip", SystemHealthRules.RoundTrip(true, TimeSpan.FromMilliseconds(4), null), SystemHealthStatus.Ok },
        { "a slow round trip", SystemHealthRules.RoundTrip(true, TimeSpan.FromSeconds(2), null), SystemHealthStatus.Degraded },
        { "no answer", SystemHealthRules.RoundTrip(true, null, "Failed: SocketException."), SystemHealthStatus.Down },
        { "no connection configured", SystemHealthRules.RoundTrip(false, null, null), SystemHealthStatus.NotConfigured },

        { "an empty queue", SystemHealthRules.Outbox(0, 0, null), SystemHealthStatus.Ok },
        { "fresh pending work", SystemHealthRules.Outbox(3, 0, TimeSpan.FromMinutes(1)), SystemHealthStatus.Ok },
        { "a dead-lettered job", SystemHealthRules.Outbox(0, 1, null), SystemHealthStatus.Degraded },
        { "work waiting a quarter hour", SystemHealthRules.Outbox(2, 0, TimeSpan.FromMinutes(15)), SystemHealthStatus.Degraded },
        { "work waiting two hours", SystemHealthRules.Outbox(2, 0, TimeSpan.FromHours(2)), SystemHealthStatus.Down },

        { "SMTP without a host", SystemHealthRules.Email(false, 0), SystemHealthStatus.NotConfigured },
        { "SMTP with no failures", SystemHealthRules.Email(true, 0), SystemHealthStatus.Ok },
        { "SMTP with a failure today", SystemHealthRules.Email(true, 1), SystemHealthStatus.Degraded },

        { "no AI provider", SystemHealthRules.Ai(false, 0, 1000, 0, 1000), SystemHealthStatus.NotConfigured },
        { "AI well under its ceiling", SystemHealthRules.Ai(true, 100, 1000, 10, 1000), SystemHealthStatus.Ok },
        { "AI tokens at 80 percent", SystemHealthRules.Ai(true, 800, 1000, 10, 1000), SystemHealthStatus.Degraded },
        { "AI cost at 90 percent", SystemHealthRules.Ai(true, 10, 1000, 900, 1000), SystemHealthStatus.Degraded },
        { "AI ceiling reached", SystemHealthRules.Ai(true, 1000, 1000, 0, 1000), SystemHealthStatus.Down },
        { "AI with no ceiling set", SystemHealthRules.Ai(true, 5000, 0, 5000, 0), SystemHealthStatus.Ok },

        { "a bot that answers", SystemHealthRules.Reachable(true, true, "bot service"), SystemHealthStatus.Ok },
        { "a bot that does not", SystemHealthRules.Reachable(true, false, "bot service"), SystemHealthStatus.Down },
        { "no bot address", SystemHealthRules.Reachable(false, null, "bot service"), SystemHealthStatus.NotConfigured },

        { "push configured", SystemHealthRules.Configured(true, "Push"), SystemHealthStatus.Ok },
        { "push not configured", SystemHealthRules.Configured(false, "Push"), SystemHealthStatus.NotConfigured },

        { "no prices worker", SystemHealthRules.Prices(false, Now, Now), SystemHealthStatus.NotConfigured },
        { "a worker that never succeeded", SystemHealthRules.Prices(true, null, Now), SystemHealthStatus.Degraded },
        { "a run yesterday", SystemHealthRules.Prices(true, Now.AddHours(-20), Now), SystemHealthStatus.Ok },
        { "a run three days ago", SystemHealthRules.Prices(true, Now.AddDays(-3), Now), SystemHealthStatus.Degraded },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachMeasurement_MapsToOneStatus(string _, HealthVerdict verdict, SystemHealthStatus expected) =>
        verdict.Status.Should().Be(expected);

    [Fact]
    public void NotConfigured_SaysWhatIsMissing_AndShowsNoNumber()
    {
        var verdicts = new[]
        {
            SystemHealthRules.Email(false, 7),
            SystemHealthRules.Ai(false, 900, 1000, 900, 1000),
            SystemHealthRules.Prices(false, Now, Now),
            SystemHealthRules.RoundTrip(false, null, null),
        };

        verdicts.Should().OnlyContain(v => v.Status == SystemHealthStatus.NotConfigured && !v.Detail.Any(char.IsDigit));
    }

    [Fact]
    public void TheDetailLine_CarriesTheNumbers()
    {
        SystemHealthRules.Outbox(2, 1, TimeSpan.FromMinutes(12)).Detail.Should().Be("2 pending, 1 failed, oldest 12 min");
        SystemHealthRules.Ai(true, 1200, 4000, 250_000, 1_000_000).Detail.Should().Be("1,200 of 4,000 tokens, $0.25 of $1.00 today");
        SystemHealthRules.Email(true, 1).Detail.Should().Be("1 failed in the last 24 hours");
    }
}
