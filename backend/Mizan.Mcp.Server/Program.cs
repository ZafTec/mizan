using Microsoft.AspNetCore.HttpOverrides;
using Mizan.Contracts.Mcp;
using Mizan.Mcp.Server;
using Mizan.Mcp.Server.Apps;
using Mizan.Mcp.Server.Authentication;
using Mizan.Mcp.Server.Authorization;
using Mizan.Mcp.Server.Prompts;
using Mizan.Mcp.Server.Resources;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using Mizan.Mcp.Server.Services;
using Mizan.Mcp.Server.Tools;
using Mizan.Mcp.Server.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using ModelContextProtocol.Protocol;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Diagnostics.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = McpLoggingConfiguration.CreateLogger(builder.Configuration);
builder.Host.UseSerilog();

// Auth: OAuth access tokens, validated by the API. A request with no token gets a 401
// that says where to sign in (RFC 9728), which is how MCP clients start the OAuth flow.
var publicUrl = (builder.Configuration["Mcp:PublicUrl"] ?? "http://localhost:5001/mcp").TrimEnd('/');
var authorizationServer = (builder.Configuration["Mcp:AuthorizationServer"] ?? "http://localhost:5000/api").TrimEnd('/');
// The web app's address, for the links in limit and upgrade messages.
var links = new McpLinks(builder.Configuration);
builder.Services.AddSingleton(links);
// Which tools this deployment serves: the legacy endpoint-shaped ones (the default) or the task tools,
// whose list and calls the API answers (docs/MCP.md#toolsets).
var toolset = McpToolsets.Parse(builder.Configuration[McpToolsets.ConfigKey]);
builder.Services.AddSingleton<TaskToolProxy>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = McpOAuthAuthenticationOptions.Scheme;
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddScheme<McpOAuthAuthenticationOptions, McpOAuthAuthenticationHandler>(McpOAuthAuthenticationOptions.Scheme, _ => { })
    .AddMcp(options =>
    {
        // Served under /mcp, which production already routes here, so no new proxy route is needed.
        options.ResourceMetadataUri = new Uri(publicUrl + "/.well-known/oauth-protected-resource");
        options.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = publicUrl,
            AuthorizationServers = { authorizationServer },
            ScopesSupported = McpScopes.All.Where(scope => !McpScopes.IsAdminScope(scope)).ToList(),
            ResourceName = "Mizan",
            BearerMethodsSupported = { "header" },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

builder.Services.AddHttpContextAccessor();

// Backend API client
var backendUrl = builder.Configuration["BACKEND_API_URL"]
                 ?? builder.Configuration["MizanApiUrl"]
                 ?? "http://mizan-backend:8080";

var serviceApiKey = builder.Configuration["Mcp:ServiceApiKey"]
    ?? builder.Configuration["ServiceApiKey"]
    ?? throw new InvalidOperationException("ServiceApiKey not configured");
var adminServiceApiKey = builder.Configuration["Mcp:AdminServiceApiKey"]
    ?? builder.Configuration["AdminServiceApiKey"]
    ?? throw new InvalidOperationException("AdminServiceApiKey not configured");
if (string.Equals(serviceApiKey, adminServiceApiKey, StringComparison.Ordinal))
{
    throw new InvalidOperationException("MCP service and admin API keys must be different");
}

builder.Services.AddHttpClient<IBackendApiClient, BackendApiClient>(client =>
{
    client.BaseAddress = new Uri(backendUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
});

// ============================================================================
// OpenTelemetry Configuration
// ============================================================================
var serviceName = "Mizan.Mcp";
var serviceVersion = "2.0.0";

var tracingEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];
var lokiEndpoint = builder.Configuration["LOKI_OTLP_ENDPOINT"];

var mcpActivitySource = new ActivitySource("Mizan.Mcp.Tools");
var mcpMeter = new Meter("Mizan.Mcp", "2.0.0");
var toolCallCounter = mcpMeter.CreateCounter<int>("mcp.tool_calls.count");
var toolCallDuration = mcpMeter.CreateHistogram<double>("mcp.tool_calls.duration", unit: "ms");

var otel = builder.Services.AddOpenTelemetry();

otel.ConfigureResource(resource => resource
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
);

otel.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddMeter("Microsoft.AspNetCore.Hosting")
    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
    .AddMeter("System.Net.Http")
    .AddMeter("Mizan.Mcp")
    .AddPrometheusExporter()
);

otel.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddHttpClientInstrumentation();
    tracing.AddSource("Mizan.Mcp.Tools");

    if (!string.IsNullOrWhiteSpace(tracingEndpoint))
    {
        tracing.AddOtlpExporter(o => o.Endpoint = new Uri(tracingEndpoint));
    }
});

