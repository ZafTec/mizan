using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Identity;

namespace Mizan.Api.Authentication;

/// <summary>
/// Feeds the Google and GitHub handlers the client ids and secrets an administrator stored in the runtime
/// settings (docs/ADMIN.md#runtime-settings). Both schemes are always registered. The handlers build their
/// options synchronously and cache them per scheme, so <see cref="RefreshAsync"/> runs ahead of every sign-in
/// request (the challenge and the provider's callback): it reads the current settings and, when they changed,
/// drops the cached options so the handler builds them again from <see cref="Current"/>. A change applies to the
/// next sign-in without a restart.
/// </summary>
public sealed class ExternalSignInSettings
{
    /// <summary>
    /// What the handler gets for a provider that is off. Its options must validate, and nothing reaches the
    /// provider with it: the challenge is refused and the callback answers 404 first.
    /// </summary>
    internal const string Unconfigured = "unconfigured";

    private readonly IRuntimeOptions<ExternalSignInOptions> _options;
    private readonly IServiceProvider _services;
    private ExternalSignInOptions _current = new();

    // The handlers' options are configured from this class, so the option monitors are resolved when used
    // rather than injected: injecting them would make the options depend on themselves.
    public ExternalSignInSettings(IRuntimeOptions<ExternalSignInOptions> options, IServiceProvider services)
    {
        _options = options;
        _services = services;
    }

    /// <summary>The settings the handlers build their options from.</summary>
    public ExternalSignInOptions Current => Volatile.Read(ref _current);

    public async ValueTask<ExternalSignInOptions> RefreshAsync(CancellationToken cancellationToken)
    {
        var latest = await _options.GetAsync(cancellationToken);
        Volatile.Write(ref _current, latest);

        // Compared with what the handler actually holds rather than with the last value seen, so options a
        // concurrent request built from older settings are replaced too.
        Rebuild<GoogleOptions>(GoogleDefaults.AuthenticationScheme, latest.Google);
        Rebuild<GitHubAuthenticationOptions>(GitHubAuthenticationDefaults.AuthenticationScheme, latest.GitHub);
        return latest;
    }

    /// <summary>The provider's settings by its route name (<c>google</c>, <c>github</c>), or null for another name.</summary>
    public static ExternalSignInProvider? For(ExternalSignInOptions options, string provider) =>
        provider.ToLowerInvariant() switch
        {
            "google" => options.Google,
            "github" => options.GitHub,
            _ => null,
        };

    internal void Apply(OAuthOptions options, Func<ExternalSignInOptions, ExternalSignInProvider> provider)
    {
        var settings = provider(Current);
        options.ClientId = settings.IsConfigured ? settings.ClientId : Unconfigured;
        options.ClientSecret = settings.IsConfigured ? settings.ClientSecret : Unconfigured;
    }

    private void Rebuild<TOptions>(string scheme, ExternalSignInProvider expected)
        where TOptions : OAuthOptions
    {
        var held = _services.GetRequiredService<IOptionsMonitor<TOptions>>().Get(scheme);
        var id = expected.IsConfigured ? expected.ClientId : Unconfigured;
        var secret = expected.IsConfigured ? expected.ClientSecret : Unconfigured;
        if (string.Equals(held.ClientId, id, StringComparison.Ordinal)
            && string.Equals(held.ClientSecret, secret, StringComparison.Ordinal))
        {
            return;
        }

        _services.GetRequiredService<IOptionsMonitorCache<TOptions>>().TryRemove(scheme);
    }
}

/// <summary>
/// Runs <see cref="ExternalSignInSettings.RefreshAsync"/> ahead of authentication for the sign-in routes, and
/// answers a provider callback for a provider that is off with 404 rather than an exchange with no credentials.
/// </summary>
public sealed class ExternalSignInRefreshMiddleware
{
    private readonly RequestDelegate _next;

    public ExternalSignInRefreshMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ExternalSignInSettings settings)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/Auth/external", StringComparison.OrdinalIgnoreCase))
        {
            await settings.RefreshAsync(context.RequestAborted);
        }
        else if (path.StartsWithSegments(ExternalProviders.ProviderCallbackPrefix, StringComparison.OrdinalIgnoreCase, out var rest))
        {
            var current = await settings.RefreshAsync(context.RequestAborted);
            var provider = ExternalSignInSettings.For(current, rest.Value?.Trim('/') ?? string.Empty);
            if (provider is { IsConfigured: false })
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        await _next(context);
    }
}
