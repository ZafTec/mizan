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

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await next(context, cancellationToken);
            sw.Stop();

            Log.Information("[MCP Tool] Tool succeeded: {ToolName} (elapsed: {ElapsedMs}ms, error: {IsError})",
                toolName, sw.ElapsedMilliseconds, result.IsError);

            var backend = context.Server.Services?.GetService<IBackendApiClient>();
            var tokenId = Guid.TryParse(httpContext.User.FindFirst("mcp_token_id")?.Value, out var tid) ? tid : Guid.Empty;

            if (backend != null && userId != Guid.Empty)
            {
                _ = backend.LogUsageAsync(tokenId, userId, toolName, null, result.IsError != true, null, (int)sw.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();

            Log.Error("[MCP Tool] Tool failed: {ToolName} (elapsed: {ElapsedMs}ms, error: {Error})",
                toolName, sw.ElapsedMilliseconds, ex.Message);

            var backend = context.Server.Services?.GetService<IBackendApiClient>();
            var tokenId = Guid.TryParse(httpContext.User.FindFirst("mcp_token_id")?.Value, out var tid) ? tid : Guid.Empty;

            if (backend != null && userId != Guid.Empty)
            {
                _ = backend.LogUsageAsync(tokenId, userId, toolName, null, false, ex.Message, (int)sw.ElapsedMilliseconds);
            }

            throw;
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

Log.Information("[MCP] MCP endpoint mapped to /mcp");
Log.Information("[MCP] Backend API URL: {BackendUrl}", builder.Configuration["BACKEND_API_URL"]);
Log.Information("[MCP] Server starting...");

app.Run();

public partial class Program { }
