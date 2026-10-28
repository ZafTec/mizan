using FluentAssertions;
using Mizan.Tests.Integration;
using Xunit;

namespace Mizan.Tests.Configuration;

/// <summary>
/// Settings an administrator can change live in <c>system_settings</c>; the environment holds only what a
/// process needs before it can reach the database or another service (README, "Configuration"). A new
/// <c>Configure&lt;T&gt;(section)</c> outside the list below fails here: make it a runtime setting in
/// <c>RuntimeSettingDefinitions</c>, or add it with its reason.
/// </summary>
[Collection("ApiIntegration")]
public class ConfigurationAllowlistTests
{
    /// <summary>The sections the API may bind from configuration, each with why it stays there.</summary>
    public static readonly IReadOnlyDictionary<string, string> Api = new Dictionary<string, string>
    {
        ["App"] = "The public URL and cookie domain: links, cookies, CORS and the OAuth issuer need them before any request.",
    };

    private readonly ApiTestFixture _fixture;

    public ConfigurationAllowlistTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheApi_BindsOnlyAllowlistedSectionsFromConfiguration()
    {
        _ = _fixture.Services; // builds the host, which records the registrations

        var ours = ConfigurationBindings.Sections(_fixture.Registrations)
            .Where(b => b.Options.Assembly.GetName().Name!.StartsWith("Mizan", StringComparison.Ordinal))
            .ToList();

        ours.Should().NotBeEmpty("the helper must see AppOptions at least");
        ours.Select(b => b.Path).Should().OnlyContain(path => Api.ContainsKey(path),
            "a section bound from configuration is read from the environment; make it a runtime setting instead");
    }
}
