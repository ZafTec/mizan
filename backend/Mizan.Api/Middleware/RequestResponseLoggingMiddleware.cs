using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;

namespace Mizan.Api.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User?.FindFirst("sub")?.Value;

        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
        using (LogContext.PushProperty("UserRole", context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "none"))
        {
            var stopwatch = Stopwatch.StartNew();
            var method = context.Request.Method;
            var path = context.Request.Path;
            var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;

            _logger.LogInformation(
                "HTTP {Method} {Path}{QueryString} - Request started",
                method,
                path,
                queryString);

            try
            {
                await _next(context);

                stopwatch.Stop();

                var statusCode = context.Response.StatusCode;
                var logLevel = statusCode >= 500 ? LogLevel.Error
                    : statusCode >= 400 ? LogLevel.Warning
                    : LogLevel.Information;

                _logger.Log(
                    logLevel,
                    "HTTP {Method} {Path}{QueryString} - Responded {StatusCode} in {ElapsedMs}ms",
                    method,
                    path,
                    queryString,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "HTTP {Method} {Path}{QueryString} - Unhandled exception after {ElapsedMs}ms",
                    method,
                    path,
                    queryString,
                    stopwatch.ElapsedMilliseconds);

                throw;
            }
        }
    }
}
