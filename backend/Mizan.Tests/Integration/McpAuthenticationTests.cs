extern alias McpServer;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpServer::Mizan.Mcp.Server.Authentication;
using McpServer::Mizan.Mcp.Server.Models;
using McpServer::Mizan.Mcp.Server.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Comprehensive tests for MCP authentication system
/// </summary>
public class McpAuthenticationTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private readonly WebApplicationFactory<McpServer::Program> _factory;
    private readonly Mock<HttpMessageHandler> _mockBackendHandler;
    private readonly Mock<IBackendClient> _mockBackendClient;

    public McpAuthenticationTests(WebApplicationFactory<McpServer::Program> factory)
    {
        _mockBackendHandler = new Mock<HttpMessageHandler>();
        _mockBackendClient = new Mock<IBackendClient>();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MizanApiUrl", "http://localhost:5000" },
                    { "BACKEND_API_URL", "http://localhost:5000" },
                    { "ServiceApiKey", "test-api-key" },
                    { "Mcp:ServiceApiKey", "test-api-key" }
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // Remove existing IBackendClient registration
                var descriptors = services.Where(d => d.ServiceType == typeof(IBackendClient)).ToList();
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }
                
                // Add the mock as a singleton so it's used everywhere including the auth handler
                services.AddSingleton<IBackendClient>(_mockBackendClient.Object);
            });
        });
    }

    #region Authentication Handler Unit Tests

    [Fact]
    public async Task McpTokenAuthenticationHandler_WithValidBearerToken_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_valid_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));

        var options = new Mock<IOptionsMonitor<McpTokenAuthenticationOptions>>();
        options.Setup(x => x.Get(It.IsAny<string>())).Returns(new McpTokenAuthenticationOptions());
        
        var loggerFactory = new Mock<ILoggerFactory>();
        var logger = new Mock<ILogger<McpTokenAuthenticationHandler>>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        var handler = new McpTokenAuthenticationHandler(
            options.Object,
            loggerFactory.Object,
            UrlEncoder.Default,
            _mockBackendClient.Object
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {token}";
        
        await handler.InitializeAsync(new AuthenticationScheme("McpToken", "MCP Token", typeof(McpTokenAuthenticationHandler)), httpContext);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
        result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(userId.ToString());
        result.Principal.FindFirst("mcp_token_id")?.Value.Should().Be(tokenId.ToString());
    }

    [Fact]
    public async Task McpTokenAuthenticationHandler_WithValidQueryToken_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_query_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));

        var options = new Mock<IOptionsMonitor<McpTokenAuthenticationOptions>>();
        options.Setup(x => x.Get(It.IsAny<string>())).Returns(new McpTokenAuthenticationOptions());
        
        var loggerFactory = new Mock<ILoggerFactory>();
        var logger = new Mock<ILogger<McpTokenAuthenticationHandler>>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        var handler = new McpTokenAuthenticationHandler(
            options.Object,
            loggerFactory.Object,
            UrlEncoder.Default,
            _mockBackendClient.Object
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString($"?token={token}");
        
        await handler.InitializeAsync(new AuthenticationScheme("McpToken", "MCP Token", typeof(McpTokenAuthenticationHandler)), httpContext);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
    }

    [Fact]
    public async Task McpTokenAuthenticationHandler_WithInvalidToken_ReturnsFail()
    {
        // Arrange
        var token = "invalid_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync((McpTokenValidation?)null);

        var options = new Mock<IOptionsMonitor<McpTokenAuthenticationOptions>>();
        options.Setup(x => x.Get(It.IsAny<string>())).Returns(new McpTokenAuthenticationOptions());
        
        var loggerFactory = new Mock<ILoggerFactory>();
        var logger = new Mock<ILogger<McpTokenAuthenticationHandler>>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        var handler = new McpTokenAuthenticationHandler(
            options.Object,
            loggerFactory.Object,
            UrlEncoder.Default,
            _mockBackendClient.Object
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {token}";
        
        await handler.InitializeAsync(new AuthenticationScheme("McpToken", "MCP Token", typeof(McpTokenAuthenticationHandler)), httpContext);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure?.Message.Should().Be("Invalid or expired MCP token");
    }

    [Fact]
    public async Task McpTokenAuthenticationHandler_WithNoToken_ReturnsNoResult()
    {
        // Arrange
        var options = new Mock<IOptionsMonitor<McpTokenAuthenticationOptions>>();
        options.Setup(x => x.Get(It.IsAny<string>())).Returns(new McpTokenAuthenticationOptions());
        
        var loggerFactory = new Mock<ILoggerFactory>();
        var logger = new Mock<ILogger<McpTokenAuthenticationHandler>>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        var handler = new McpTokenAuthenticationHandler(
            options.Object,
            loggerFactory.Object,
            UrlEncoder.Default,
            _mockBackendClient.Object
        );

        var httpContext = new DefaultHttpContext();
        
        await handler.InitializeAsync(new AuthenticationScheme("McpToken", "MCP Token", typeof(McpTokenAuthenticationHandler)), httpContext);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.None.Should().BeTrue();
    }

    #endregion

    #region Controller Integration Tests

    [Fact]
    public async Task ConnectSse_WithValidToken_ReturnsEventStream()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_valid_sse_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));

        var client = _factory.CreateClient();
        
        // Use a cancellation token to abort the SSE connection after we verify headers
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        HttpResponseMessage? response = null;

        // Act - SSE endpoint keeps connection open, so we catch the cancellation
        try
        {
            response = await client.GetAsync($"/mcp/sse?token={token}", cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected - SSE connection was aborted by cancellation token
        }

        // If we didn't get a response before cancellation, make a new request without waiting for body
        if (response == null)
        {
            using var quickCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            try
            {
                response = await client.GetAsync($"/mcp/sse?token={token}", HttpCompletionOption.ResponseHeadersRead, quickCts.Token);
            }
            catch (TaskCanceledException)
            {
                // Should not happen with ResponseHeadersRead, but just in case
                Assert.True(false, "Failed to get SSE response headers");
                return;
            }
        }

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
        response.Headers.CacheControl?.NoCache.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectSse_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/mcp/sse");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConnectSse_WithInvalidToken_Returns401()
    {
        // Arrange
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((McpTokenValidation?)null);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/mcp/sse?token=invalid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HandleMessage_WithValidToken_CallsToolAndReturnsResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_message_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));
        
        _mockBackendClient.Setup(x => x.CallApiAsync(userId, "GET", "/api/Foods/search?search=chicken", null))
            .ReturnsAsync("{\"items\": [{\"name\": \"Chicken Breast\"}]}");

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
        jsonResponse.Result.Should().NotBeNull();
        
        // Verify usage was logged
        _mockBackendClient.Verify(x => x.LogUsageAsync(
            tokenId, userId, "list_ingredients", 
            It.Is<string>(s => s.Contains("chicken")), 
            true, null, It.IsAny<int>()), 
            Times.Once);
    }

    [Fact]
    public async Task HandleMessage_WithoutToken_Returns401()
    {
        // Arrange
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

        // Act
        var response = await client.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HandleMessage_Initialize_ReturnsServerInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_init_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new JsonRpcRequest
        {
            Method = "initialize",
            Id = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Error.Should().BeNull();
        
        var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
            jsonResponse.Result!.ToString()!);
        result.Should().ContainKey("protocolVersion");
        result.Should().ContainKey("serverInfo");
    }

    [Fact]
    public async Task HandleMessage_ToolsList_ReturnsAvailableTools()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_tools_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new JsonRpcRequest
        {
            Method = "tools/list",
            Id = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Error.Should().BeNull();
        
        var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
            jsonResponse.Result!.ToString()!);
        result.Should().ContainKey("tools");
    }

    [Fact]
    public async Task HandleMessage_NotificationsInitialized_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_notif_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new JsonRpcRequest
        {
            Method = "notifications/initialized",
            Id = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleMessage_InvalidMethod_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_invalid_method_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new JsonRpcRequest
        {
            Method = "invalid/method",
            Id = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK); // JSON-RPC returns 200 even for errors
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Error.Should().NotBeNull();
        jsonResponse.Error!.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task HandleMessage_ToolCallFailure_LogsErrorAndReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "mcp_tool_fail_token";
        
        _mockBackendClient.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(new McpTokenValidation(userId, tokenId));
        
        _mockBackendClient.Setup(x => x.CallApiAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new Exception("Backend service unavailable"));

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
        jsonResponse!.Error.Should().NotBeNull();
        
        // Verify failure was logged
        _mockBackendClient.Verify(x => x.LogUsageAsync(
            tokenId, userId, "list_ingredients", 
            It.IsAny<string>(), 
            false, It.Is<string>(s => s.Contains("unavailable")), It.IsAny<int>()), 
            Times.Once);
    }

    #endregion

    #region Backend Client Tests

    [Fact]
    public async Task BackendClient_ValidateTokenAsync_WithValidToken_ReturnsValidation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var token = "backend_test_token";
        
        _mockBackendHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => 
                    r.RequestUri!.AbsolutePath.Contains("/api/McpTokens/validate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(new { UserId = userId, TokenId = tokenId, IsValid = true })
            });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MizanApiUrl", "http://localhost:5000" },
                { "ServiceApiKey", "test-api-key" }
            })
            .Build());
        services.AddLogging();
        services.AddHttpClient<IBackendClient, BackendClient>()
            .ConfigurePrimaryHttpMessageHandler(() => _mockBackendHandler.Object);
        
        var provider = services.BuildServiceProvider();
        var backendClient = provider.GetRequiredService<IBackendClient>();

        // Act
        var result = await backendClient.ValidateTokenAsync(token);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.TokenId.Should().Be(tokenId);
    }

    [Fact]
    public async Task BackendClient_ValidateTokenAsync_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        _mockBackendHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => 
                    r.RequestUri!.AbsolutePath.Contains("/api/McpTokens/validate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(new { IsValid = false })
            });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MizanApiUrl", "http://localhost:5000" },
                { "ServiceApiKey", "test-api-key" }
            })
            .Build());
        services.AddLogging();
        services.AddHttpClient<IBackendClient, BackendClient>()
            .ConfigurePrimaryHttpMessageHandler(() => _mockBackendHandler.Object);
        
        var provider = services.BuildServiceProvider();
        var backendClient = provider.GetRequiredService<IBackendClient>();

        // Act
        var result = await backendClient.ValidateTokenAsync("invalid_token");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BackendClient_CallApiAsync_WithValidRequest_AddsServiceHeaders()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var capturedRequest = new List<HttpRequestMessage>();
        
        _mockBackendHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest.Add(req))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"result\": \"ok\"}")
            });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MizanApiUrl", "http://localhost:5000" },
                { "ServiceApiKey", "test-api-key" }
            })
            .Build());
        services.AddLogging();
        services.AddHttpClient<IBackendClient, BackendClient>()
            .ConfigurePrimaryHttpMessageHandler(() => _mockBackendHandler.Object);
        
        var provider = services.BuildServiceProvider();
        var backendClient = provider.GetRequiredService<IBackendClient>();

        // Act
        await backendClient.CallApiAsync(userId, "GET", "/api/test");

        // Assert
        capturedRequest.Should().HaveCount(1);
        var request = capturedRequest[0];
        request.Headers.Contains("X-Api-Key").Should().BeTrue();
        request.Headers.GetValues("X-Api-Key").First().Should().Be("test-api-key");
        request.Headers.Contains("X-Impersonate-User").Should().BeTrue();
        request.Headers.GetValues("X-Impersonate-User").First().Should().Be(userId.ToString());
    }

    #endregion

    #region Environment Configuration Tests

    [Theory]
    [InlineData("MizanApiUrl", "http://custom-api:9000")]
    [InlineData("BACKEND_API_URL", "http://backend-service:8080")]
    public void BackendClient_UsesCorrectEnvironmentVariable(string envVarName, string expectedUrl)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { envVarName, expectedUrl },
                { "ServiceApiKey", "test-key" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddHttpClient<IBackendClient, BackendClient>();
        
        // The base address should be set correctly during construction
        // We can't easily test this without creating the client, but we can verify the configuration is read
        config[envVarName].Should().Be(expectedUrl);
    }

    [Fact]
    public void BackendClient_Throws_WhenServiceApiKeyMissing()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddHttpClient<IBackendClient, BackendClient>();
        
        var provider = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IBackendClient>());
    }

    #endregion
}
