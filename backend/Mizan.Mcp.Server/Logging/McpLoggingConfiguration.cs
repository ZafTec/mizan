using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace Mizan.Mcp.Server.Logging;

public static class McpLoggingConfiguration
{
    public static Serilog.ILogger CreateLogger(IConfiguration configuration)
    {
        var showMcpLogs = configuration.GetValue<bool>("SHOW_MCP_LOGS", false);
        var minimumLevel = showMcpLogs ? LogEventLevel.Debug : LogEventLevel.Information;

        var consoleTemplate = showMcpLogs
            ? "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            : "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Mizan.Mcp.Server")
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithExceptionDetails()
            .WriteTo.Console(outputTemplate: consoleTemplate)
            .CreateLogger();
    }
}
