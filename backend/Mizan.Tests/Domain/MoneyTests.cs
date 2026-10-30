using FluentAssertions;
using Mizan.Domain.Pricing;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class MoneyTests
{
    [Theory]
    [InlineData(1245.5, "ETB", "ETB 1,245.50")]
    [InlineData(40, "etb", "ETB 40.00")]
    [InlineData(0.005, "KES", "KES 0.01")]
    [InlineData(-12, "ETB", "-ETB 12.00")]
    [InlineData(300, null, "300.00")]
    [InlineData(300, " ", "300.00")]
    public void Format_PutsTheCodeFirst_AsTheWebDoes(double amount, string? currency, string expected) =>
        Money.Format((decimal)amount, currency).Should().Be(expected);
}
