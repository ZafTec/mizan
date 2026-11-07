extern alias McpServer;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using McpServer::Mizan.Mcp.Server.Services;
using Mizan.Tests.Integration;

namespace Mizan.Tests.Eval;

/// <summary>What one tool call did, as the model's client saw it.</summary>
public sealed record McpCallOutcome(string Text, bool IsError);

/// <summary>
/// The MCP service in process, serving one toolset, with its API calls going to the in-process API.
/// Both arms of the evaluation reach their tools through it the same way: tools/list, then tools/call.
/// </summary>
public sealed class EvalMcpArm : IAsyncDisposable
{
    private readonly WebApplicationFactory<McpServer::Program> _factory;

    public EvalMcpArm(string toolset, ApiTestFixture api)
    {
        Toolset = toolset;
        _factory = new WebApplicationFactory<McpServer::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MizanApiUrl", "http://localhost:5000");
            builder.UseSetting("ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");
            builder.UseSetting("Mcp:Toolset", toolset);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MizanApiUrl"] = "http://localhost:5000",
                ["ServiceApiKey"] = "test-api-key",
                ["Mcp:Toolset"] = toolset,
                ["Serilog:MinimumLevel:Default"] = "Warning",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackendApiClient>();
                services.AddScoped<IBackendApiClient>(sp =>
                {
                    var client = new HttpClient(api.Server.CreateHandler()) { BaseAddress = new Uri("http://localhost:5000"), Timeout = TimeSpan.FromMinutes(3) };
                    client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
                    return new BackendApiClient(client, sp.GetRequiredService<IHttpContextAccessor>(), sp.GetRequiredService<ILogger<BackendApiClient>>());
                });
            });
        });
    }

    public string Toolset { get; }

    public HttpClient Connect(string token)
    {
        var client = _factory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(3);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>The server's instructions, as a client that still sends initialize reads them when it connects.</summary>
    public static async Task<string> InstructionsAsync(HttpClient client)
    {
        var json = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 0,
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = "2025-06-18",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "mizan-eval", ["version"] = "1" },
            },
        }.ToJsonString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return (await ReadAsync(request, client))["instructions"]?.GetValue<string>() ?? string.Empty;
    }

    /// <summary>The tools the connection is offered, as OpenAI function tools.</summary>
    public static async Task<JsonArray> ToolsAsync(HttpClient client)
    {
        var result = await SendAsync(client, "tools/list", new { });
        var tools = new JsonArray();
        foreach (var tool in result["tools"]!.AsArray().OfType<JsonObject>())
        {
            var schema = tool["inputSchema"]?.DeepClone() as JsonObject ?? new JsonObject { ["type"] = "object" };
            schema.Remove("$schema");
            schema["properties"] ??= new JsonObject();
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool["name"]!.GetValue<string>(),
                    ["description"] = tool["description"]?.GetValue<string>() ?? string.Empty,
                    ["parameters"] = schema,
                },
            });
        }
        return tools;
    }

    public static async Task<McpCallOutcome> CallAsync(HttpClient client, string name, JsonObject arguments)
    {
        JsonObject result;
        try
        {
            result = await SendAsync(client, "tools/call", new { name, arguments });
        }
        catch (McpRpcException ex)
        {
            return new McpCallOutcome(ex.Message, true);
        }

        var text = string.Join("\n", (result["content"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(block => block["type"]?.GetValue<string>() == "text")
            .Select(block => block["text"]?.GetValue<string>() ?? string.Empty));
        return new McpCallOutcome(text, result["isError"]?.GetValue<bool>() == true);
    }

    private static async Task<JsonObject> SendAsync(HttpClient client, string method, object @params)
    {
        var node = JsonSerializer.SerializeToNode(@params)!.AsObject();
        node["_meta"] = JsonSerializer.SerializeToNode(new Dictionary<string, object>
        {
            ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28",
            ["io.modelcontextprotocol/clientInfo"] = new { name = "mizan-eval", version = "1" },
            ["io.modelcontextprotocol/clientCapabilities"] = new { },
        });
        var json = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = node }.ToJsonString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2026-07-28");
        request.Headers.TryAddWithoutValidation("Mcp-Method", method);
        if (node["name"] is JsonValue named) request.Headers.TryAddWithoutValidation("Mcp-Name", named.GetValue<string>());

        return await ReadAsync(request, client);
    }

    private static async Task<JsonObject> ReadAsync(HttpRequestMessage request, HttpClient client)
    {
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        request.Dispose();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            text = text.Split('\n').Where(l => l.StartsWith("data:", StringComparison.Ordinal) && l.Contains('{'))
                .Select(l => l["data:".Length..].Trim()).LastOrDefault() ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new McpRpcException($"The MCP service answered {(int)response.StatusCode} with no body.");
        }

        var root = JsonNode.Parse(text)!.AsObject();
        if (root["error"] is JsonObject error)
        {
            throw new McpRpcException(error["message"]?.GetValue<string>() ?? error.ToJsonString());
        }
        return root["result"]!.AsObject();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}

public sealed class McpRpcException(string message) : Exception(message);
