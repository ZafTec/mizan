using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Mizan.Api.Authentication;
using Mizan.Api.Hubs;
using Mizan.Api.Middleware;
using Mizan.Application;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Mizan.Application.Exceptions;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Environment.EnvironmentName;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", environment)
    .Enrich.WithProperty("Application", "Mizan.Api")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/mizan-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{RequestId}] [{UserId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File(
        path: "logs/mizan-errors-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90,
        restrictedToMinimumLevel: LogEventLevel.Error,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{RequestId}] [{UserId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting Mizan API - Environment: {Environment}", environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Mizan API", Version = "v1" });
    c.SupportNonNullableReferenceTypes();
    c.NonNullableReferenceTypesAsRequired();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddFluentValidationRulesToSwagger();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtIssuer = builder.Configuration["BetterAuth:Issuer"]
    ?? builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["BetterAuth:Audience"]
    ?? builder.Configuration["Jwt:Audience"];

builder.Services.AddHttpClient<IJwksProvider, JwksProvider>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
            ValidIssuer = jwtIssuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = false,
            RequireSignedTokens = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ValidAlgorithms = new[] { "EdDSA" },
        };
        options.TokenHandlers.Clear();
        options.TokenHandlers.Add(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler());
        options.TokenValidationParameters.SignatureValidator = EdDsaJwtSignatureValidator.Validate;

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirst("sub")?.Value
                    ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Invalid user id");
                    return;
                }

                var userStatus = context.HttpContext.RequestServices
                    .GetRequiredService<IUserStatusService>();
                var status = await userStatus.GetStatusAsync(userId, context.HttpContext.RequestAborted);

                if (!status.Exists)
                {
                    context.Fail("User not found");
                    return;
                }

                if (!status.EmailVerified)
                {
                    context.Fail("Email not verified");
                    return;
                }

                if (status.IsBanned)
                {
                    context.Fail("User banned");
                    return;
                }
            }
        };
    })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationSchemeOptions.DefaultScheme, options =>
    {
        options.ApiKey = builder.Configuration["Mcp:ServiceApiKey"] ?? throw new InvalidOperationException("Mcp:ServiceApiKey is not configured");
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationSchemeOptions.DefaultScheme)
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("RequireAdmin", policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationSchemeOptions.DefaultScheme)
        .RequireAuthenticatedUser()
        .RequireRole("admin"));

    options.AddPolicy("RequireTrainer", policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationSchemeOptions.DefaultScheme)
        .RequireAuthenticatedUser()
        .RequireRole("trainer"));
});

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        try
        {
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            var multiplexer = ConnectionMultiplexer.Connect(configuration);
            Log.Information("Redis connection established successfully");
            return multiplexer;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to Redis at {ConnectionString}", redisConnectionString);
            throw;
        }
    });
}
else
{
    Log.Warning("Redis connection string not configured - caching and SignalR backplane will be unavailable");
}

var signalRBuilder = builder.Services.AddSignalR();

if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("Mizan");
    });
    Log.Information("SignalR configured with Redis backplane");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = (builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? new[] { "http://localhost:3000" })
            .Select(o => o.Trim()).Where(o => !string.IsNullOrEmpty(o)).ToArray();

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL")!)
    .AddRedis(redisConnectionString ?? "localhost");

// ============================================================================
// OpenTelemetry Configuration
// ============================================================================
var serviceName = "Mizan.Api";
var serviceVersion = "1.0.0";

var tracingEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];
var lokiEndpoint = builder.Configuration["LOKI_OTLP_ENDPOINT"];

var otel = builder.Services.AddOpenTelemetry();

otel.ConfigureResource(resource => resource
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
);

otel.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddMeter("Microsoft.AspNetCore.Hosting")
    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
    .AddMeter("System.Net.Http")
    .AddMeter("System.Net.NameResolution")
    .AddPrometheusExporter()
);

otel.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddHttpClientInstrumentation();
    tracing.AddEntityFrameworkCoreInstrumentation();

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

var app = builder.Build();

JwksProviderAccessor.Set(app.Services.GetRequiredService<IJwksProvider>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});

app.UseCors("AllowFrontend");

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandlerFeature?.Error;

        if (exception is ValidationException validationEx)
        {
            Log.Warning(
                "Validation failed for {Path} - {ErrorCount} errors",
                context.Request.Path,
                validationEx.Errors.Count());

            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                errors = validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            });
        }
        else if (exception is DomainValidationException domainValidationEx)
        {
            Log.Warning("Domain validation failed for {Path}: {Message}",
                context.Request.Path, domainValidationEx.Message);

            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = domainValidationEx.Message });
        }
        else if (exception is EntityNotFoundException notFoundEx)
        {
            Log.Warning("Entity not found for {Path}: {Message}",
                context.Request.Path, notFoundEx.Message);

            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = notFoundEx.Message });
        }
        else if (exception is ForbiddenAccessException forbiddenEx)
        {
            Log.Warning("Forbidden access for {Path}: {Message}",
                context.Request.Path, forbiddenEx.Message);

            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = forbiddenEx.Message });
        }
        else if (exception is UnauthorizedAccessException)
        {
            Log.Warning(
                "Unauthorized access attempt to {Path}",
                context.Request.Path);

            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
        }
        else
        {
            Log.Error(
                exception,
                "Unhandled exception for {Path}",
                context.Request.Path);

            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
        }
    });
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<Mizan.Infrastructure.Data.MizanDbContext>();
    try
    {
        dbContext.Database.Migrate();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to apply database migrations");
    }
}

Log.Information("Mizan API starting on {Urls}", string.Join(", ", app.Urls));

try
{
    app.Run();
    Log.Information("Mizan API stopped gracefully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Mizan API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
