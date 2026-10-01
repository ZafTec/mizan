using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Mizan.Api.Middleware;

/// <summary>
/// Gives every error a controller returns the one shape clients parse: <c>{ errorCode, error }</c>.
///
/// Many handlers answer with a result object (<c>{ success: false, message }</c>) or a bare string, and the
/// web client reads those. The filter adds the two standard fields next to whatever is already there, and
/// turns a bare string into the standard body, so nothing a client relies on goes away and an app never has
/// to special-case an endpoint. The OAuth endpoints are exempt: RFC 6749 fixes their error format.
/// </summary>
public sealed class ErrorShapeFilter : IAlwaysRunResultFilter
{
    private readonly JsonSerializerOptions _json;

    public ErrorShapeFilter(IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> json) => _json = json.Value.JsonSerializerOptions;

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { StatusCode: >= 400 and < 500 } result) return;
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/oauth")
            || context.HttpContext.Request.Path.StartsWithSegments("/api/.well-known")) return;

        var code = CodeFor(result.StatusCode!.Value);

        if (result.Value is string text)
        {
            result.Value = new { errorCode = code, error = text };
            return;
        }

        if (result.Value is null || result.Value is ProblemDetails) return;

        JsonNode? node;
        try { node = JsonSerializer.SerializeToNode(result.Value, result.Value.GetType(), _json); }
        catch (NotSupportedException) { return; }

        if (node is not JsonObject body) return;

        var hasCode = body.Any(p => string.Equals(p.Key, "errorCode", StringComparison.OrdinalIgnoreCase));
        var hasError = body.Any(p => string.Equals(p.Key, "error", StringComparison.OrdinalIgnoreCase));
        if (hasCode && hasError) return;

        if (!hasCode) body["errorCode"] = code;
        if (!hasError)
        {
            var message = body.FirstOrDefault(p => string.Equals(p.Key, "message", StringComparison.OrdinalIgnoreCase)).Value?.GetValue<string>();
            body["error"] = string.IsNullOrWhiteSpace(message) ? DefaultText(result.StatusCode.Value) : message;
        }

        result.Value = body;
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    private static string CodeFor(int status) => status switch
    {
        400 => "bad_request",
        401 => "unauthorized",
        403 => "forbidden",
        404 => "not_found",
        409 => "conflict",
        410 => "gone",
        422 => "unprocessable",
        429 => "rate_limited",
        _ => "error",
    };

    private static string DefaultText(int status) => status switch
    {
        400 => "The request could not be completed.",
        401 => "Unauthorized",
        403 => "You do not have access to this.",
        404 => "Not found",
        409 => "That conflicts with the current state.",
        _ => "The request failed.",
    };
}
