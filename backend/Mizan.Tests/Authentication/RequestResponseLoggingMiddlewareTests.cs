using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mizan.Api.Middleware;
using Moq;
using Xunit;

namespace Mizan.Tests.Authentication;

public sealed class RequestResponseLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RedactsSensitiveQueryValues()
    {
        var messages = new List<string>();
        var logger = new Mock<ILogger<RequestResponseLoggingMiddleware>>();
        logger.Setup(value => value.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation => messages.Add(invocation.Arguments[2].ToString() ?? string.Empty)));
        var middleware = new RequestResponseLoggingMiddleware(_ => Task.CompletedTask, logger.Object);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?token=raw-secret&view=feed");

        await middleware.InvokeAsync(context);

        messages.Should().NotContain(message => message.Contains("raw-secret", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("%5BREDACTED%5D", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("view=feed", StringComparison.Ordinal));
    }
}
