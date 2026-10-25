using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Features;

namespace Mizan.Application.Behaviors;

/// <summary>
/// Refuses a request marked <see cref="RequiresFeatureAttribute"/> when its
/// feature is off for the caller's household. A request names its feature once,
/// and every adapter that sends it (controller, MCP, Telegram, an assistant
/// tool) is held to it without knowing the rule.
/// </summary>
public class FeatureGateBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // One lookup per closed request type.
    private static readonly string[] Keys = typeof(TRequest)
        .GetCustomAttributes<RequiresFeatureAttribute>(inherit: true)
        .Select(a => a.Key)
        .Distinct()
        .ToArray();

    private readonly IServiceProvider _services;

    public FeatureGateBehavior(IServiceProvider services) => _services = services;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (Keys.Length > 0)
        {
            var gate = _services.GetRequiredService<FeatureGate>();
            foreach (var key in Keys) await gate.EnsureEnabledAsync(key, cancellationToken);
        }

        return await next();
    }
}
