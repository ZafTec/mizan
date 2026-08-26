using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mizan.Api.Authentication;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;
using Mizan.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace Mizan.Tests.Integration;

public sealed class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string[] TablesToTruncate = new[]
    {
        // ai_eval_cases is deliberately absent: the synthetic suite is seeded
        // by the migration and the publish gate has nothing to check without it.
        "ai_eval_runs",
        "ai_prompt_versions",
        "ai_prompts",
        "ai_usage_logs",
        "user_ai_consents",
        "chat_messages",
        "chat_conversations",
        "trainer_client_relationships",
        "mcp_usage_logs",
        "mcp_tokens",
        "goal_progress",
        "user_goals",
        "food_diary_entries",
        "favorite_recipes",
        "recipe_ingredients",
        "recipes",
        "foods",
        "household_members",
        "households",
        "subscriptions",
        "audit_logs",
        "users"
    };

    private readonly PostgreSqlContainer? _dbContainer;
    private readonly string _connectionString;
    private readonly string? _redisConnectionString;

    public ApiTestFixture()
    {
        // Check if we should use InMemory database (for local unit testing)
        var useInMemory = Environment.GetEnvironmentVariable("USE_INMEMORY_DATABASE")?.ToLower() == "true";

        if (useInMemory)
        {
            // Use InMemory database for fast local testing
            _connectionString = "inmemory";
            _dbContainer = null;
        }
        else
        {
            // Try multiple environment variable formats for real database
            var existingConnString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings:PostgreSQL");

            if (!string.IsNullOrWhiteSpace(existingConnString))
            {
                // Using existing DB connection (CI/CD pipeline)
                _connectionString = existingConnString;
                _dbContainer = null;
            }
            else
            {
                // Create Testcontainers PostgreSQL for local integration testing
                _dbContainer = new PostgreSqlBuilder()
                    .WithImage("postgres:18-alpine")
                    .WithDatabase("mizan_test")
                    .WithUsername("mizan")
                    .WithPassword("mizan_test_password")
                    .Build();
                _connectionString = string.Empty; // Will be set in InitializeAsync
            }
        }


        _redisConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Redis");
    }


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
        builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var connString = !string.IsNullOrEmpty(_connectionString)
                ? _connectionString
                : _dbContainer?.GetConnectionString() ?? throw new InvalidOperationException("No DB connection string available");

            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = connString,
                ["ConnectionStrings:Redis"] = _redisConnectionString,
                ["Mcp:ServiceApiKey"] = "test-api-key",
                ["Mcp:AdminServiceApiKey"] = "test-admin-api-key",
                ["RateLimits:McpTokenValidation:PermitLimit"] = "10000",
                ["RateLimits:AuthCredentials:PermitLimit"] = "10000",
                ["RateLimits:AuthEmail:PermitLimit"] = "10000",
                // Small AI ceilings so quota tests exercise the limits in a
                // handful of calls instead of hundreds.
                ["Ai:Free:DailyRequests"] = "3",
                ["Ai:Free:DailyTokens"] = "1000",
                ["Ai:Pro:DailyRequests"] = "10",
                ["Ai:Pro:DailyTokens"] = "5000",
                ["Ai:GlobalDailyTokens"] = "4000",
                ["Ai:GlobalDailyCostMicros"] = "1000000000"
            };

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext registration
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MizanDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            if (_connectionString == "inmemory")
            {
                // Use InMemory database for fast local unit testing
                services.AddDbContext<MizanDbContext>(options =>
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            }
            else
            {
                var connString = !string.IsNullOrEmpty(_connectionString)
                    ? _connectionString
                    : _dbContainer?.GetConnectionString() ?? throw new InvalidOperationException("No DB connection string available");

                // Add DbContext using real PostgreSQL connection
                services.AddDbContext<MizanDbContext>(options =>
                    options.UseNpgsql(connString));
            }

            // Identity mails verification and reset links; tests need to read
            // them, and nothing here should ever open an SMTP connection.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);

            // Nothing in a test run may reach a real provider. The fake also
            // lets a test decide what came back, which is the only way to
            // exercise the schema-validation path.
            services.RemoveAll<IAiProvider>();
            services.AddSingleton<IAiProvider>(Ai);

            // Configure minimal logging for tests
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
                logging.AddFilter("Microsoft", LogLevel.Error);
                logging.AddFilter("System", LogLevel.Error);
                logging.AddFilter("Mizan", LogLevel.Warning);
            });
        });
    }

    public async Task InitializeAsync()
    {
        if (_dbContainer != null)
        {
            await _dbContainer.StartAsync();
            // Update connection string for non-webhost usage
            var field = typeof(ApiTestFixture).GetField("_connectionString", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(this, _dbContainer.GetConnectionString());
        }

        await EnsureDatabaseAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_dbContainer != null)
        {
            await _dbContainer.StopAsync();
        }
        await base.DisposeAsync();
    }

    /// <summary>
    /// v2 browsers authenticate with a session cookie, so the fixture issues a
    /// real session row rather than forging a token. Same code path the app
    /// uses, minus the login form.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(Guid userId, string email, string role = "user")
    {
        var token = CreateSessionAsync(userId).GetAwaiter().GetResult();
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{SessionCookieName}={token}");
        return client;
    }

    public const string SessionCookieName = "mizan_session";

    public RecordingEmailSender Email { get; } = new();

    public ScriptedAiProvider Ai { get; } = new();

    public async Task<string> CreateSessionAsync(Guid userId, DateTime? expiresAt = null)
    {
        var token = SecureToken.Generate();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var now = DateTime.UtcNow;
        db.UserSessions.Add(new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = SecureToken.Hash(token),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = expiresAt ?? now.AddDays(7),
        });
        await db.SaveChangesAsync();
        return token;
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        if (db.Database.IsInMemory())
        {
            // For InMemory database, delete all entities manually
            // Since InMemory doesn't support ExecuteSqlRaw
            db.ChatMessages.RemoveRange(db.ChatMessages);
            db.ChatConversations.RemoveRange(db.ChatConversations);
            db.TrainerClientRelationships.RemoveRange(db.TrainerClientRelationships);
            db.McpUsageLogs.RemoveRange(db.McpUsageLogs);
            db.McpTokens.RemoveRange(db.McpTokens);
            db.GoalProgress.RemoveRange(db.GoalProgress);
            db.UserGoals.RemoveRange(db.UserGoals);
            db.FoodDiaryEntries.RemoveRange(db.FoodDiaryEntries);
            db.FavoriteRecipes.RemoveRange(db.FavoriteRecipes);
            db.RecipeIngredients.RemoveRange(db.RecipeIngredients);
            db.Recipes.RemoveRange(db.Recipes);
            db.Foods.RemoveRange(db.Foods);
            db.HouseholdMembers.RemoveRange(db.HouseholdMembers);
            db.Households.RemoveRange(db.Households);
            db.Subscriptions.RemoveRange(db.Subscriptions);
            db.AuditLogs.RemoveRange(db.AuditLogs);
            db.Users.RemoveRange(db.Users);
            await db.SaveChangesAsync();
        }
        else
        {
            // TRUNCATE is faster than deleting and recreating for real databases
            var tableList = string.Join(", ", TablesToTruncate.Select(t => $"\"{t}\""));
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {tableList} RESTART IDENTITY CASCADE;");
#pragma warning restore EF1002
        }
    }

    public async Task<User> SeedUserAsync(Guid id, string email, bool emailVerified = true, string role = "user", bool banned = false, DateTime? banExpires = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = id,
            Email = email,
            EmailVerified = emailVerified,
            Name = "Test User",
            ThemePreference = "system",
            CompactMode = false,
            ReduceAnimations = false,
            Role = role,
            Banned = banned,
            BanExpires = banExpires,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // Entitlement is resolved from the subscriptions table (see EntitlementService),
    // not a user flag, so tests hitting Pro-gated endpoints need a row here.
    public async Task GrantProAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var now = DateTime.UtcNow;
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Plan = "pro",
            Status = "active",
            IsLifetime = false,
            CurrentPeriodEnd = now.AddDays(30),
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    public async Task<Recipe> SeedRecipeAsync(Guid userId, string title, string description, int servings, int prepTimeMinutes, bool isPublic = false)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var now = DateTime.UtcNow;
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Description = description,
            Servings = servings,
            PrepTimeMinutes = prepTimeMinutes,
            CookTimeMinutes = 15,
            IsPublic = isPublic,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe;
    }

    public async Task<List<Recipe>> GetRecipesByUserId(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.Recipes.Where(r => r.UserId == userId).ToListAsync();
    }

    public async Task<List<Food>> GetFoodsByUserId(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.Foods.Where(f => f.UserId == userId).ToListAsync();
    }

    /// <summary>Foods with no owner - the shared catalogue.</summary>
    public async Task<List<Food>> GetPublicFoodsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.Foods.Where(f => f.UserId == null).ToListAsync();
    }

    public async Task<List<FoodDiaryEntry>> GetFoodDiaryEntriesByUserId(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.FoodDiaryEntries.Where(e => e.UserId == userId).ToListAsync();
    }

    public async Task<Guid> SeedShoppingListAsync(Guid userId, string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    public async Task<Food> SeedFoodAsync(string name, decimal caloriesPer100g, decimal proteinPer100g, decimal carbsPer100g, decimal fatPer100g)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var now = DateTime.UtcNow;
        var food = new Food
        {
            Id = Guid.NewGuid(),
            Name = name,
            CaloriesPer100g = caloriesPer100g,
            ProteinPer100g = proteinPer100g,
            CarbsPer100g = carbsPer100g,
            FatPer100g = fatPer100g,
            ServingSize = 100,
            ServingUnit = "g",
            IsVerified = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Foods.Add(food);
        await db.SaveChangesAsync();
        return food;
    }

    public async Task<McpUsageLog> SeedMcpUsageLogAsync(Guid tokenId, Guid userId, string toolName, bool success, int executionTimeMs)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var log = new McpUsageLog
        {
            Id = Guid.NewGuid(),
            McpTokenId = tokenId,
            UserId = userId,
            ToolName = toolName,
            Parameters = "{}",
            Success = success,
            ExecutionTimeMs = executionTimeMs,
            Timestamp = DateTime.UtcNow
        };

        db.McpUsageLogs.Add(log);
        await db.SaveChangesAsync();
        return log;
    }

    public async Task<List<McpUsageLog>> GetMcpUsageLogsByUserId(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.McpUsageLogs.Where(l => l.UserId == userId).ToListAsync();
    }

    private async Task EnsureDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        // Skip migrations for InMemory database
        if (db.Database.IsInMemory())
        {
            // InMemory database doesn't support migrations, just ensure it's created
            await db.Database.EnsureCreatedAsync();
            return;
        }

        // EF Core owns every table now, users included.
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ApiTestFixture] MigrateAsync failed: {ex.Message}");
            throw; // Fail fast if migrations fail
        }
    }

    // Helper to get environment variable or throw if missing (for legacy tests)
    internal static string GetRequiredEnvironment(string name)
    {
        // For Testcontainers, we don't rely on env vars for connection strings anymore
        if (name == "ConnectionStrings__PostgreSQL") return "ignored";

        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            // Default fallbacks for tests if env not set
            if (name == "Jwt__Issuer") return "http://localhost:3000";
            if (name == "Jwt__Audience") return "mizan-api";
            return string.Empty;
        }

        return value;
    }
}

