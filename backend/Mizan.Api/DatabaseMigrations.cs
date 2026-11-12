using Microsoft.EntityFrameworkCore;
using Mizan.Application.Prices;
using Mizan.Infrastructure.Data;
using Serilog;

namespace Mizan.Api;

/// <summary>
/// How the EF migration chain reaches a database - docs/ARCHITECTURE.md#data-and-migrations.
/// </summary>
public static class DatabaseMigrations
{
    /// <summary>
    /// Applies pending migrations, then gives a catalogue with no shared food the starter foods.
    /// Returns a process exit code: 0 when the schema is current, 1 when a migration failed. EF Core
    /// takes a database lock while it migrates, so two runs cannot interleave.
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
            }
            else
            {
                Log.Information("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
                await db.Database.MigrateAsync();
                Log.Information("Database migrations applied successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to apply database migrations");
            return 1;
        }

        // A catalogue with no shared food leaves logging and planning with nothing to offer (issue #122). It is not
        // a schema step, so a failure is logged and the deploy goes on; an administrator can import from the prices page.
        try
        {
            var seeded = await scope.ServiceProvider.GetRequiredService<StarterFoodImport>().ImportIfEmptyAsync(CancellationToken.None);
            if (seeded is not null) Log.Information("The catalogue was empty; imported {Count} starter foods", seeded.Created);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not import the starter foods into an empty catalogue");
        }

        return 0;
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
