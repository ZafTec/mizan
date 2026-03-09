using Mizan.Mcp.Server.Authentication;
using Mizan.Mcp.Server.Services;
using Mizan.Mcp.Server.Tools;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using ModelContextProtocol.Protocol;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Mizan.Mcp.Server")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/mcp-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File(
        path: "logs/mcp-errors-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90,
        restrictedToMinimumLevel: LogEventLevel.Error)
    .CreateLogger();

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
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = "Authentication required. Provide a valid MCP token." }],
                IsError = true
            };
        }

        var sw = Stopwatch.StartNew();
        var toolName = context.Params?.Name ?? "unknown";

        try
        {
            var result = await next(context, cancellationToken);
            sw.Stop();

            var backend = context.Server.Services?.GetService<IBackendApiClient>();
            var userId = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;
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

            var backend = context.Server.Services?.GetService<IBackendApiClient>();
            var userId = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;
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

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp");

Log.Information("Mizan MCP Server v2.0.0 starting on {Urls}", string.Join(", ", app.Urls));
app.Run();

public partial class Program { }
