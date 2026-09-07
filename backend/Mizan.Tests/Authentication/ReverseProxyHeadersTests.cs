using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Api.Middleware;
using Xunit;

namespace Mizan.Tests.Authentication;

public class ReverseProxyHeadersTests
{
    [Theory]
    [InlineData("172.18.0.2", "https|api.example.test|203.0.113.10")]
    [InlineData("198.51.100.5", "http|api.example.test|198.51.100.5")]
    public async Task OnlyConfiguredProxyCanForwardSchemeAndClientAddress(string peer, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:KnownProxy"] = "172.18.0.2",
        }).Build();
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services => services.Configure<ForwardedHeadersOptions>(options =>
                ReverseProxyHeaders.Configure(options, configuration)))
            .Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                    await next(context);
                });
                app.UseForwardedHeaders();
                app.Run(context => context.Response.WriteAsync(
                    $"{context.Request.Scheme}|{context.Request.Host}|{context.Connection.RemoteIpAddress}"));
            }));
        using var client = server.CreateClient();
        client.BaseAddress = new Uri("http://api.example.test");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "attacker.example");

        (await client.GetStringAsync("/")).Should().Be(expected);
    }

    [Fact]
    public void InvalidProxyConfigurationCannotEnableTrustForAllAddresses()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:KnownProxy"] = "*",
        }).Build();

        var configure = () => ReverseProxyHeaders.Configure(new ForwardedHeadersOptions(), configuration);

        configure.Should().Throw<InvalidOperationException>();
    }
}
