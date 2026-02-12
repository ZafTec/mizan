using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Api.Authentication;
using Mizan.Application.Interfaces;
using Moq;
using Xunit;

namespace Mizan.Tests.Authentication;

public class ApiKeyAuthenticationHandlerTests
{
    private readonly Mock<IOptionsMonitor<ApiKeyAuthenticationSchemeOptions>> _options;
    private readonly Mock<ILoggerFactory> _loggerFactory;
    private readonly Mock<UrlEncoder> _encoder;
    private readonly Mock<IUserStatusService> _userStatusService;
    private readonly ApiKeyAuthenticationHandler _handler;

    public ApiKeyAuthenticationHandlerTests()
    {
        _options = new Mock<IOptionsMonitor<ApiKeyAuthenticationSchemeOptions>>();
        _options.Setup(x => x.Get(It.IsAny<string>())).Returns(new ApiKeyAuthenticationSchemeOptions
        {
            ApiKey = "test-api-key"
        });

        _loggerFactory = new Mock<ILoggerFactory>();
        _loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        
        _encoder = new Mock<UrlEncoder>();
        _userStatusService = new Mock<IUserStatusService>();

        _handler = new ApiKeyAuthenticationHandler(
            _options.Object,
            _loggerFactory.Object,
            _encoder.Object,
            _userStatusService.Object);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsSuccess_WhenApiKeyIsValid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "test-api-key";
        await _handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("service", result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsFail_WhenApiKeyIsInvalid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "wrong-key";
        await _handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsSuccessWithImpersonation_WhenUserValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "test-api-key";
        context.Request.Headers["X-Impersonate-User"] = userId.ToString();

        _userStatusService.Setup(x => x.GetStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccessStatus(true, true, false));

        await _handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(userId.ToString(), result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("service_impersonation", result.Principal?.FindFirst("type")?.Value);
    }
}
