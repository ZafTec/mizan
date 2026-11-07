using FluentAssertions;
using Mizan.Infrastructure.Ai;
using Xunit;

namespace Mizan.Tests.Infrastructure;

public class AiActionLabelTests
{
    [Theory]
    [InlineData("plan_meals",
        "Drafted \"14-day keto plan toward 73 kg\" (plan b120f485-30dc-4ca9-851c-e18cd3a1daea, Draft): 14 days, 42 entries.\nDay 0: BREAKFAST d0524919-0695-455f-b825-7b9046ea85fc Tilapia",
        "Drafted \"14-day keto plan toward 73 kg\"")]
    [InlineData("find_food",
        "b7268588-66a5-440a-b985-98a1b2aea63f — Chicken breast (raw), one serving 100 g; per 100g: 165.00 kcal\nb656dd34-bb65-4f14-95a9-4fff73571f5d — Garlic (raw)",
        "Find food")]
    [InlineData("get_reminders", "The user has no reminders.", "The user has no reminders.")]
    [InlineData("set_goal", "Goal created successfully", "Goal created successfully")]
    [InlineData("get_meal_plan",
        "No active plan covers that day. Drafts are not active until the person accepts them.",
        "No active plan covers that day.")]
    [InlineData("log_food", "", "Log food")]
    public void ShowsWhatWasDoneWithoutIds(string tool, string text, string expected) =>
        AiActionLabel.For(tool, text).Should().Be(expected);
}
