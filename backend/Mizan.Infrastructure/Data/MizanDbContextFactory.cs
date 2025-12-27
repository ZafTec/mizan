using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Mizan.Infrastructure.Data;

public class MizanDbContextFactory : IDesignTimeDbContextFactory<MizanDbContext>
{
    public MizanDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MizanDbContext>();

        // Try to read connection string from environment variable first
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL");

        // If not found, use default development connection string
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "Host=localhost;Database=mizan;Username=mizan;Password=mizan_dev_password";
        }

        optionsBuilder.UseNpgsql(connectionString, b =>
        {
            b.MigrationsAssembly("Mizan.Infrastructure");
        });

        return new MizanDbContext(optionsBuilder.Options);
    }
}
