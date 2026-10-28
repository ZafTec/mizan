using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Behaviors;

namespace Mizan.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(FeatureGateBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<Commands.SubscriberBilling>();
        services.AddScoped<OAuth.OAuthClientResolver>();
        services.AddScoped<Interfaces.IHouseholdAccess, Services.HouseholdAccess>();
        services.AddScoped<Planning.PlanAccess>();
        services.AddScoped<Planning.PlanEntryBuilder>();
        services.AddScoped<Planning.PlanReadModel>();
        services.AddScoped<Planning.PlanTargetsReader>();
        services.AddScoped<Planning.EveningReminders>();
        services.AddScoped<Planning.PlanCostTable>();
        services.AddScoped<Planning.MealPlanContextBuilder>();
        services.AddScoped<Planning.ShoppingListAccess>();
        services.AddScoped<Prices.PriceBookReader>();
        services.AddScoped<Interfaces.IEmailQueue, Mail.EmailQueue>();
        services.AddScoped<Mail.AnnouncementRecipients>();
        services.AddScoped<Features.ActiveHousehold>();
        services.AddScoped<Interfaces.IFeatureFlags, Features.FeatureFlagService>();
        services.AddScoped<Features.FeatureGate>();
        services.AddScoped<Usage.UsageLimitService>();
        services.AddScoped<Interfaces.IUsageLimits>(sp => sp.GetRequiredService<Usage.UsageLimitService>());
        services.AddScoped<Usage.UsageLimitSubjects>();

        return services;
    }
}
