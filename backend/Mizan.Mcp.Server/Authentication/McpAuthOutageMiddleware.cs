namespace Mizan.Mcp.Server.Authentication;

/// <summary>
/// When the API that validates tokens cannot be reached, the request is answered with 503 and a
/// Retry-After instead of the 401 challenge. A 401 would make every client throw its token away and
/// start sign-in again for an outage that has nothing to do with the token. Runs right after
/// authentication, before authorization gets to issue the challenge.
/// </summary>
public sealed class McpAuthOutageMiddleware
{
    public const string Item = "mcp.auth.outage";
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    private readonly RequestDelegate _next;

    public McpAuthOutageMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        if (!context.Items.ContainsKey(Item)) return _next(context);

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = ((int)RetryAfter.TotalSeconds).ToString();
        return context.Response.WriteAsJsonAsync(new
        {
            error = "service_unavailable",
            message = "Mizan could not verify the connection just now. Try again in a few seconds.",
        });
    }
}