if (!string.IsNullOrWhiteSpace(lokiEndpoint))
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        );
        logging.AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(lokiEndpoint);
            o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        });
    });
}

// The container does not exist while the server is configured, and a task finishes after
// its request is over, so work that outlives a request reaches services through this.
IServiceProvider? rootServices = null;

// Only these tools can take a task. A task is for work that may outlast a chat turn,
// which here means the ones that call an AI provider or build a whole export.
var taskTools = toolset == McpToolset.Tasks
    ? new HashSet<string> { "estimate_food", "plan_meals" }
    : new HashSet<string> { "ask_ai", "suggest_meals", "analyze_food_image", "generate_meal_plan", "export_profile" };

// MCP Server
var mcpServer = builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "mizan-mcp",
        Version = "3.0.0"
    };
    options.ServerInstructions = McpServerInstructions.For(toolset);

    // Skills over MCP: the server advertises the extension and answers its two methods.
    // The skill files themselves are ordinary resources, registered below.
    options.Capabilities ??= new ServerCapabilities();
    options.Capabilities.Extensions ??= new Dictionary<string, object>();
    options.Capabilities.Extensions[McpSkillCatalog.ExtensionId] = new JsonObject();
    options.RequestHandlers =
    [
        new McpServerRequestHandler
        {
            Method = "skills/list",
            Handler = (request, ct) => ValueTask.FromResult<JsonNode?>(McpSkillCatalog.List(toolset)),
        },
        new McpServerRequestHandler
        {
            Method = "skills/get",
            Handler = (request, ct) =>
            {
                var uri = request.Params?["uri"]?.GetValue<string>();
                var found = McpSkillCatalog.Get(uri, toolset)
                    ?? throw new McpProtocolException($"Unknown skill: {uri}", McpErrorCode.InvalidParams);
                return ValueTask.FromResult<JsonNode?>(found);
            },
        },
    ];
})
.WithHttpTransport(http =>
{
    // Stateless: no session to pin a client to one instance. Older clients that
    // still send initialize are served the same way.
    http.Stateless = builder.Configuration.GetValue("Mcp:Stateless", true);
    http.IdleTimeout = TimeSpan.FromMinutes(30);
});

if (toolset == McpToolset.Tasks)
{
    // The task tools: the API lists what this caller may use and runs each call. A tool the
    // grant does not cover is not listed, and a call to it is refused by the API.
    mcpServer
        .WithListToolsHandler(async (context, cancellationToken) => new ListToolsResult
        {
            Tools = await rootServices!.GetRequiredService<TaskToolProxy>().ListAsync(cancellationToken),
        })
        .WithCallToolHandler(async (context, cancellationToken) =>
            await rootServices!.GetRequiredService<TaskToolProxy>().CallAsync(context.Params?.Name ?? string.Empty, context.Params?.Arguments, cancellationToken))
        .WithPrompts<MizanTaskPrompts>();
}
else
{
    mcpServer
.WithTools<FoodTools>()
.WithTools<PriceTools>()
.WithTools<RecipeTools>()
.WithTools<MealTools>()
.WithTools<NutritionTools>()
.WithTools<GoalTools>()
.WithTools<MealPlanTools>()
.WithTools<ShoppingListTools>()
.WithTools<BodyMeasurementTools>()
.WithTools<WorkoutTools>()
.WithTools<WorkoutTemplateTools>()
.WithTools<ExerciseTools>()
.WithTools<SocialTools>()
.WithTools<NotificationTools>()
.WithTools<ReminderTools>()
.WithTools<AdminTools>()
.WithTools<AdminPlatformTools>()
.WithTools<AdminNotificationTools>()
.WithTools<AchievementTools>()
.WithTools<ProfileTools>()
.WithTools<HouseholdTools>()
.WithTools<TrainerTools>()
.WithTools<AiTools>()
.WithTools<UploadTools>()
.WithPrompts<MizanPrompts>();
}

