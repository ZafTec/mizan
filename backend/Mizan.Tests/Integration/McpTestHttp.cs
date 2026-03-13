using System.Net.Http.Json;
using System.Net;
using System.Text;

namespace Mizan.Tests.Integration;

internal static class McpTestHttp
{
    public static async Task<HttpResponseMessage> PostMcpAsync(
        this HttpClient client,
        object request,
        string mcpPath = "/mcp",
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, mcpPath)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Accept.ParseAdd("application/json");
        message.Headers.Accept.ParseAdd("text/event-stream");

        var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var payload = await ReadNextJsonPayloadAsync(reader, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }

        return response;
    }

    private static async Task<string> ReadNextJsonPayloadAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new InvalidOperationException("MCP SSE stream ended before returning a JSON-RPC payload.");
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;
            var data = line["data:".Length..].Trim();
            if (data.StartsWith("{", StringComparison.Ordinal))
                return data;
        }
    }
}
