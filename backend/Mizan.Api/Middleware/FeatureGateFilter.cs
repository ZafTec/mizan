using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Mizan.Application.Features;
using Mizan.Contracts.Telegram;
using Mizan.Domain.Features;

namespace Mizan.Api.Middleware;

/// <summary>
/// The API half of the feature gate (docs/ADMIN.md#feature-flags). An action
/// marked <see cref="RequiresFeatureAttribute"/> is checked here, for the
/// endpoints that do their work outside a MediatR request; a write the Telegram
/// bot makes as a linked user is held to <see cref="FeatureFlags.TelegramBot"/>.
/// Both ask <see cref="FeatureGate"/>, the same check the MediatR behavior makes.
/// </summary>
public sealed class FeatureGateFilter : IAsyncActionFilter
{
    private readonly FeatureGate _gate;

    public FeatureGateFilter(FeatureGate gate) => _gate = gate;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cancellation = context.HttpContext.RequestAborted;

        if (context.ActionDescriptor is ControllerActionDescriptor action)
        {
            var keys = action.MethodInfo.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true)
                .Concat(action.ControllerTypeInfo.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true))
                .Cast<RequiresFeatureAttribute>()
                .Select(a => a.Key)
                .Distinct();
            foreach (var key in keys) await _gate.EnsureEnabledAsync(key, cancellation);
        }

        var request = context.HttpContext.Request;
        if (IsWrite(request.Method)
            && string.Equals(request.Headers[TelegramSurface.Header], TelegramSurface.Value, StringComparison.OrdinalIgnoreCase))
        {
            await _gate.EnsureEnabledAsync(FeatureFlags.TelegramBot, cancellation);
        }

        await next();
    }

    private static bool IsWrite(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
}
