using FluentAssertions;
using Mizan.Domain.Streaks;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The rule that three separate readers used to disagree about. Every case
/// here is one that was wrong in production.
/// </summary>
public class StreakClockTests
{
    private const string AddisAbaba = "Africa/Addis_Ababa"; // UTC+3, no DST
    private const string NewYork = "America/New_York";      // UTC-5/-4, DST

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso).ToUniversalTime();

    public class LocalDays
    {
        /// <summary>
        /// The bug this whole file exists for: a 01:00 local snack in Addis is
        /// 22:00 UTC the previous day. On UTC days it lands on yesterday, so a
        /// nightly logger's streak never advances.
        /// </summary>
        [Fact]
        public void ALateNightLogCountsAsTodayNotYesterday()
        {
            var utcNow = Utc("2026-03-10T22:15:00Z"); // 01:15 on the 11th in Addis

            StreakClock.Today(AddisAbaba, utcNow).Should().Be(new DateOnly(2026, 3, 11));
            StreakClock.Today(StreakClock.DefaultTimeZone, utcNow).Should().Be(new DateOnly(2026, 3, 10));
        }

        [Fact]
        public void AnEarlyMorningLogInTheWestCountsAsYesterday()
        {
            var utcNow = Utc("2026-03-11T02:30:00Z"); // 21:30 on the 10th in New York

            StreakClock.Today(NewYork, utcNow).Should().Be(new DateOnly(2026, 3, 10));
        }

        [Fact]
        public void AnUnknownZoneFallsBackToUtcRatherThanThrowing()
        {
            // A restored database on a host with different tzdata must not take
            // the logging path down.
            var utcNow = Utc("2026-03-10T22:15:00Z");

            StreakClock.Today("Mars/Olympus_Mons", utcNow).Should().Be(new DateOnly(2026, 3, 10));
            StreakClock.IsKnownZone("Mars/Olympus_Mons").Should().BeFalse();
            StreakClock.IsKnownZone(AddisAbaba).Should().BeTrue();
        }

        [Fact]
        public void ResetIsTheNextLocalMidnight()
        {
            var utcNow = Utc("2026-03-10T12:00:00Z"); // 15:00 in Addis

            var resets = StreakClock.ResetsAt(AddisAbaba, utcNow);

            // Midnight on the 11th in Addis is 21:00 UTC on the 10th.
            resets.ToUniversalTime().Should().Be(Utc("2026-03-10T21:00:00Z"));
            (resets - utcNow).Should().Be(TimeSpan.FromHours(9));
        }

        /// <summary>
        /// A day that starts before a DST jump and ends after it is still one
        /// day. Computing the offset at the boundary rather than at "now" is
        /// what makes that true.
        /// </summary>
        [Fact]
        public void ADayThatCrossesADstChangeStillEndsAtMidnight()
        {
            // US DST began 2026-03-08. Take the evening before.
            var utcNow = Utc("2026-03-07T20:00:00Z"); // 15:00 EST on the 7th

            var resets = StreakClock.ResetsAt(NewYork, utcNow);
            var local = TimeZoneInfo.ConvertTime(resets, StreakClock.Zone(NewYork));

            local.TimeOfDay.Should().Be(TimeSpan.Zero);
            local.Date.Should().Be(new DateTime(2026, 3, 8));
        }
    }

    public class Decay
    {
        private static StreakState Evaluate(int count, DateOnly? last, int freezes = 0) =>
            StreakClock.Evaluate(count, 30, last, freezes, AddisAbaba, Utc("2026-03-10T09:00:00Z"));

        private static readonly DateOnly Today = new(2026, 3, 10);

        [Fact]
        public void LoggedTodayIsAliveAndActive()
        {
            var state = Evaluate(5, Today);

            state.CurrentCount.Should().Be(5);
            state.IsActiveToday.Should().BeTrue();
            state.AtRisk.Should().BeFalse();
        }

        [Fact]
        public void LoggedYesterdayIsAliveAndAtRisk()
        {
            var state = Evaluate(5, Today.AddDays(-1));

            state.CurrentCount.Should().Be(5);
            state.IsActiveToday.Should().BeFalse();
            state.AtRisk.Should().BeTrue();
        }

        [Fact]
        public void OneMissedDayWithoutAFreezeIsDead()
        {
            Evaluate(5, Today.AddDays(-2), freezes: 0).CurrentCount.Should().Be(0);
        }

        [Fact]
        public void OneMissedDayWithAFreezeSurvives()
        {
            Evaluate(5, Today.AddDays(-2), freezes: 1).CurrentCount.Should().Be(5);
        }

        [Fact]
        public void TwoMissedDaysAreDeadEvenWithFreezes()
        {
            Evaluate(5, Today.AddDays(-3), freezes: 2).CurrentCount.Should().Be(0);
        }

        [Fact]
        public void NeverLoggedIsZeroRatherThanWhateverTheRowSays()
        {
            Evaluate(9, null).CurrentCount.Should().Be(0);
        }

        [Fact]
        public void TheLongestCountSurvivesTheStreakDying()
        {
            var state = Evaluate(5, Today.AddDays(-10));

            state.CurrentCount.Should().Be(0);
            state.LongestCount.Should().Be(30);
        }

        /// <summary>
        /// Flying east can put the last activity date in the future. That is
        /// not a broken streak, it is a user on a plane.
        /// </summary>
        [Fact]
        public void ALastActivityInTheFutureDoesNotKillTheStreak()
        {
            var state = Evaluate(5, Today.AddDays(1));

            state.CurrentCount.Should().Be(5);
            state.IsActiveToday.Should().BeTrue();
        }
    }

    public class Extension
    {
        private static readonly DateOnly Today = new(2026, 3, 10);

        [Fact]
        public void AConsecutiveDayExtends()
        {
            var t = StreakClock.Extend(4, 9, Today.AddDays(-1), 0, Today);

            t.CurrentCount.Should().Be(5);
            t.Extended.Should().BeTrue();
            t.IsNewRecord.Should().BeFalse();
        }

        [Fact]
        public void ASecondLogOnTheSameDayChangesNothing()
        {
            var t = StreakClock.Extend(5, 9, Today, 1, Today);

            t.CurrentCount.Should().Be(5);
            t.Extended.Should().BeFalse();
            t.FreezesAvailable.Should().Be(1);
        }

        [Fact]
        public void AGapRestartsAtOne()
        {
            StreakClock.Extend(20, 20, Today.AddDays(-5), 0, Today).CurrentCount.Should().Be(1);
        }

        [Fact]
        public void AFreezeCoversASingleMissedDayAndIsSpent()
        {
            var t = StreakClock.Extend(5, 9, Today.AddDays(-2), 1, Today);

            t.CurrentCount.Should().Be(6);
            t.FreezeConsumed.Should().BeTrue();
            t.FreezesAvailable.Should().Be(0);
        }

        [Fact]
        public void EverySeventhDayEarnsAFreezeCappedAtTwo()
        {
            StreakClock.Extend(6, 6, Today.AddDays(-1), 0, Today).FreezesAvailable.Should().Be(1);
            StreakClock.Extend(13, 13, Today.AddDays(-1), 1, Today).FreezesAvailable.Should().Be(2);
            StreakClock.Extend(20, 20, Today.AddDays(-1), 2, Today).FreezesAvailable.Should().Be(2);
        }

        [Fact]
        public void ANewRecordRaisesTheLongestCount()
        {
            var t = StreakClock.Extend(9, 9, Today.AddDays(-1), 0, Today);

            t.CurrentCount.Should().Be(10);
            t.LongestCount.Should().Be(10);
            t.IsNewRecord.Should().BeTrue();
        }

        [Fact]
        public void TheFirstEverLogStartsAtOne()
        {
            var t = StreakClock.Extend(0, 0, null, 0, Today);

            t.CurrentCount.Should().Be(1);
            t.IsNewRecord.Should().BeTrue();
        }
    }
}
