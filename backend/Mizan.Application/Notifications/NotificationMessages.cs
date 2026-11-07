using System.Globalization;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Entities;
using Mizan.Domain.Goals;
using Mizan.Domain.Notifications;

namespace Mizan.Application.Notifications;

/// <summary>
/// The content of each structured notification, built once here and carried to every channel
/// (docs/ARCHITECTURE.md#notifications). Images are absolute URLs: a shipped badge is turned into one
/// by the caller through <c>IAppUrls.Asset</c>, because Telegram fetches it itself.
/// </summary>
public static class NotificationMessages
{
    public const string AchievementsPath = "/achievements";

    public static NotificationMessage Achievement(Achievement achievement, string? imageUrl)
    {
        var facts = new List<NotificationFact> { new("Points", $"+{achievement.Points.ToString(CultureInfo.InvariantCulture)}") };
        if (!string.IsNullOrWhiteSpace(achievement.Category)) facts.Add(new("Category", Capitalise(achievement.Category)));

        return new NotificationMessage(
            NotificationKinds.Achievements,
            $"Achievement unlocked: {achievement.Name}",
            achievement.Description,
            imageUrl,
            [new NotificationAction("See achievements", AchievementsPath)],
            facts);
    }

    public const string GoalPath = "/goal";

    /// <summary>A body target of the person's goal reached by a measurement, with the target and the logged value.</summary>
    public static NotificationMessage GoalReached(GoalMetric metric, string target, string logged)
    {
        var (title, what) = metric switch
        {
            GoalMetric.Weight => ("You reached your target weight", "target weight"),
            GoalMetric.BodyFat => ("You reached your body fat target", "body fat target"),
            _ => ("You reached your muscle mass target", "muscle mass target"),
        };
        return new NotificationMessage(
            NotificationKinds.Achievements,
            title,
            $"You logged {logged}, which meets your {what} of {target}.",
            Actions: [new NotificationAction("Open goal", GoalPath)],
            Facts: [new NotificationFact("Target", target), new NotificationFact("Logged", logged)]);
    }

    public static NotificationMessage StreakMilestone(int days, string streakType, string? imageUrl) =>
        new(
            NotificationKinds.Achievements,
            $"{days}-day {streakType} streak",
            "Keep the momentum going.",
            imageUrl,
            [new NotificationAction("See achievements", AchievementsPath)],
            [new NotificationFact("Streak", $"{days} days")]);

    /// <summary>The shipped badge for a streak milestone, as a site-relative path.</summary>
    public static string StreakBadge(int days) => days switch
    {
        >= 100 => "/badges/512/achievement-gold-badge.png",
        >= 30 => "/badges/512/achievement-monthly-master.png",
        _ => "/badges/512/achievement-one-week-warrior.png",
    };

    /// <summary>
    /// A shipped badge in the size Telegram shows well. The list uses the 128px file; the 512px file
    /// has the same name. Uploaded icons pass through unchanged.
    /// </summary>
    public static string? LargeBadge(string? iconUrl) =>
        iconUrl is not null && iconUrl.StartsWith("/badges/128/", StringComparison.Ordinal)
            ? "/badges/512/" + iconUrl["/badges/128/".Length..]
            : iconUrl;

    public static NotificationMessage PlanDrafted(string coachName, MealPlan plan) =>
        new(
            NotificationKinds.Planning,
            $"{coachName} drafted a meal plan for you",
            string.IsNullOrWhiteSpace(plan.Name) ? "Open it to accept or decline." : $"\"{plan.Name}\" is waiting for you to accept or decline.",
            Actions: [new NotificationAction("Open plan", $"/meal-plan/{plan.Id}")],
            Facts: plan.StartDate is { } start
                ? [new NotificationFact("Starts", start.ToString("ddd d MMM", CultureInfo.InvariantCulture)), new NotificationFact("Days", plan.DurationDays.ToString(CultureInfo.InvariantCulture))]
                : null);

    public static NotificationMessage EveningReminder(string title, string text, TimeOnly windowEnd, decimal calories, decimal? target, string linkUrl)
    {
        var eaten = Math.Round(calories, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
        var today = target is { } goal
            ? $"{eaten} of {Math.Round(goal, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture)} kcal"
            : $"{eaten} kcal";
        return new NotificationMessage(
            NotificationKinds.Reminders,
            title,
            text,
            Actions: [new NotificationAction("Open plan", linkUrl)],
            Facts: [new NotificationFact("Window closes", windowEnd.ToString("HH:mm", CultureInfo.InvariantCulture)), new NotificationFact("Today", today)]);
    }

    /// <summary>The message a plain notification becomes: its kind from its type, and nothing but the words.</summary>
    public static NotificationMessage Plain(string type, string title, string? body) =>
        new(NotificationKinds.ForType(type), title, body);

    private static string Capitalise(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