/// <summary>
/// A provider that answers with whatever the test told it to, and records
/// what it was asked. Every message it receives is available, so a test can
/// assert on what actually reached the model rather than on what was meant to.
/// </summary>
public sealed class ScriptedAiProvider : IAiProvider
{
    private readonly List<AiCompletionRequest> _calls = new();
    private readonly Queue<Func<AiCompletionRequest, AiCompletionResponse>> _scripted = new();

    public string Model => "test-model";

    public bool IsConfigured { get; set; } = true;

    public IReadOnlyList<AiCompletionRequest> Calls
    {
        get { lock (_calls) return _calls.ToList(); }
    }

    public AiCompletionRequest LastCall
    {
        get { lock (_calls) return _calls[^1]; }
    }

    public void Reset()
    {
        lock (_calls)
        {
            _calls.Clear();
            _scripted.Clear();
            IsConfigured = true;
        }
    }

    /// <summary>The next call answers with this. Unscripted calls echo a default.</summary>
    public void Reply(string content)
    {
        lock (_calls) _scripted.Enqueue(_ => new AiCompletionResponse(content, new AiTokenUsage(40, 12), Model));
    }

    public void Fail(string message)
    {
        lock (_calls) _scripted.Enqueue(_ => throw new AiUnavailableException(message));
    }

    public Task<AiCompletionResponse> CompleteAsync(
        AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new AiUnavailableException("The assistant is not configured on this server.");
        }

        Func<AiCompletionRequest, AiCompletionResponse> answer;
        lock (_calls)
        {
            _calls.Add(request);
            answer = _scripted.Count > 0
                ? _scripted.Dequeue()
                : _ => new AiCompletionResponse("Noted.", new AiTokenUsage(40, 12), Model);
        }

        return Task.FromResult(answer(request));
    }
}

/// <summary>
/// Captures what identity would have mailed, so a test can follow the same
/// link a user would click.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_sent) return _sent.ToList(); }
    }

    public void Clear()
    {
        lock (_sent) _sent.Clear();
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        lock (_sent) _sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>The token from the most recent link mailed to this address.</summary>
    public string? LastTokenFor(string email, string pathSegment)
    {
        var message = Sent.LastOrDefault(m =>
            string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)
            && m.Text.Contains(pathSegment, StringComparison.Ordinal));
        if (message is null) return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            message.Text, pathSegment + @"\?token=([A-Za-z0-9_\-%]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }
}
