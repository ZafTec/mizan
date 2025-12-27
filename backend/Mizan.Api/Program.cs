using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Mizan.Api.Authentication;
using Mizan.Api.Hubs;
using Mizan.Application;
using Mizan.Infrastructure;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Mizan API (BFF Backend)", Version = "v1" });
    c.AddSecurityDefinition("BFF", new()
    {
        Description = "BFF Trusted Secret (X-BFF-Secret) and User ID (X-User-Id) headers",
        Name = "X-BFF-Secret",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "BFF"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "BFF" }
            },
            Array.Empty<string>()
        }
    });
});

// Add FluentValidation rules to Swagger/OpenAPI schema
builder.Services.AddFluentValidationRulesToSwagger();

// Application & Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// BFF Authentication - validates trusted secret from frontend
var trustedSecret = builder.Configuration["Bff:TrustedSecret"] ?? throw new InvalidOperationException("BFF:TrustedSecret is required");

builder.Services.AddAuthentication("BffTrustedSource")
    .AddScheme<BffAuthenticationSchemeOptions, BffAuthenticationHandler>("BffTrustedSource", options =>
    {
        options.TrustedSecret = trustedSecret;
    });

builder.Services.AddAuthorization();

// Redis connection for caching and SignalR
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrEmpty(redisConnectionString))
{
    // Register Redis connection multiplexer as singleton for JWKS caching
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString);
        return ConnectionMultiplexer.Connect(configuration);
    });
}

// SignalR with Redis backplane for scaling
var signalRBuilder = builder.Services.AddSignalR();

if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("Mizan");
    });
}

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? new[] { "http://localhost:3000" };

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL")!)
    .AddRedis(redisConnectionString ?? "localhost");

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseCors("AllowFrontend");

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandlerFeature?.Error;

        if (exception is ValidationException validationEx)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                errors = validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            });
        }
        else if (exception is UnauthorizedAccessException)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
        }
        else
        {
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

// Apply migrations automatically in development
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

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
