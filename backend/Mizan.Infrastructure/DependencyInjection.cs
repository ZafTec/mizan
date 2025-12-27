using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.AI;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Services;

namespace Mizan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<MizanDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("PostgreSQL"),
                b => b.MigrationsAssembly(typeof(MizanDbContext).Assembly.FullName)));

        services.AddScoped<IMizanDbContext>(provider => provider.GetRequiredService<MizanDbContext>());

        // Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<INutritionAiService, NutritionAiService>();
        services.AddSingleton<IRedisCacheService, RedisCacheService>();

        return services;
    }
}
