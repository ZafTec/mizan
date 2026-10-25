using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.AI;
using Mizan.Infrastructure.Ai;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Email;
using Mizan.Infrastructure.Identity;
using Mizan.Infrastructure.Outbox;
using Mizan.Infrastructure.Push;
using Mizan.Infrastructure.Services;
using Mizan.Infrastructure.Settings;
using Mizan.Infrastructure.Storage;
using Mizan.Infrastructure.Telegram;

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

        // Runtime settings: values an administrator sets in the admin area, laid over configuration
        // and read per use (docs/ADMIN.md#runtime-settings). Their secrets are protected with Data
        // Protection, whose key ring lives in the database so it survives restarts and every API
        // instance shares it. DataProtection__KeysPath moves the ring to a directory instead (a volume
        // every instance mounts), which keeps it out of database backups.
        var dataProtection = services.AddDataProtection().SetApplicationName("Mizan");
        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToDbContext<MizanDbContext>();
        }
        else
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddSingleton<RuntimeSettingsCache>();
        services.AddScoped<IRuntimeSettings, RuntimeSettings>();
        AddRuntimeOptions<SmtpOptions>(services, SmtpOptions.SectionName);
        AddRuntimeOptions<AiOptions>(services, AiOptions.SectionName);
        AddRuntimeOptions<TelegramOptions>(services, TelegramOptions.SectionName);
        AddRuntimeOptions<PushOptions>(services, PushOptions.SectionName);
        AddRuntimeOptions<PaddleOptions>(services, PaddleOptions.SectionName);

        // Domain services
        services.TryAddSingleton(TimeProvider.System);
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

        // Identity - the backend owns auth end to end since v2 (docs/ARCHITECTURE.md#identity)
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.AddSingleton<ITelegramSettings, TelegramSettings>();
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<IAppUrls, AppUrls>();
        services.AddScoped<ISessionService, SessionService>();

        // OAuth authorization server for MCP clients and our own apps (docs/MCP.md#connect-with-oauth).
        services.Configure<AuthorizationServerOptions>(configuration.GetSection(AuthorizationServerOptions.SectionName));
        services.AddSingleton<IOAuthSettings, OAuthSettings>();
        services.AddHttpClient(OAuthClientMetadataFetcher.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(4),
                UseProxy = false,
                ConnectCallback = OAuthClientMetadataFetcher.GuardedConnectAsync,
            });
        services.AddScoped<IOAuthClientMetadataFetcher, OAuthClientMetadataFetcher>();
        services.AddScoped<IUserCacheInvalidator, UserCacheInvalidator>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Object storage - one S3 client covers MinIO and Cloudflare R2
        // (docs/ARCHITECTURE.md#storage-caching-and-jobs). Unconfigured is a supported state: the API
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
        // (docs/AI.md).
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.AddHttpClient(OpenAiCompatibleProvider.HttpClientName);
        services.AddSingleton<IAiProvider, OpenAiCompatibleProvider>();
        services.AddSingleton<IAiCeilings, AiCeilings>();
        services.AddSingleton<IAiTierDefaults, AiTierDefaults>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddScoped<IAiMeteredClient, MeteredAiClient>();
        services.AddScoped<IDataAccessPolicy, DataAccessPolicy>();
        services.AddScoped<IAiContextBuilder, AiContextBuilder>();
        services.AddScoped<IAiPromptResolver, AiPromptResolver>();
        services.AddScoped<IAiEvalRunner, AiEvalRunner>();
        services.AddScoped<IAiToolRunner, AiToolRunner>();

        // Outbox: reliable work that must not run inside the request that
        // asked for it (docs/ARCHITECTURE.md#storage-caching-and-jobs).
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddScoped<IOutbox, Outbox.Outbox>();
        services.AddScoped<IOutboxHandler, EmailJobHandler>();
        services.AddScoped<IOutboxHandler, EvalRunJobHandler>();
        services.AddScoped<IOutboxHandler, PushJobHandler>();
        services.AddScoped<IOutboxHandler, EveningReminderJobHandler>();
        services.AddScoped<IOutboxHandler, TelegramMessageJobHandler>();

        // Messages to a linked Telegram chat go through the bot service, which alone holds the
        // token (docs/TELEGRAM.md#evening-reminder). Off until Telegram:BotServiceUrl is set.
        services.AddHttpClient(TelegramBotNotifier.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddSingleton<ITelegramBotNotifier, TelegramBotNotifier>();
        services.AddScoped<ITelegramMessenger, TelegramMessenger>();

        // Push to phones. Off until Firebase credentials are configured (docs/ARCHITECTURE.md#native-clients).
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));
        services.AddHttpClient(FcmPushSender.HttpClientName);
        services.AddSingleton<IPushSender, FcmPushSender>();

        // The system health page's round trips (docs/ADMIN.md#overview-and-health).
        services.AddHttpClient(SystemProbes.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(3));
        services.AddScoped<ISystemProbes, SystemProbes>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<EveningReminderScheduler>();

        // Billing
        services.Configure<PaddleOptions>(configuration.GetSection(PaddleOptions.SectionName));
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddHttpClient(Billing.PaddleApiClient.HttpClientName);
        services.AddScoped<IPaddleApiClient, Billing.PaddleApiClient>();

        // The prices worker's Run now endpoint (docs/PLANNING.md#prices).
        services.AddHttpClient(PricesWorkerClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IPricesWorkerClient, PricesWorkerClient>();

        return services;
    }

    /// <summary>
    /// One overlay per options type, registered as a singleton under both its interfaces, so the
    /// settings list and every consumer read the same instance.
    /// </summary>
    private static void AddRuntimeOptions<TOptions>(IServiceCollection services, string section)
        where TOptions : class
    {
        services.AddSingleton(sp => new RuntimeOptions<TOptions>(
            section,
            sp.GetRequiredService<IOptionsMonitor<TOptions>>(),
            sp.GetRequiredService<RuntimeSettingsCache>(),
            sp.GetRequiredService<ILogger<RuntimeOptions<TOptions>>>()));
        services.AddSingleton<IRuntimeOptions<TOptions>>(sp => sp.GetRequiredService<RuntimeOptions<TOptions>>());
        services.AddSingleton<IRuntimeOptionsSection>(sp => sp.GetRequiredService<RuntimeOptions<TOptions>>());
    }
}
