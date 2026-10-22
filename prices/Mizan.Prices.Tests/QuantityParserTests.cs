using Xunit;
using FluentAssertions;
using Mizan.Prices.Scraping;

namespace Mizan.Prices.Tests;

public class QuantityParserTests
{
    [Theory]
    [InlineData("Teff Flour 1kg", 1, "kg")]
    [InlineData("Dabur Basil Toothpaste 150g", 150, "g")]
    [InlineData("Enrich Fruity Rings 375g", 375, "g")]
    [InlineData("Shiro 500gm", 500, "g")]
    [InlineData("Milk 1l", 1, "l")]
    [InlineData("Coca Cola 330ml", 330, "ml")]
    [InlineData("Azzy Caring Hand Wash Orange 5l", 5, "l")]
    [InlineData("Sunflower Oil 5ltr", 5, "l")]
    [InlineData("Eggs 10pc", 10, "piece")]
    [InlineData("Eggs 30 pcs", 30, "piece")]
    [InlineData("Water 1.5L", 1.5, "l")]
    [InlineData("Berbere (500 g)", 500, "g")]
    public void TheTrailingSizeOfAName_BecomesQuantityAndUnit(string name, decimal quantity, string unit)
    {
        QuantityParser.Parse(name).Should().Be((quantity, unit));
    }

    [Theory]
    [InlineData("whole chicken")]
    [InlineData("Tikel Gomen")]
    [InlineData("Cherry")]
    [InlineData("Sassy Sanitary Pad Ultra Long")]
    [InlineData("Omo 2 in 1")]
    [InlineData("Route 66 Sauce")]
    [InlineData("")]
    [InlineData(null)]
    public void ANameWithoutASize_HasNoQuantity(string? name)
    {
        QuantityParser.Parse(name).Should().BeNull();
    }
}
