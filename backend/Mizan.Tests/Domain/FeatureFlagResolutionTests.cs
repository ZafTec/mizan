using FluentAssertions;
using Mizan.Domain.Features;
using Xunit;

namespace Mizan.Tests.PlatformRules;

/// <summary>A household's override, then the stored default, then the catalog default.</summary>
public class FeatureFlagResolutionTests
{
    public static TheoryData<string, bool, bool?, bool?, bool> Cases => new()
    {
        { "nothing stored: the catalog default", true, null, null, true },
        { "nothing stored, catalog off", false, null, null, false },
        { "a stored default replaces the catalog's", true, false, null, false },
        { "a stored default can turn a catalog-off flag on", false, true, null, true },
        { "an override wins over the stored default", true, false, true, true },
        { "an override off wins over a default on", true, true, false, false },
        { "an override wins with no stored default", true, null, false, false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Resolve_PrefersTheHouseholdThenTheStoredDefaultThenTheCatalog(
        string _, bool catalog, bool? stored, bool? householdOverride, bool expected) =>
        FeatureFlags.Resolve(catalog, stored, householdOverride).Should().Be(expected);

    [Theory]
    [InlineData("planning.generate", true)]
    [InlineData("prices", true)]
    [InlineData("telegram.bot", true)]
    [InlineData("ai.assistant", true)]
    [InlineData("social.feed", true)]
    [InlineData("Prices", false)]
    [InlineData("planning", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyCatalogKeysAreKnown_Exactly(string? key, bool known) =>
        FeatureFlags.IsKnown(key).Should().Be(known);
}
