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
        services.AddScoped<Planning.PlanCostTable>();
        services.AddScoped<Planning.MealPlanContextBuilder>();
        services.AddScoped<Planning.ShoppingListAccess>();
        services.AddScoped<Prices.PriceBookReader>();

        return services;
    }
}
