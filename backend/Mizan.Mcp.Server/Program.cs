using Mizan.Mcp.Server.Authentication;
using Mizan.Mcp.Server.Services;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Environment.EnvironmentName;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", environment)
    .Enrich.WithProperty("Application", "Mizan.Mcp.Server")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/mcp-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{RequestId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File(
        path: "logs/mcp-errors-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90,
        restrictedToMinimumLevel: LogEventLevel.Error,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{RequestId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting Mizan MCP Server - Environment: {Environment}", environment);

builder.Services.AddControllers();
builder.Services.AddHttpClient<IBackendClient, BackendClient>();
builder.Services.AddScoped<McpToolHandler>();

builder.Services.AddAuthentication(McpTokenAuthenticationOptions.DefaultScheme)
    .AddScheme<McpTokenAuthenticationOptions, McpTokenAuthenticationHandler>(
        McpTokenAuthenticationOptions.DefaultScheme, _ => { });

builder.Services.AddAuthorization();

var app = builder.Build();

Log.Information("Mizan MCP Server starting on {Urls}", string.Join(", ", app.Urls));

try
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.Run();
    Log.Information("Mizan MCP Server stopped gracefully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Mizan MCP Server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
