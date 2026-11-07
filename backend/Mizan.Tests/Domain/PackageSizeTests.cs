using FluentAssertions;
using Mizan.Domain.Pricing;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class PackageSizeTests
{
    public static TheoryData<string, decimal, string, string> Sizes => new()
    {
        { "Teff Magna 500gm", 500, "g", "Teff Magna" },
        { "Teff flour 1kg", 1, "kg", "Teff flour" },
        { "Whole Milk 1L", 1, "l", "Whole Milk" },
        { "Farm eggs 6pc", 6, "piece", "Farm eggs" },
        { "Eggs 30 pcs", 30, "piece", "Eggs" },
        { "Sunflower oil 1.5 Ltr", 1.5m, "l", "Sunflower oil" },
        { "Rice (500 gm)", 500, "g", "Rice" },
        { "Juice 250ml - orange", 250, "ml", "Juice - orange" },
        { "Water 6 x 500ml", 3000, "ml", "Water" },
        { "Lentils 2,5 kg", 2.5m, "kg", "Lentils" },
        { "1kg Sugar", 1, "kg", "Sugar" },
    };

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Reads_the_size_and_leaves_the_name(string product, decimal quantity, string unit, string name) =>
        PackageSize.Parse(product).Should().Be(new PackageSizeMatch(quantity, unit, name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Carrier bag")]
    [InlineData("Coca-Cola")]
    [InlineData("Size 0 nappies")]
    [InlineData("Model X200")]
    [InlineData("Gloves 2 pairs")]
    public void Says_nothing_without_a_size_it_knows(string? product) =>
        PackageSize.Parse(product).Should().BeNull();
}
