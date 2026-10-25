using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>The next evening reminder: the plan day whose window it is for, when it is due, and when that window closes.</summary>
public readonly record struct EveningReminderDue(DateOnly Day, DateTimeOffset At, DateTimeOffset ClosesAt);

/// <summary>A plan a person follows, as the evening reminder reads it.</summary>
public sealed record FollowedPlan(Guid PlanId, DateOnly? Start, int DurationDays, TimeOnly? WindowEnd, decimal? TargetCalories);

/// <summary>
/// When the evening reminder is due. It goes out <see cref="Lead"/> before the
/// plan's <c>EatingWindowEnd</c> on each plan day, in the person's time zone, at
/// most once per local calendar day. A day is skipped when the plan does not
/// cover it, when its window has already closed, or when a reminder already
/// went out on the calendar day this one would be sent. A window that ends
/// before 00:30 is reminded about late on the evening before. A person who
/// follows several plans (their own and their households') is reminded about
/// each day from the plan that day is read from.
/// </summary>
public static class EveningReminderSchedule
{
    public static readonly TimeSpan Lead = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How far ahead a scan queues a reminder. Far enough that one missed scan does not miss a
    /// reminder, near enough that a plan changed during the day is read as it is in the evening.
    /// </summary>
    public static readonly TimeSpan Lookahead = TimeSpan.FromHours(2);

    /// <summary>How far a job may run from the time it was scheduled for and still count as that reminder.</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(1);

    /// <summary>The next reminder at or after <paramref name="now"/>, or null when there is none to send.</summary>
    public static EveningReminderDue? Next(
        TimeOnly? windowEnd,
        DateOnly? planStart,
        int durationDays,
        TimeZoneInfo zone,
        DateTimeOffset now,
        DateTimeOffset? lastSentAt)
    {
        if (windowEnd is null || planStart is null) return null;

        // Today's window, then tomorrow's: a window ending just after midnight
        // is reminded about tonight, and nothing later is ever due sooner.
        foreach (var day in Days(zone, now))
        {
            if (DueOn(day, windowEnd, planStart, durationDays, zone, now, lastSentAt) is { } due) return due;
        }

        return null;
    }

    /// <summary>
    /// The next reminder of a person who can follow several plans: their own and their
    /// households'. <paramref name="followed"/> is in the order the day's plan is picked
    /// (their own personal plan first, then their own household plan, then another member's,
    /// each newest first), and each day is reminded about from the first plan that covers it.
    /// A plan without a window still takes its days, so a person whose own plan is active is
    /// never reminded about a household plan's window.
    /// </summary>
    public static (FollowedPlan Plan, EveningReminderDue Due)? NextFor(
        IReadOnlyList<FollowedPlan> followed, TimeZoneInfo zone, DateTimeOffset now, DateTimeOffset? lastSentAt)
    {
        foreach (var day in Days(zone, now))
        {
            if (followed.FirstOrDefault(p => p.Start is { } start && PlanSchedule.Covers(start, p.DurationDays, day)) is not { } plan) continue;
            if (DueOn(day, plan.WindowEnd, plan.Start, plan.DurationDays, zone, now, lastSentAt) is { } due) return (plan, due);
        }

        return null;
    }

    /// <summary>
    /// Whether a job scheduled for <paramref name="day"/> at <paramref name="scheduledAt"/> should send now.
    /// It must still be the next reminder at the time it was scheduled for; a window that moved has
    /// its own job, and one that closed or was already reminded about has none.
    /// </summary>
    public static bool IsDue(EveningReminderDue? next, DateOnly day, DateTimeOffset scheduledAt, DateTimeOffset now) =>
        next is { } due
        && due.Day == day
        && (due.At - scheduledAt).Duration() < Tolerance
        && now >= due.At - Tolerance;

    private static DateOnly[] Days(TimeZoneInfo zone, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return [today, today.AddDays(1)];
    }

    /// <summary>The reminder for one plan day, or null when the plan does not cover it, its window closed, or one already went out on the calendar day it would be sent.</summary>
    private static EveningReminderDue? DueOn(
        DateOnly day, TimeOnly? windowEnd, DateOnly? planStart, int durationDays, TimeZoneInfo zone, DateTimeOffset now, DateTimeOffset? lastSentAt)
    {
        if (windowEnd is not { } end || planStart is not { } start || !PlanSchedule.Covers(start, durationDays, day)) return null;

        var closes = LocalInstant(day, end, zone);
        if (closes <= now) return null;

        var at = closes - Lead;
        if (lastSentAt is { } sent
            && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(sent, zone).DateTime) == DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime))
            return null;

        return new EveningReminderDue(day, at, closes);
    }

    /// <summary>
    /// A local wall-clock time as an instant. A time inside a spring-forward gap does not exist,
    /// so it moves to the same minute after the gap.
    /// </summary>
    private static DateTimeOffset LocalInstant(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
