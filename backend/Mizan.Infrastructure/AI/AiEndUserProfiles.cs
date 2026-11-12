using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// What the end-user token tells the gateway about a person, so its console can show who they are:
/// display attributes beside the opaque <c>sub</c>, never instead of it (docs/AI.md#behind-a-gateway).
/// </summary>
public interface IAiEndUserProfiles
{
    /// <summary>The attributes for one person, or none when they cannot be read.</summary>
    Task<IReadOnlyDictionary<string, string>> AttributesForAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Reads the name, email and plan; the gateway takes string values of at most 200 characters.</summary>
public sealed class AiEndUserProfiles(IServiceScopeFactory scopes) : IAiEndUserProfiles
{
    public const int MaxValueLength = 200;

    public async Task<IReadOnlyDictionary<string, string>> AttributesForAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IMizanDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Name, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null) return new Dictionary<string, string>();

        var plan = (await scope.ServiceProvider.GetRequiredService<IEntitlementService>().GetAsync(userId, cancellationToken)).Plan;
        var attributes = new Dictionary<string, string>();
        Add(attributes, "name", user.Name);
        Add(attributes, "email", user.Email);
        Add(attributes, "plan", plan);
        return attributes;
    }

    private static void Add(Dictionary<string, string> attributes, string key, string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value)) return;
        attributes[key] = value.Length > MaxValueLength ? value[..MaxValueLength] : value;
    }
}
