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

        var method = SanitizeForLog(context.Request.Method);
        var path = SanitizeForLog(context.Request.Path.Value ?? string.Empty);
        var queryString = GetSafeQueryString(context.Request.Query);

        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
        using (LogContext.PushProperty("UserRole", context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "none"))
        {
            var stopwatch = Stopwatch.StartNew();

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

    private static string SanitizeForLog(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.ReplaceLineEndings(" ").Replace('\n', ' ').Replace('\r', ' ');
    }

    private static string GetSafeQueryString(IQueryCollection query)
    {
        if (query.Count == 0) return string.Empty;
        var values = query.SelectMany(parameter => parameter.Value.Select(value =>
            new KeyValuePair<string, string?>(
                parameter.Key,
                IsSensitiveQueryKey(parameter.Key) ? "[REDACTED]" : SanitizeForLog(value ?? string.Empty))));
        return QueryString.Create(values).Value ?? string.Empty;
    }

    private static bool IsSensitiveQueryKey(string key)
        => key.Equals("token", StringComparison.OrdinalIgnoreCase)
            || key.Equals("access_token", StringComparison.OrdinalIgnoreCase)
            || key.Equals("api_key", StringComparison.OrdinalIgnoreCase);
}
