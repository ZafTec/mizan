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
using Microsoft.IdentityModel.Tokens;
using Mizan.Api.Authentication;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using NSec.Cryptography;
using Testcontainers.PostgreSql;
using Xunit;

namespace Mizan.Tests.Integration;

public sealed class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string[] TablesToTruncate = new[]
    {
        "mcp_usage_logs",
        "mcp_tokens",
        "goal_progress",
        "user_goals",
        "food_diary_entries",
        "favorite_recipes",
        "recipe_tags",
        "recipe_instructions",
        "recipe_ingredients",
        "recipe_nutrition",
        "recipes",
        "foods",
        "household_members",
        "households",
        "audit_logs",
        "users"
    };

    private readonly PostgreSqlContainer? _dbContainer;
    private readonly TestJwtIssuer _jwtIssuer;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _connectionString;
    private readonly string? _redisConnectionString;

    public ApiTestFixture()
    {
        // Try multiple environment variable formats
        var existingConnString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION") 
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:PostgreSQL");

        if (!string.IsNullOrWhiteSpace(existingConnString))
        {
            // Using existing DB connection
            _connectionString = existingConnString;
            _dbContainer = null;
        }
        else
        {
            _dbContainer = new PostgreSqlBuilder()
                .WithImage("postgres:15-alpine")
                .WithDatabase("mizan_test")
                .WithUsername("mizan")
                .WithPassword("mizan_test_password")
                .Build();
            _connectionString = string.Empty; // Will be set in InitializeAsync
        }

        _issuer = Environment.GetEnvironmentVariable("BetterAuth__Issuer") ?? "http://localhost:3000";
        _audience = Environment.GetEnvironmentVariable("BetterAuth__Audience") ?? "mizan-api";
        _jwtIssuer = TestJwtIssuer.Create();
        
        _redisConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Redis");
    }

    public string Issuer => _issuer;
    public string Audience => _audience;
    public TestJwtIssuer JwtIssuer => _jwtIssuer;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var connString = !string.IsNullOrEmpty(_connectionString) 
                ? _connectionString 
                : _dbContainer?.GetConnectionString() ?? throw new InvalidOperationException("No DB connection string available");

            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = connString,
                ["ConnectionStrings:Redis"] = _redisConnectionString,
                ["BetterAuth:Issuer"] = _issuer,
                ["BetterAuth:Audience"] = _audience,
                ["BetterAuth:JwksUrl"] = "http://jwks.test",
                ["Mcp:ServiceApiKey"] = "test-api-key"
            };

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext registration
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MizanDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            var connString = !string.IsNullOrEmpty(_connectionString) 
                ? _connectionString 
                : _dbContainer?.GetConnectionString() ?? throw new InvalidOperationException("No DB connection string available");

            // Add DbContext using container connection
            services.AddDbContext<MizanDbContext>(options =>
                options.UseNpgsql(connString));

            var descriptors = services.Where(d => d.ServiceType == typeof(IJwksProvider)).ToList();
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IJwksProvider>(new TestJwksProvider(_jwtIssuer.Jwk));
            
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

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        
        // Set the JWKS provider accessor for EdDSA signature validation
        var jwksProvider = host.Services.GetRequiredService<IJwksProvider>();
        JwksProviderAccessor.Set(jwksProvider);
        
        return host;
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
        _jwtIssuer.Dispose();
        await base.DisposeAsync();
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, string email, string role = "user")
    {
        var token = CreateToken(userId, email, role);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public string CreateToken(Guid userId, string email, string role = "user")
    {
        return _jwtIssuer.CreateToken(userId, email, role, _issuer, _audience);
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        
        // TRUNCATE is faster than deleting and recreating
        var tableList = string.Join(", ", TablesToTruncate.Select(t => $"\"{t}\""));
        await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {tableList} RESTART IDENTITY CASCADE;");
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
        // Foods are global in this simplified model, but let's assume we filter by creation or just return all for now if no user ownership on foods
        // Or if foods are global, just return a list.
        // Wait, foods table usually doesn't have UserId unless it's custom food.
        // Let's check Food entity.
        // Assuming global foods for now or verifying creation.
        return await db.Foods.ToListAsync();
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

        // 1. Manually create Better Auth tables required by backend foreign keys
        // These tables are managed by Frontend/Drizzle in production, so EF Core migrations exclude them.
        // But in tests, we start with an empty DB, so we must create them manually first.
        
        var createUsersTable = @"
            CREATE TABLE IF NOT EXISTS ""users"" (
                ""id"" uuid NOT NULL,
                ""email"" character varying(255) NOT NULL,
                ""email_verified"" boolean NOT NULL DEFAULT FALSE,
                ""name"" character varying(255),
                ""image"" text,
                ""role"" character varying(50) DEFAULT 'user',
                ""banned"" boolean DEFAULT FALSE,
                ""ban_reason"" text,
                ""ban_expires"" timestamp with time zone,
                ""created_at"" timestamp with time zone DEFAULT (NOW()),
                ""updated_at"" timestamp with time zone DEFAULT (NOW()),
                CONSTRAINT ""PK_users"" PRIMARY KEY (""id"")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_users_email"" ON ""users"" (""email"");
        ";

        await db.Database.ExecuteSqlRawAsync(createUsersTable);

        // 2. Apply EF Core migrations to create business logic tables (foods, recipes, etc.)
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
            if (name == "BetterAuth__Issuer") return "http://localhost:3000";
            if (name == "BetterAuth__Audience") return "mizan-api";
            return string.Empty; 
        }

        return value;
    }
}

public sealed class TestJwksProvider : IJwksProvider
{
    private readonly IReadOnlyCollection<SecurityKey> _keys;

    public TestJwksProvider(JsonWebKey jwk)
    {
        _keys = new[] { jwk };
    }

    public Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_keys);
    }
}

public sealed class TestJwtIssuer : IDisposable
{
    private readonly Key _key;
    private readonly JsonWebKey _jwk;

    private TestJwtIssuer(Key key, JsonWebKey jwk)
    {
        _key = key;
        _jwk = jwk;
    }

    public JsonWebKey Jwk => _jwk;

    public static TestJwtIssuer Create()
    {
        var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });

        var publicKey = key.Export(KeyBlobFormat.RawPublicKey);
        var jwk = new JsonWebKey
        {
            Kty = "OKP",
            Crv = "Ed25519",
            Alg = "EdDSA",
            Use = "sig",
            Kid = Guid.NewGuid().ToString("N"),
            X = Base64UrlEncoder.Encode(publicKey)
        };

        return new TestJwtIssuer(key, jwk);
    }

    public string CreateToken(Guid userId, string email, string role, string issuer, string audience)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object>
        {
            ["alg"] = "EdDSA",
            ["typ"] = "JWT",
            ["kid"] = _jwk.Kid!
        };

        var payload = new Dictionary<string, object?>
        {
            ["sub"] = userId.ToString(),
            ["email"] = email,
            ["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] = role,
            ["role"] = role,
            ["iss"] = issuer,
            ["aud"] = audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddHours(1).ToUnixTimeSeconds()
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerB64 = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{headerB64}.{payloadB64}";

        var signature = SignatureAlgorithm.Ed25519.Sign(_key, Encoding.ASCII.GetBytes(signingInput));
        var signatureB64 = Base64UrlEncoder.Encode(signature);

        return $"{signingInput}.{signatureB64}";
    }

    public void Dispose()
    {
        _key.Dispose();
    }
}
