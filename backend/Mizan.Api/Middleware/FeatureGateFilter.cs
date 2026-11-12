using System.Runtime.CompilerServices;
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
            foreach (var key in KeysOf(action)) await _gate.EnsureEnabledAsync(key, cancellation);
        }

        var request = context.HttpContext.Request;
        if (IsWrite(request.Method)
            && string.Equals(request.Headers[TelegramSurface.Header], TelegramSurface.Value, StringComparison.OrdinalIgnoreCase))
        {
            await _gate.EnsureEnabledAsync(FeatureFlags.TelegramBot, cancellation);
        }

        await next();
    }

    // An action's attributes never change, so they are read once per action rather than on every request.
    private static readonly ConditionalWeakTable<ControllerActionDescriptor, string[]> Keys = new();

    private static string[] KeysOf(ControllerActionDescriptor action) =>
        Keys.GetValue(action, static a => a.MethodInfo.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true)
            .Concat(a.ControllerTypeInfo.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true))
            .Cast<RequiresFeatureAttribute>()
            .Select(attribute => attribute.Key)
            .Distinct()
            .ToArray());

    private static bool IsWrite(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
}
