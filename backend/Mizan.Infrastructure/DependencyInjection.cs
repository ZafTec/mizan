using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.AI;
using Mizan.Infrastructure.Ai;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Email;
using Mizan.Infrastructure.Identity;
using Mizan.Infrastructure.Services;
using Mizan.Infrastructure.Storage;

namespace Mizan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        // Singleton, and stateless by design - see the note in the class.
        services.AddSingleton<ActivityCounterInterceptor>();
        services.AddDbContext<MizanDbContext>(options =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("PostgreSQL"),
                    b => b.MigrationsAssembly(typeof(MizanDbContext).Assembly.FullName))
                .AddInterceptors(new ActivityCounterInterceptor()));

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
        services.AddScoped<IUserClock, UserClock>();
        services.AddScoped<IActivityCounters, ActivityCounters>();
        services.AddScoped<IUserStatsProvider, UserStatsProvider>();
        services.AddScoped<IAchievementCatalogue, AchievementCatalogue>();
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

        // Object storage - one S3 client covers MinIO and Cloudflare R2
        // (docs/REFOCUS.md §7). Unconfigured is a supported state: the API
        // starts and only uploads refuse.
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        if (string.IsNullOrWhiteSpace(configuration[$"{StorageOptions.SectionName}:ServiceUrl"]))
        {
            services.AddSingleton<IStorageService, UnconfiguredStorageService>();
        }
        else
        {
            services.AddSingleton<IStorageService, S3StorageService>();
        }

        // AI platform. Quota and consent are registered with the provider, not
        // after it: an unmetered or unconsented call must not be constructible
        // (docs/REFOCUS.md §10).
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.AddHttpClient(OpenAiCompatibleProvider.HttpClientName);
        services.AddSingleton<IAiProvider, OpenAiCompatibleProvider>();
        services.AddSingleton<IAiCeilings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value);
        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddScoped<IDataAccessPolicy, DataAccessPolicy>();
        services.AddScoped<IAiContextBuilder, AiContextBuilder>();
        services.AddScoped<IAiPromptResolver, AiPromptResolver>();
        services.AddScoped<IAiEvalRunner, AiEvalRunner>();
        services.AddScoped<IAiToolRunner, AiToolRunner>();

        // Billing
        services.Configure<PaddleOptions>(configuration.GetSection(PaddleOptions.SectionName));
        services.AddScoped<IEntitlementService, EntitlementService>();

        return services;
    }
}
