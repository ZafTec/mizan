using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mizan.Prices.Tests;

/// <summary>
/// The worker reads only where the API is, its key and the run lock from the environment. The crawl settings
/// are runtime settings the API sends with each run (prices/README.md), so a new <c>Configure&lt;T&gt;(section)</c>
/// outside <c>Prices</c> fails here.
/// </summary>
public class ConfigurationAllowlistTests
{
    [Fact]
    public void TheWorker_BindsOnlyItsOwnSectionFromConfiguration()
    {
        List<ServiceDescriptor> registrations = [];
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => registrations = services.ToList()));
        _ = factory.Services;

        var bound = registrations
            .Where(d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IOptionsChangeTokenSource<>)
                && d.ImplementationInstance?.GetType() is { IsGenericType: true } type
                && type.GetGenericTypeDefinition() == typeof(ConfigurationChangeTokenSource<>)
                && d.ServiceType.GetGenericArguments()[0].Assembly == typeof(PricesOptions).Assembly)
            .Select(d => (d.ImplementationInstance!.GetType()
                .GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(d.ImplementationInstance) as IConfigurationSection)?.Path)
            .ToList();

        bound.Should().Equal("Prices");
        typeof(PricesOptions).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            ["ApiUrl", "ServiceApiKey", "Redis", "IsConfigured"],
            "the user agent, request delay and product cap are runtime settings now");
    }
}
