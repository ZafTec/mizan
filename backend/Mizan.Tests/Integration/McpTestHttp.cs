using System.Net.Http.Json;
using System.IO;
using System.Net;
using System.Text;

namespace Mizan.Tests.Integration;

internal static class McpTestHttp
{
    public static async Task<HttpResponseMessage> PostMcpAsync(this HttpClient client, object request, string ssePath = "/mcp/sse", CancellationToken cancellationToken = default)
    {
        using var sseResponse = await client.GetAsync(ssePath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        sseResponse.EnsureSuccessStatusCode();

        await using var stream = await sseResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var messagePath = await GetLegacyMessagePathAsync(reader, ssePath, cancellationToken);

        using var message = new HttpRequestMessage(HttpMethod.Post, messagePath)
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.Accept.ParseAdd("application/json");
        message.Headers.Accept.ParseAdd("text/event-stream");

        var response = await client.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return response;
        }

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            return response;
        }

        var payload = await ReadNextJsonPayloadAsync(reader, cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private static async Task<string> GetLegacyMessagePathAsync(StreamReader reader, string ssePath, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (string.IsNullOrEmpty(data))
            {
                continue;
            }

            if (Uri.TryCreate(data, UriKind.Absolute, out var absoluteUri)
                && absoluteUri.PathAndQuery.Contains("/message", StringComparison.OrdinalIgnoreCase))
            {
                return absoluteUri.PathAndQuery;
            }

            if (data.Contains("/message", StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }
        }

        throw new InvalidOperationException($"MCP SSE endpoint '{ssePath}' did not provide a legacy message path.");
    }

    private static async Task<string> ReadNextJsonPayloadAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                throw new InvalidOperationException("MCP SSE stream ended before returning a JSON-RPC payload.");
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data.StartsWith("{", StringComparison.Ordinal))
            {
                return data;
            }
        }
    }
}
