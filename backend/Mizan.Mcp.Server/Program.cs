using Microsoft.AspNetCore.HttpOverrides;
using Mizan.Contracts.Mcp;
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
            ScopesSupported = McpScopes.All.Where(scope => scope != McpScopes.Admin).ToList(),
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
var taskTools = new HashSet<string> { "ask_ai", "suggest_meals", "analyze_food_image", "export_profile" };

// MCP Server
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "mizan-mcp",
        Version = "3.0.0"
    };
    options.ServerInstructions = McpServerInstructions.Text;

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
            Handler = (request, ct) => ValueTask.FromResult<JsonNode?>(McpSkillCatalog.List()),
        },
        new McpServerRequestHandler
        {
            Method = "skills/get",
            Handler = (request, ct) =>
            {
                var uri = request.Params?["uri"]?.GetValue<string>();
                var found = McpSkillCatalog.Get(uri)
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
})
.WithTools<FoodTools>()
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
.WithTools<AdminTools>()
.WithTools<AchievementTools>()
.WithTools<ProfileTools>()
.WithTools<HouseholdTools>()
.WithTools<TrainerTools>()
.WithTools<AiTools>()
.WithTools<UploadTools>()
.WithMcpApps()
.WithResources<MizanResources>()
.WithResources(MizanApps.Resources())
.WithResources(McpSkillCatalog.Files.Select(file => McpServerResource.Create(
    () => file.Text,
    new McpServerResourceCreateOptions { UriTemplate = file.Uri, Name = file.Uri["skill://".Length..], MimeType = file.MimeType })))
.WithPrompts<MizanPrompts>()
.WithTasks(
    new ApiMcpTaskStore(() => rootServices!.GetRequiredService<IServiceScopeFactory>()),
    options => options.ExecutionModeSelector = context =>
    {
        // A call the grant does not cover, or one over the monthly cap, is refused on the spot
        // instead of becoming a task that can only fail.
        var name = context.Params?.Name ?? string.Empty;
        var allowed = McpToolScopes.TryGet(name, out var scope) && McpGrantAccess.Allowed(null, scope) && McpGrantAccess.LimitReached(null) is null;
        return allowed && taskTools.Contains(name) ? McpTaskExecutionMode.Optional : McpTaskExecutionMode.Synchronous;
    })
.WithRequestFilters(filters =>
{
    // A connected app sees only the tools its grant covers, so a read-only
    // connection never even learns that write tools exist.
    filters.AddListToolsFilter(next => async (context, cancellationToken) =>
    {
        var result = await next(context, cancellationToken);
        var held = McpGrantAccess.Held(context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext);

        result.Tools = result.Tools
            .Where(tool => McpToolScopes.TryGet(tool.Name, out var scope) && McpScopes.Allows(held, scope))
            .ToList();

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
        result.Resources = result.Resources
            .Where(r => McpResourceScopes.TryGet(r.Uri, out var scope) && McpGrantAccess.Allowed(http, scope))
            .ToList();
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

        if (!McpResourceScopes.TryGet(uri, out var scope) || !McpGrantAccess.Allowed(http, scope))
        {
            if (backend != null && grantId != Guid.Empty) await backend.LogUsageAsync(grantId, userId, "resource", name, false, "Not allowed by the connection", 0);
            throw new McpProtocolException(McpToolText.NotAllowed(name, scope), McpErrorCode.InvalidParams);
        }

        // Skills are public instructions and app pages hold no data, so reading one is free. Everything else shares the monthly cap with tools.
        if (uri.StartsWith("skill://", StringComparison.Ordinal) || uri.StartsWith("ui://", StringComparison.Ordinal))
            return await next(context, cancellationToken);
        if (McpGrantAccess.LimitReached(http) is { } limit) throw new McpProtocolException(limit, McpErrorCode.InvalidRequest);

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
        result.Prompts = result.Prompts
            .Where(p => McpResourceScopes.PromptScopes.TryGetValue(p.Name, out var scope) && McpGrantAccess.Allowed(http, scope))
            .ToList();
        return result;
    });

    filters.AddGetPromptFilter(next => async (context, cancellationToken) =>
    {
        var http = context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var name = context.Params?.Name ?? string.Empty;
        var userId = McpGrantAccess.UserId(http);
        var grantId = McpGrantAccess.GrantId(http);
        var backend = http?.RequestServices.GetService<IBackendApiClient>();

        if (!McpResourceScopes.PromptScopes.TryGetValue(name, out var scope) || !McpGrantAccess.Allowed(http, scope))
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
        if (!McpToolScopes.TryGet(toolName, out var requiredScope)
            || !McpScopes.Allows(McpGrantAccess.Held(httpContext), requiredScope))
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

        if (int.TryParse(caller.FindFirst("mcp_usage_limit")?.Value, out var monthlyLimit) &&
            int.TryParse(caller.FindFirst("mcp_usage_used")?.Value, out var usedThisMonth) &&
            usedThisMonth >= monthlyLimit)
        {
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = $"[MONTHLY LIMIT REACHED] The free plan includes {monthlyLimit} MCP tool calls per month. Upgrade at https://mizan.zaftech.co/billing." }],
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
            var result = await next(context, cancellationToken);
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

// Behind the reverse proxy the request says http and an internal host. Without these
// headers the protected resource metadata would advertise the wrong address.
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
};
forwarded.KnownIPNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

app.UseAuthentication();
app.UseAuthorization();

// A task keeps running after its request ends. Naming the caller here lets that
// background work act as them, and only them.
app.Use(async (context, next) =>
{
    McpCallIdentity.Current = context.User.Identity?.IsAuthenticated == true ? context.User : null;
    await next();
});

app.MapMcp("/mcp").RequireAuthorization();

// Where the 401 points clients. Under /mcp, which production already routes to this
// service, so signing in needs no extra proxy route. The SDK also serves the standard
// root /.well-known path for clients that look there.
app.MapGet("/mcp/.well-known/oauth-protected-resource", () => Results.Json(new Dictionary<string, object>
{
    ["resource"] = publicUrl,
    ["authorization_servers"] = new[] { authorizationServer },
    ["scopes_supported"] = McpScopes.All.Where(scope => scope != McpScopes.Admin).ToArray(),
    ["bearer_methods_supported"] = new[] { "header" },
    ["resource_name"] = "Mizan",
})).AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mizan-mcp", version = "2.0.0" }));
app.MapPrometheusScrapingEndpoint();

Log.Information("[MCP] MCP endpoint mapped to /mcp");
Log.Information("[MCP] Backend API URL: {BackendUrl}", builder.Configuration["BACKEND_API_URL"]);
Log.Information("[MCP] Server starting...");

app.Run();

public partial class Program { }