mcpServer
.WithMcpApps()
.WithResources<MizanResources>()
.WithResources<ReferenceResources>()
.WithResources(MizanApps.Resources(toolset))
.WithResources(McpSkillCatalog.FilesFor(toolset).Select(file => McpServerResource.Create(
    () => file.Text,
    new McpServerResourceCreateOptions { UriTemplate = file.Uri, Name = file.Uri["skill://".Length..], MimeType = file.MimeType })))
.WithTasks(
    new ApiMcpTaskStore(() => rootServices!.GetRequiredService<IServiceScopeFactory>()),
    options => options.ExecutionModeSelector = context =>
    {
        // A call the grant does not cover, or one over the monthly cap, is refused on the spot
        // instead of becoming a task that can only fail.
        var name = context.Params?.Name ?? string.Empty;
        if (!taskTools.Contains(name) || McpGrantAccess.LimitReached(null, links) is not null) return McpTaskExecutionMode.Synchronous;
        // Task tools: the caller's cached manifest says whether the API would take the call. This choice
        // cannot wait on the API, so without a cached manifest the call is answered at once (the API still checks it).
        var allowed = toolset == McpToolset.Tasks
            ? rootServices!.GetRequiredService<TaskToolProxy>().ListsCached(name) == true
            : McpToolScopes.TryGet(name, out var scope) && McpGrantAccess.Allowed(null, scope);
        return allowed ? McpTaskExecutionMode.Optional : McpTaskExecutionMode.Synchronous;
    })
