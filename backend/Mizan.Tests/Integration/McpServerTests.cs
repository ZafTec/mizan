extern alias McpServer;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using McpServer::Mizan.Mcp.Server.Services;
using Moq;
using Xunit;

namespace Mizan.Tests.Integration;

public class McpServerTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private readonly WebApplicationFactory<McpServer::Program> _factory;
    private readonly Mock<IBackendApiClient> _mockBackendClient;

    public McpServerTests(WebApplicationFactory<McpServer::Program> factory)
    {
        _mockBackendClient = new Mock<IBackendApiClient>();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MizanApiUrl", "http://localhost:5000");
            builder.UseSetting("ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MizanApiUrl", "http://localhost:5000" },
                    { "ServiceApiKey", "test-api-key" }
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackendApiClient>();
                services.AddSingleton<IBackendApiClient>(_mockBackendClient.Object);
            });
        });
    }

    [Fact]
    public async Task CallTool_ReturnsError_WhenTokenMissing()
    {
        var client = _factory.CreateClient();
        
        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                name = "search_foods",
                arguments = new { search = "chicken" }
            }),
            Id = 1
        };

        var response = await client.PostMcpAsync(request);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Result.Should().NotBeNull();
        jsonResponse.Error.Should().BeNull();

        var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonResponse.Result!.ToString()!);
        result.GetProperty("isError").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CallTool_ReturnsSuccess_WhenTokenValidAndBackendResponds()
    {
        // Arrange
        var token = "mcp_valid_token";
        var userId = Guid.NewGuid();

        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenValidation(userId, Guid.NewGuid()));

        _mockBackendClient.Setup(x => x.GetAsync(
                It.Is<string>(s => s.Contains("/api/Foods/search") && s.Contains("searchTerm=chicken")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"items\": [{\"name\": \"Chicken\"}]}");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                name = "search_foods",
                arguments = new { search = "chicken" }
            }),
            Id = 1
        };

        // Act
        var response = await client.PostMcpAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Error.Should().BeNull();
    }
}
