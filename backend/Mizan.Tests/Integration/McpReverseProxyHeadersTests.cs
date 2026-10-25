extern alias McpServer;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using McpReverseProxyHeaders = McpServer::Mizan.Mcp.Server.ReverseProxyHeaders;

namespace Mizan.Tests.Integration;

public class McpReverseProxyHeadersTests
{
    [Theory]
    [InlineData("172.18.0.2", "https|mcp.example.test|203.0.113.10")]
    [InlineData("198.51.100.5", "http|mcp.example.test|198.51.100.5")]
    public async Task OnlyConfiguredProxyCanForwardSchemeAndClientAddress(string peer, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:KnownProxy"] = "172.18.0.2",
        }).Build();
        using var server = BuildServer(configuration, peer);
        using var client = ForwardingClient(server);

        (await client.GetStringAsync("/")).Should().Be(expected);
    }

    [Fact]
    public async Task WithoutConfigurationOnlyLoopbackIsTrusted()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var server = BuildServer(configuration, "198.51.100.5");
        using var client = ForwardingClient(server);

        (await client.GetStringAsync("/")).Should().Be("http|mcp.example.test|198.51.100.5");
    }

    [Fact]
    public void InvalidProxyConfigurationCannotEnableTrustForAllAddresses()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:KnownProxy"] = "*",
        }).Build();

        var configure = () => McpReverseProxyHeaders.Configure(new ForwardedHeadersOptions(), configuration);

        configure.Should().Throw<InvalidOperationException>();
    }

    private static TestServer BuildServer(IConfiguration configuration, string peer) =>
        new(new WebHostBuilder()
            .ConfigureServices(services => services.Configure<ForwardedHeadersOptions>(options =>
                McpReverseProxyHeaders.Configure(options, configuration)))
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

    private static HttpClient ForwardingClient(TestServer server)
    {
        var client = server.CreateClient();
        client.BaseAddress = new Uri("http://mcp.example.test");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "attacker.example");
        return client;
    }
}
