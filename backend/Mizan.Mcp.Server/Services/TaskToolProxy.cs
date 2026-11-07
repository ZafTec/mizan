using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Mizan.Contracts.Tools;
using Mizan.Mcp.Server.Authorization;
using ModelContextProtocol.Protocol;

namespace Mizan.Mcp.Server.Services;

/// <summary>
/// The task tools as an MCP connection meets them (<c>Mcp:Toolset</c> tasks). The API decides everything:
/// <c>GET /api/tools?surface=mcp</c> is the caller's manifest, already narrowed to their role and grant, and
/// <c>POST /api/tools/{name}/call</c> runs one call and checks the caller again. This class only turns those
/// answers into MCP shapes, so no rule lives here.
/// </summary>
public sealed class TaskToolProxy
{
    /// <summary>How long one connection's manifest is reused. A grant that is narrowed shows within this; a call is checked by the API anyway.</summary>
    public static readonly TimeSpan ManifestLifetime = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _http;
    private readonly McpLinks _links;

    public TaskToolProxy(IServiceScopeFactory scopes, IMemoryCache cache, IHttpContextAccessor http, McpLinks links)
    {
        _scopes = scopes;
        _cache = cache;
        _http = http;
        _links = links;
    }

    /// <summary>The caller's task tools as MCP tools: annotations from each tool's kind, closed-world, and the app page for those that have one.</summary>
    public async Task<IList<Tool>> ListAsync(CancellationToken cancellationToken) =>
        (await ManifestAsync(cancellationToken)).Tools.Select(ToTool).ToList();

    /// <summary>Whether the caller's manifest lists <paramref name="name"/>.</summary>
    public async Task<bool> ListsAsync(string name, CancellationToken cancellationToken) =>
        (await ManifestAsync(cancellationToken)).Tools.Any(tool => tool.Name == name);

    /// <summary>
    /// Whether the caller's cached manifest lists <paramref name="name"/>, or null when nothing is cached.
    /// For the task execution mode, which is chosen synchronously: it never waits on the API.
    /// </summary>
    public bool? ListsCached(string name) =>
        _cache.TryGetValue(CacheKey(), out ToolManifestDto? cached) && cached is not null
            ? cached.Tools.Any(tool => tool.Name == name)
            : null;

    /// <summary>Whether a tool in the caller's manifest renders the app page <paramref name="uri"/>: the page is open exactly when its tool is.</summary>
    public async Task<bool> ListsAppAsync(string uri, CancellationToken cancellationToken) =>
        (await ManifestAsync(cancellationToken)).Tools.Any(tool => string.Equals(tool.App, uri, StringComparison.OrdinalIgnoreCase));

    public async Task<CallToolResult> CallAsync(string name, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var backend = scope.ServiceProvider.GetRequiredService<IBackendApiClient>();
        var request = new ToolCallRequest(ToolSurfaces.Mcp, JsonSerializer.SerializeToElement(arguments ?? new Dictionary<string, JsonElement>()));
        var body = await backend.PostAsync($"/api/tools/{Uri.EscapeDataString(name)}/call", request, cancellationToken);
        var result = JsonSerializer.Deserialize<ToolCallResultDto>(body, Json)
            ?? throw new InvalidOperationException("The API answered the tool call with nothing.");
        return ToResult(result, _links);
    }

    private async Task<ToolManifestDto> ManifestAsync(CancellationToken cancellationToken)
    {
        var key = CacheKey();
        if (_cache.TryGetValue(key, out ToolManifestDto? cached) && cached is not null) return cached;

        using var scope = _scopes.CreateScope();
        var backend = scope.ServiceProvider.GetRequiredService<IBackendApiClient>();
        var body = await backend.GetAsync("/api/tools?surface=" + ToolSurfaces.Mcp, cancellationToken);
        var manifest = JsonSerializer.Deserialize<ToolManifestDto>(body, Json)
            ?? throw new InvalidOperationException("The API answered the tool manifest with nothing.");
        _cache.Set(key, manifest, ManifestLifetime);
        return manifest;
    }

    // Per connection: the grant and the person. An administrator's manifest differs from their own
    // ordinary connection's because the grant differs, and the API decides what each one gets.
    private string CacheKey()
    {
        var http = _http.HttpContext;
        return $"mcp:task-tools:{McpGrantAccess.GrantId(http)}:{McpGrantAccess.UserId(http)}";
    }

    public static Tool ToTool(ToolDescriptorDto descriptor) => new()
    {
        Name = descriptor.Name,
        Title = descriptor.Title,
        Description = descriptor.Description,
        InputSchema = descriptor.InputSchema,
        Annotations = new ToolAnnotations
        {
            Title = descriptor.Title,
            ReadOnlyHint = descriptor.Kind == ToolKinds.Read,
            DestructiveHint = descriptor.Kind == ToolKinds.Read ? null : descriptor.Kind == ToolKinds.Destructive,
            // Every tool talks only to Mizan, never to the open internet.
            OpenWorldHint = false,
        },
        // The MCP Apps link: a host that shows apps renders this page with the tool's result.
        Meta = descriptor.App is null ? null : new JsonObject { ["ui"] = new JsonObject { ["resourceUri"] = descriptor.App } },
    };

    /// <summary>
    /// The API's answer as an MCP result. <c>Text</c> is what the model reads; <c>Data</c> is the structured
    /// content an app page and a client read (an array arrives as <c>{ "items": [...] }</c>, as in the legacy
    /// tools). A previewed action carries its token and preview there too; the text already says how to confirm.
    /// </summary>
    public static CallToolResult ToResult(ToolCallResultDto result, McpLinks links)
    {
        var text = result.IsError && result.ErrorCode == "upgrade_required" ? links.UpgradeRequired(result.Text) : result.Text;

        JsonObject? structured = result.Data is { } data ? data.ValueKind switch
        {
            JsonValueKind.Object => JsonNode.Parse(data.GetRawText())!.AsObject(),
            JsonValueKind.Array => new JsonObject { ["items"] = JsonNode.Parse(data.GetRawText()) },
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => new JsonObject { ["value"] = JsonNode.Parse(data.GetRawText()) },
        } : null;

        if (result.Confirmation is { } confirmation)
        {
            structured ??= new JsonObject();
            structured["confirmation"] = new JsonObject
            {
                ["token"] = confirmation.Token,
                ["expiresAt"] = confirmation.ExpiresAt.ToString("O"),
                ["preview"] = JsonNode.Parse(confirmation.Preview.GetRawText()),
            };
        }

        if (result.IsError)
        {
            // The code and the missing permission let a client say where to change it without parsing the text.
            // What the error carries besides (the items a promotion needs weights for) stays alongside.
            var error = new JsonObject { ["errorCode"] = result.ErrorCode, ["needs"] = result.Needs };
            foreach (var (field, value) in structured ?? [])
            {
                if (!error.ContainsKey(field)) error[field] = value?.DeepClone();
            }
            structured = error;
        }

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = structured is null ? null : JsonSerializer.SerializeToElement(structured),
            IsError = result.IsError,
        };
    }
}
