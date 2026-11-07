using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// Notifies every administrator of a provider refusal that retrying does not fix. It writes in
/// its own scope, so the notification is saved whatever the failed request does with its own
/// changes, and a failure here never hides the provider's error from the caller.
/// </summary>
public sealed class AiProviderIncidents : IAiProviderIncidents
{
    public const string NotificationType = "ai_misconfigured";
    public const string LinkUrl = "/admin/settings#ai";
    public static readonly TimeSpan Quiet = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AiProviderIncidents> _logger;

    public AiProviderIncidents(IServiceScopeFactory scopes, ILogger<AiProviderIncidents> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task ReportAsync(AiProviderRejectedException refusal, CancellationToken cancellationToken = default)
    {
        if (!refusal.IsConfigurationError) return;
        var body = Body(refusal);
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IMizanDbContext>();
            var since = DateTime.UtcNow - Quiet;
            if (await context.Notifications.AsNoTracking().AnyAsync(n => n.Type == NotificationType && n.Body == body && n.CreatedAt >= since, cancellationToken))
                return;

            var admins = await context.Users.AsNoTracking()
                .Where(u => u.Role == "admin" && !u.Banned)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            if (admins.Count == 0) return;

            var notifications = scope.ServiceProvider.GetRequiredService<INotificationWriter>();
            foreach (var admin in admins)
            {
                await notifications.AddAsync(admin, NotificationType, "The AI provider refused a request", body, LinkUrl, cancellationToken);
            }
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not notify administrators of an AI provider refusal");
        }
    }

    /// <summary>The status and the provider's own message, already cut short and free of secrets.</summary>
    public static string Body(AiProviderRejectedException refusal)
    {
        var message = refusal.ProviderMessage ?? "The provider gave no reason.";
        var detail = refusal.ProviderParam is { } param ? $" (param {param})" : string.Empty;
        return $"{refusal.Status}{detail}: {message}";
    }
}
