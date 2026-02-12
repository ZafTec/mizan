extern alias McpServer;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using McpServer::Mizan.Mcp.Server.Models;
using McpServer::Mizan.Mcp.Server.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace Mizan.Tests.Integration;

public class McpServerTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private readonly WebApplicationFactory<McpServer::Program> _factory;
    private readonly Mock<HttpMessageHandler> _mockBackendHandler;

    public McpServerTests(WebApplicationFactory<McpServer::Program> factory)
    {
        _mockBackendHandler = new Mock<HttpMessageHandler>();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
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
                services.AddHttpClient<IBackendClient, BackendClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => _mockBackendHandler.Object);
            });
        });
    }

    [Fact]
    public async Task CallTool_ReturnsUnauthorized_WhenTokenMissing()
    {
        var client = _factory.CreateClient();
        
        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                name = "list_ingredients",
                arguments = new { search = "chicken" }
            }),
            Id = 1
        };

        var response = await client.PostAsJsonAsync("/mcp/messages?sessionId=test", request);
        
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CallTool_ReturnsSuccess_WhenTokenValidAndBackendResponds()
    {
        // Arrange
        var token = "mcp_valid_token";
        var userId = Guid.NewGuid();

        // Mock ValidateToken endpoint
        _mockBackendHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsolutePath.Contains("/api/McpTokens/validate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(new { UserId = userId, IsValid = true })
            });

        // Mock Foods search endpoint
        _mockBackendHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => 
                    r.RequestUri!.AbsolutePath.Contains("/api/Foods/search") &&
                    r.Headers.Contains("X-Api-Key") &&
                    r.Headers.GetValues("X-Api-Key").First() == "test-api-key" &&
                    r.Headers.Contains("X-Impersonate-User") &&
                    r.Headers.GetValues("X-Impersonate-User").First() == userId.ToString()
                ),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"items\": [{\"name\": \"Chicken\"}]}")
            });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new JsonRpcRequest
        {
            Method = "tools/call",
            Params = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                name = "list_ingredients",
                arguments = new { search = "chicken" }
            }),
            Id = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Error.Should().BeNull();
    }
}