.WithRequestFilters(filters =>
{
    // A connected app sees only the tools its grant covers, so a read-only
    // connection never even learns that write tools exist.
    filters.AddListToolsFilter(next => async (context, cancellationToken) =>
    {
        var result = await next(context, cancellationToken);
        var held = McpGrantAccess.Held(context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext);

        // The task tools' list is already the caller's own manifest, narrowed by the API.
        if (toolset == McpToolset.Legacy)
        {
            result.Tools = result.Tools
                .Where(tool => McpToolScopes.TryGet(tool.Name, out var scope) && McpScopes.Allows(held, scope))
                .ToList();
        }

        foreach (var tool in result.Tools)
        {
            tool.Title ??= McpToolText.Title(tool.Name);
            tool.Annotations ??= new ToolAnnotations();
            // Every tool talks only to Mizan, never to the open internet.
            tool.Annotations.OpenWorldHint = false;
        }

        return result;
    });

    // Resources and prompts are held to the same grant as tools. A client sees only
    // the ones it may use, and a direct read of one it may not is refused.
    filters.AddListResourcesFilter(next => async (context, cancellationToken) =>
    {
        var result = await next(context, cancellationToken);
        var http = context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var kept = new List<ModelContextProtocol.Protocol.Resource>();
        foreach (var resource in result.Resources)
        {
            if (await ResourceAllowedAsync(http, resource.Uri, cancellationToken)) kept.Add(resource);
        }
        result.Resources = kept;
        return result;
    });

    filters.AddListResourceTemplatesFilter(next => async (context, cancellationToken) =>
    {
        var result = await next(context, cancellationToken);
        var http = context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        result.ResourceTemplates = result.ResourceTemplates
            .Where(t => McpResourceScopes.TryGet(t.UriTemplate.Replace("{", "x").Replace("}", "x"), out var scope) && McpGrantAccess.Allowed(http, scope))
            .ToList();
        return result;
    });

    filters.AddReadResourceFilter(next => async (context, cancellationToken) =>
    {
        var http = context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var uri = context.Params?.Uri ?? string.Empty;
        var userId = McpGrantAccess.UserId(http);
        var grantId = McpGrantAccess.GrantId(http);
        var backend = http?.RequestServices.GetService<IBackendApiClient>();
        var name = McpResourceScopes.Pattern(uri);

        if (http?.User.Identity?.IsAuthenticated != true)
            throw new McpProtocolException("Authentication required. Connect Mizan from your MCP client.", McpErrorCode.InvalidRequest);

        McpResourceScopes.TryGet(uri, out var scope);
        if (!await ResourceAllowedAsync(http, uri, cancellationToken))
        {
            if (backend != null && grantId != Guid.Empty) await backend.LogUsageAsync(grantId, userId, "resource", name, false, "Not allowed by the connection", 0);
            throw new McpProtocolException(McpToolText.NotAllowed(name, scope), McpErrorCode.InvalidParams);
        }

        // Skills are public instructions and app pages hold no data, so reading one is free. Everything else shares the monthly cap with tools.
        if (uri.StartsWith("skill://", StringComparison.Ordinal) || uri.StartsWith("ui://", StringComparison.Ordinal) || McpResourceScopes.IsReference(uri))
            return await next(context, cancellationToken);
        if (McpGrantAccess.LimitReached(http, links) is { } limit) throw new McpProtocolException(limit, McpErrorCode.InvalidRequest);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next(context, cancellationToken);
            if (backend != null && grantId != Guid.Empty) await backend.LogUsageAsync(grantId, userId, "resource", name, true, null, (int)sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            if (backend != null && grantId != Guid.Empty) await backend.LogUsageAsync(grantId, userId, "resource", name, false, ex.Message, (int)sw.ElapsedMilliseconds);
            throw;
        }
    });

    filters.AddListPromptsFilter(next => async (context, cancellationToken) =>
    {
        var result = await next(context, cancellationToken);
        var http = context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var kept = new List<Prompt>();
        foreach (var prompt in result.Prompts)
        {
            if (await PromptAllowedAsync(http, prompt.Name, cancellationToken)) kept.Add(prompt);
        }
        result.Prompts = kept;
        return result;
    });

    filters.AddGetPromptFilter(next => async (context, cancellationToken) =>
    {
        var http = context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var name = context.Params?.Name ?? string.Empty;
        var userId = McpGrantAccess.UserId(http);
        var grantId = McpGrantAccess.GrantId(http);
        var backend = http?.RequestServices.GetService<IBackendApiClient>();

        if (!await PromptAllowedAsync(http, name, cancellationToken))
            throw new McpProtocolException(McpToolText.NotAllowed(name, McpResourceScopes.PromptScopes.GetValueOrDefault(name)), McpErrorCode.InvalidParams);

        var result = await next(context, cancellationToken);
        // A prompt is only text and reads nothing, so it is recorded but not counted against the monthly cap.
        if (backend != null && grantId != Guid.Empty) await backend.LogUsageAsync(grantId, userId, "prompt", name, true, null, 0);
        return result;
    });

    filters.AddCallToolFilter(next => async (context, cancellationToken) =>
    {
        var httpContext = context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var toolName = context.Params?.Name ?? "unknown";

        Log.Information("[MCP Tool] Calling tool: {ToolName}", toolName);

        var caller = McpCallIdentity.Of(httpContext);
        if (caller?.Identity?.IsAuthenticated != true)
        {
            Log.Warning("[MCP Tool] Tool call rejected - user not authenticated. Tool: {ToolName}", toolName);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = "Authentication required. Connect Mizan from your MCP client." }],
                IsError = true
            };
        }

        var userId = Guid.TryParse(caller.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;
        var grantId = Guid.TryParse(caller.FindFirst(GrantClaims.GrantId)?.Value, out var gid) ? gid : Guid.Empty;
        // A task's tool runs after the request is over, so this does not come from the request's own services.
        using var backendScope = rootServices?.CreateScope();
        var backend = backendScope?.ServiceProvider.GetService<IBackendApiClient>();

        // Fail closed: a tool with no scope on record is refused, and so is one the grant does not cover.
        // A task tool is checked by the API, which refuses it the same way and says what is missing.
        string? requiredScope = null;
        if (toolset == McpToolset.Legacy
            && (!McpToolScopes.TryGet(toolName, out requiredScope)
                || !McpScopes.Allows(McpGrantAccess.Held(httpContext), requiredScope!)))
        {
            Log.Warning("[MCP Tool] Tool call refused by grant. Tool: {ToolName}", toolName);
            if (backend != null && userId != Guid.Empty && grantId != Guid.Empty)
            {
                await backend.LogUsageAsync(grantId, userId, "tool", toolName, false, "Not allowed by the connection", 0);
            }

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = McpToolText.NotAllowed(toolName, requiredScope) }],
                IsError = true
            };
        }

        if (McpGrantAccess.LimitReached(httpContext, links) is { } limitReached)
        {
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = limitReached }],
                IsError = true
            };
        }

        Log.Debug("[MCP Tool] Tool: {ToolName}, UserId: {UserId}", toolName, userId);

        using var activity = mcpActivitySource.StartActivity($"Tool:{toolName}");
        activity?.SetTag("mcp.tool.name", toolName);
        activity?.SetTag("mcp.user.id", userId.ToString());

        var sw = Stopwatch.StartNew();

        try
        {
            var answered = await next(context, cancellationToken);
            // A task tool's result already carries its structured content from the API.
            var result = toolset == McpToolset.Legacy ? WidgetResults.WithStructuredContent(toolName, answered) : answered;
            sw.Stop();

            activity?.SetTag("mcp.tool.success", result.IsError != true);
            toolCallCounter.Add(1,
                new KeyValuePair<string, object?>("tool", toolName),
                new KeyValuePair<string, object?>("success", (result.IsError != true).ToString()));
            toolCallDuration.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("tool", toolName));

            Log.Information("[MCP Tool] Tool succeeded: {ToolName} (elapsed: {ElapsedMs}ms, error: {IsError})",
                toolName, sw.ElapsedMilliseconds, result.IsError);

            if (backend != null && userId != Guid.Empty && grantId != Guid.Empty)
            {
                await backend.LogUsageAsync(grantId, userId, "tool", toolName, result.IsError != true, null, (int)sw.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();

            activity?.SetTag("mcp.tool.success", false);
            activity?.SetTag("mcp.tool.error", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            toolCallCounter.Add(1,
                new KeyValuePair<string, object?>("tool", toolName),
                new KeyValuePair<string, object?>("success", "False"));
            toolCallDuration.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("tool", toolName));

            Log.Error("[MCP Tool] Tool failed: {ToolName} (elapsed: {ElapsedMs}ms, error: {Error})",
                toolName, sw.ElapsedMilliseconds, ex.Message);

            if (backend != null && userId != Guid.Empty && grantId != Guid.Empty)
            {
                await backend.LogUsageAsync(grantId, userId, "tool", toolName, false, ex.Message, (int)sw.ElapsedMilliseconds);
            }

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = ex.Message }],
                IsError = true
            };
        }
    });
});

