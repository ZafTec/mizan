extern alias telegram;

using FluentAssertions;
using Mizan.Contracts.Planning;
using telegram::Mizan.Telegram.Bot;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>The cost lines the /plan and /shop replies carry: present only when the API sent a cost.</summary>
public class TelegramPlanCostTextTests
{
    [Fact]
    public void ADayWithACost_SaysItInTheRegionsCurrency_AndADayWithoutOneSaysNothing()
    {
        PlanCostText.Day(new PlanDayViewDto { Currency = "ETB", Day = new PlanDayDto { Cost = 1245.5m } }).Should().Be("Cost: 1,245.50 ETB");
        PlanCostText.Day(new PlanDayViewDto { Currency = "ETB", Day = new PlanDayDto() }).Should().BeNull();
    }

    [Theory]
    [InlineData(340.5, "Observed", 0, "Total: 340.50 ETB")]
    [InlineData(340.5, "Estimated", 1, "Total: 340.50 ETB (estimated), 1 item without a price")]
    [InlineData(12d, "Reported", 3, "Total: 12.00 ETB, 3 items without a price")]
    [InlineData(null, null, 4, null)]
    public void AListTotal_NamesTheGaps(double? total, string? confidence, int unpriced, string? expected) =>
        PlanCostText.List(new ShoppingListDetailDto
        {
            Currency = "ETB",
            TotalCost = (decimal?)total,
            CostConfidence = confidence,
            UnpricedCount = unpriced,
        }).Should().Be(expected);
}
