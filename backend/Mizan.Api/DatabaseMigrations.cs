using Microsoft.EntityFrameworkCore;
using Mizan.Infrastructure.Data;
using Serilog;

namespace Mizan.Api;

/// <summary>
/// How the EF migration chain reaches a database - docs/ARCHITECTURE.md#data-and-migrations.
/// </summary>
public static class DatabaseMigrations
{
    /// <summary>
    /// Applies pending migrations. Returns a process exit code: 0 when the
    /// schema is current, 1 when a migration failed. EF Core takes a database
    /// lock while it migrates, so two runs cannot interleave.
    /// </summary>
    public static async Task<int> ApplyAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count == 0)
            {
                Log.Information("Database schema is current; no migrations to apply");
                return 0;
            }

            Log.Information("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
            Log.Information("Database migrations applied successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to apply database migrations");
            return 1;
        }
    }

    /// <summary>Throws when the database is behind this build, so the API does not start on the wrong schema.</summary>
    public static async Task RequireCurrentAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            var message = $"The database is missing {pending.Count} migration(s): {string.Join(", ", pending)}. "
                + "Run this image with --migrate first; the production Compose file's mizan-db-migrate step does.";
            Log.Fatal(message);
            throw new InvalidOperationException(message);
        }
    }
}
