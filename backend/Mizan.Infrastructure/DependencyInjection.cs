using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.AI;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Email;
using Mizan.Infrastructure.Identity;
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

        // Distributed cache (L2) wraps Redis. HybridCache will use this
        // automatically when registered, combining in-proc L1 + Redis L2
        // with stampede protection in one API.
        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(o => o.Configuration = redis);
        }

        services.AddHybridCache();

        // Domain services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITrainerAuthorizationService, TrainerAuthorizationService>();
        services.AddScoped<IUserStatusService, UserStatusService>();
        services.AddScoped<INutritionAiService, NutritionAiService>();
        services.AddScoped<IStreakService, StreakService>();
        services.AddScoped<IAchievementEvaluator, AchievementEvaluator>();
        services.AddScoped<INotificationWriter, NotificationWriter>();

        // Identity - the backend owns auth end to end since v2 (docs/REFOCUS.md §6)
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<IAppUrls, AppUrls>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IUserCacheInvalidator, UserCacheInvalidator>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Billing
        services.Configure<PaddleOptions>(configuration.GetSection(PaddleOptions.SectionName));
        services.AddScoped<IEntitlementService, EntitlementService>();

        return services;
    }
}