var app = builder.Build();
rootServices = app.Services;

var showMcpLogs = app.Configuration.GetValue<bool>("SHOW_MCP_LOGS", false);
Log.Information("Mizan MCP Server v2.0.0 starting on {Urls}", string.Join(", ", app.Urls));
Log.Information("[MCP] Detailed logging enabled: {Enabled}", showMcpLogs);
Log.Information("[MCP] Environment: {Environment}", builder.Configuration["ASPNETCORE_ENVIRONMENT"]);

// The advertised metadata comes from Mcp:PublicUrl, so forwarded headers only decide the
// client address and scheme the server records. Trust them from the loopback address, or
// from the one proxy named in ReverseProxy:KnownProxy, and from nobody else.
var forwarded = new ForwardedHeadersOptions();
ReverseProxyHeaders.Configure(forwarded, app.Configuration);
app.UseForwardedHeaders(forwarded);

app.UseAuthentication();
// An outage of the API that validates tokens is a 503 with Retry-After, not a 401 that
// sends every client back through sign-in.
app.UseMiddleware<McpAuthOutageMiddleware>();
app.UseAuthorization();

// A task keeps running after its request ends. Naming the caller here lets that
// background work act as them, and only them.
app.Use(async (context, next) =>
{
    McpCallIdentity.Current = context.User.Identity?.IsAuthenticated == true ? context.User : null;
    await next();
});

