extern alias McpServer;
using FluentAssertions;
using McpServer::Mizan.Mcp.Server;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>The MCP service's links to the web app come from configuration, with one fallback, and never from a hard-coded host.</summary>
public sealed class McpLinksTests
{
    [Theory]
    [InlineData("https://mizan.example", "https://mizan.example/mcp", "https://mizan.example")]
    [InlineData("https://app.example/", null, "https://app.example")]
    [InlineData(null, "https://mizan.example/mcp", "https://mizan.example")]
    [InlineData(null, "https://mizan.example/MCP/", "https://mizan.example")]
    [InlineData(null, "http://localhost:5001", "http://localhost:5001")]
    [InlineData("", "", McpLinks.DefaultAppUrl)]
    [InlineData(null, null, McpLinks.DefaultAppUrl)]
    public void TheAppUrl_IsAppPublicUrl_OrMcpPublicUrlWithoutItsMcpPath(string? app, string? mcp, string expected) =>
        new McpLinks(app, mcp).AppUrl.Should().Be(expected);

    [Fact]
    public void TheMessages_CarryTheBillingPage()
    {
        var links = new McpLinks("https://app.example", null);

        links.Billing.Should().Be("https://app.example/billing");
        links.LimitReached(15).Should().StartWith("[MONTHLY LIMIT REACHED]").And.Contain("15").And.EndWith("https://app.example/billing.");
        links.UpgradeRequired("Pro only.").Should().StartWith("[UPGRADE REQUIRED] Pro only.").And.EndWith("https://app.example/billing");
    }
}
