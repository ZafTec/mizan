using Mizan.Mcp.Server.Authentication;
using Mizan.Mcp.Server.Services;
using Mizan.Mcp.Server.Tools;
using Mizan.Mcp.Server.Logging;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using ModelContextProtocol.Protocol;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = McpLoggingConfiguration.CreateLogger(builder.Configuration);
builder.Host.UseSerilog();

// Auth
builder.Services.AddAuthentication(McpTokenAuthenticationOptions.DefaultScheme)
    .AddScheme<McpTokenAuthenticationOptions, McpTokenAuthenticationHandler>(
        McpTokenAuthenticationOptions.DefaultScheme, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

// Backend API client
var backendUrl = builder.Configuration["BACKEND_API_URL"]
                 ?? builder.Configuration["MizanApiUrl"]
                 ?? "http://mizan-backend:8080";

var apiKey = builder.Configuration["Mcp:ServiceApiKey"]
             ?? builder.Configuration["ServiceApiKey"]
             ?? throw new InvalidOperationException("ServiceApiKey not configured");

builder.Services.AddHttpClient<IBackendApiClient, BackendApiClient>(client =>
{
    client.BaseAddress = new Uri(backendUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
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

    if (tracingEndpoint != null)
    {
        tracing.AddOtlpExporter(o => o.Endpoint = new Uri(tracingEndpoint));
    }
});

if (lokiEndpoint != null)
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

// MCP Server
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "mizan-mcp",
        Version = "2.0.0"
    };
})
.WithHttpTransport(http =>
{
    http.Stateless = false;
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
.WithTools<ExerciseTools>()
.WithTools<AchievementTools>()
.WithTools<ProfileTools>()
.WithTools<HouseholdTools>()
.WithTools<TrainerTools>()
.AddAuthorizationFilters()
.WithRequestFilters(filters =>
{
    filters.AddCallToolFilter(next => async (context, cancellationToken) =>
    {
        var httpContext = context.Server.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var toolName = context.Params?.Name ?? "unknown";

        Log.Information("[MCP Tool] Calling tool: {ToolName}", toolName);

        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            Log.Warning("[MCP Tool] Tool call rejected - user not authenticated. Tool: {ToolName}", toolName);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = "Authentication required. Provide a valid MCP token." }],
                IsError = true
            };
        }

        var userId = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;
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

            var backend = httpContext.RequestServices.GetService<IBackendApiClient>();
            var tokenId = Guid.TryParse(httpContext.User.FindFirst("mcp_token_id")?.Value, out var tid) ? tid : Guid.Empty;

            if (backend != null && userId != Guid.Empty)
            {
                await backend.LogUsageAsync(tokenId, userId, toolName, null, result.IsError != true, null, (int)sw.ElapsedMilliseconds);
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

            var backend = httpContext.RequestServices.GetService<IBackendApiClient>();
            var tokenId = Guid.TryParse(httpContext.User.FindFirst("mcp_token_id")?.Value, out var tid) ? tid : Guid.Empty;

            if (backend != null && userId != Guid.Empty)
            {
                await backend.LogUsageAsync(tokenId, userId, toolName, null, false, ex.Message, (int)sw.ElapsedMilliseconds);
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

var showMcpLogs = app.Configuration.GetValue<bool>("SHOW_MCP_LOGS", false);
Log.Information("Mizan MCP Server v2.0.0 starting on {Urls}", string.Join(", ", app.Urls));
Log.Information("[MCP] Detailed logging enabled: {Enabled}", showMcpLogs);
Log.Information("[MCP] Environment: {Environment}", builder.Configuration["ASPNETCORE_ENVIRONMENT"]);

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mizan-mcp", version = "2.0.0" }));
app.MapPrometheusScrapingEndpoint();

Log.Information("[MCP] MCP endpoint mapped to /mcp");
Log.Information("[MCP] Backend API URL: {BackendUrl}", builder.Configuration["BACKEND_API_URL"]);
Log.Information("[MCP] Server starting...");

app.Run();

public partial class Program { }