app.MapMcp("/mcp").RequireAuthorization();

// The protected resource metadata (RFC 9728). The 401 points clients at /mcp/.well-known/..., which production
// already routes to this service. Clients that build the URL themselves use RFC 9728's form, the well-known
// segment at the root followed by the resource's path (/.well-known/oauth-protected-resource/mcp), and some try
// the bare root. All three answer the same document; the proxy routes the root two here (docs/MCP.md).
var resourcePath = new Uri(publicUrl).AbsolutePath.TrimEnd('/');
IResult ProtectedResource() => Results.Json(new Dictionary<string, object>
{
    ["resource"] = publicUrl,
    ["authorization_servers"] = new[] { authorizationServer },
    ["scopes_supported"] = McpScopes.All.Where(scope => !McpScopes.IsAdminScope(scope)).ToArray(),
    ["bearer_methods_supported"] = new[] { "header" },
    ["resource_name"] = "Mizan",
});
foreach (var path in new[]
{
    $"{resourcePath}/.well-known/oauth-protected-resource",
    $"/.well-known/oauth-protected-resource{resourcePath}",
    "/.well-known/oauth-protected-resource",
}.Distinct())
{
    app.MapGet(path, ProtectedResource).AllowAnonymous();
}
// The Claude plugin for this deployment: its connector points at this server (docs/MCP.md#claude-plugin).
// Public, like the skills it carries; nginx already routes /mcp/* here.
var plugin = new PluginPackage(publicUrl, toolset);
app.MapGet($"{resourcePath}/plugin.zip", (HttpContext context) =>
{
    context.Response.Headers.ETag = plugin.ETag;
    context.Response.Headers.CacheControl = "public, max-age=3600";
    if (context.Request.Headers.IfNoneMatch.Contains(plugin.ETag)) return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.File(plugin.Bytes, "application/zip", PluginPackage.FileName);
}).AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mizan-mcp", version = "2.0.0" }));
app.MapPrometheusScrapingEndpoint();

Log.Information("[MCP] MCP endpoint mapped to /mcp");
Log.Information("[MCP] Backend API URL: {BackendUrl}", builder.Configuration["BACKEND_API_URL"]);
Log.Information("[MCP] Server starting...");

app.Run();

// In tasks mode an app page is open exactly when a tool in the caller's manifest renders it, so it
// follows the areas the API checks. Everything else, and every page in legacy mode, keeps its scope.
async Task<bool> ResourceAllowedAsync(HttpContext? http, string uri, CancellationToken cancellationToken)
{
    if (toolset == McpToolset.Tasks && uri.StartsWith("ui://", StringComparison.Ordinal))
    {
        return http?.User.Identity?.IsAuthenticated == true
            && await rootServices!.GetRequiredService<TaskToolProxy>().ListsAppAsync(uri, cancellationToken);
    }

    return McpResourceScopes.TryGet(uri, out var scope) && McpGrantAccess.Allowed(http, scope);
}

// A prompt needs its scope; in tasks mode a prompt built on a role's tool also needs that tool in the manifest.
async Task<bool> PromptAllowedAsync(HttpContext? http, string name, CancellationToken cancellationToken)
{
    if (!McpResourceScopes.PromptScopes.TryGetValue(name, out var scope) || !McpGrantAccess.Allowed(http, scope)) return false;
    if (toolset != McpToolset.Tasks || !MizanTaskPrompts.Tools.TryGetValue(name, out var tool)) return true;
    return http?.User.Identity?.IsAuthenticated == true
        && await rootServices!.GetRequiredService<TaskToolProxy>().ListsAsync(tool, cancellationToken);
}

public partial class Program { }
