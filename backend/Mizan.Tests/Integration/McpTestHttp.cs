using System.Net;
using System.Text;
using System.Text.Json;

namespace Mizan.Tests.Integration;

/// <summary>
/// HTTP helpers for the MCP streamable HTTP transport.
///
/// The server runs in stateful mode (Stateless = false), meaning:
///   1. The first request must be "initialize" — the server creates a session and
///      returns an Mcp-Session-Id response header.
///   2. Every subsequent request must carry that Mcp-Session-Id header.
///
/// PostMcpAsync handles this automatically: for any non-initialize method it
/// calls EstablishSessionAsync first, captures the session ID, then sends the
/// real request with the header attached.
/// </summary>
internal static class McpTestHttp
{
    public static async Task<HttpResponseMessage> PostMcpAsync(
        this HttpClient client,
        object request,
        string mcpPath = "/mcp",
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(request);
        var method = ReadMethod(json);

        string? sessionId = null;
        if (method != "initialize")
            sessionId = await EstablishSessionAsync(client, mcpPath, cancellationToken);

        using var message = BuildMessage(mcpPath, json, sessionId);
        var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return await UnwrapAsync(response, cancellationToken);
    }

    /// <summary>
    /// Sends an initialize request and returns the Mcp-Session-Id for use in
    /// subsequent requests. Returns null if the server is running in stateless mode.
    /// </summary>
    public static async Task<string?> EstablishSessionAsync(
        HttpClient client,
        string mcpPath = "/mcp",
        CancellationToken cancellationToken = default)
    {
        const string initJson = """{"jsonrpc":"2.0","id":0,"method":"initialize"}""";

        using var message = BuildMessage(mcpPath, initJson, sessionId: null);
        var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        // Drain the SSE body to free the connection before reading headers.
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            await ReadNextJsonPayloadAsync(reader, cancellationToken);
        }

        response.Headers.TryGetValues("Mcp-Session-Id", out var values);
        return values?.FirstOrDefault();
    }

    private static HttpRequestMessage BuildMessage(string mcpPath, string json, string? sessionId)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, mcpPath)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        msg.Headers.Accept.ParseAdd("application/json");
        msg.Headers.Accept.ParseAdd("text/event-stream");
        if (sessionId != null)
            msg.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        return msg;
    }

    private static async Task<HttpResponseMessage> UnwrapAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
            return response;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var payload = await ReadNextJsonPayloadAsync(reader, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private static string? ReadMethod(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("method", out var m) ? m.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ReadNextJsonPayloadAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new InvalidOperationException("MCP SSE stream ended before a JSON-RPC payload was received.");
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;
            var data = line["data:".Length..].Trim();
            if (data.StartsWith("{", StringComparison.Ordinal))
                return data;
        }
    }
}
