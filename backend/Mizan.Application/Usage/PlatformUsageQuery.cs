using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;

namespace Mizan.Application.Usage;

/// <summary>
/// The AI usage ledger between two UTC dates, inclusive, grouped by household,
/// user, plan or feature, optionally narrowed to one household or one user
/// (docs/ADMIN.md#usage). From defaults to the first of this month, To to today.
/// A plan is the user's current one: Free without a subscription, or "Pro
/// (other)" for a lifetime grant or a price outside the catalogue.
/// </summary>
public record GetPlatformUsageQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    string? GroupBy = null,
    Guid? HouseholdId = null,
    Guid? UserId = null) : IRequest<PlatformUsageDto>;

public static class PlatformUsageGroups
{
    public const string Household = "household";
    public const string User = "user";
    public const string Plan = "plan";
    public const string Feature = "feature";

    public static readonly IReadOnlyList<string> All = [Household, User, Plan, Feature];

    public static string Parse(string? groupBy)
    {
        if (string.IsNullOrWhiteSpace(groupBy)) return Household;
        return All.FirstOrDefault(g => string.Equals(g, groupBy, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainValidationException("Group by household, user, plan or feature.");
    }
}

public class GetPlatformUsageQueryHandler : IRequestHandler<GetPlatformUsageQuery, PlatformUsageDto>
{
    private const int MaxDays = 366;

    private readonly IMizanDbContext _context;
    private readonly TimeProvider _clock;

    public GetPlatformUsageQueryHandler(IMizanDbContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<PlatformUsageDto> Handle(GetPlatformUsageQuery request, CancellationToken cancellationToken)
    {
        var groupBy = PlatformUsageGroups.Parse(request.GroupBy);
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var to = request.To ?? today;
        var from = request.From ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to) throw new DomainValidationException("The start date is after the end date.");
        if (to.DayNumber - from.DayNumber >= MaxDays) throw new DomainValidationException($"Ask for at most {MaxDays} days at a time.");

        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var ledger = _context.AiUsageLogs.AsNoTracking().Where(l => l.CreatedAt >= start && l.CreatedAt < end);
        if (request.HouseholdId is { } household) ledger = ledger.Where(l => l.HouseholdId == household);
        if (request.UserId is { } user) ledger = ledger.Where(l => l.UserId == user);

        // Summed in the database by the finest key any grouping needs; plans are mapped from users after.
        var sums = await ledger
            .GroupBy(l => new { l.UserId, l.HouseholdId, l.Feature })
            .Select(g => new Sum(
                g.Key.UserId,
                g.Key.HouseholdId,
                g.Key.Feature,
                g.Count(l => l.Outcome != AiCallOutcome.Refused),
                g.Sum(l => (long)l.PromptTokens + l.CompletionTokens),
                g.Sum(l => l.EstimatedCostMicros),
                g.Count(l => l.Outcome == AiCallOutcome.Refused)))
            .ToListAsync(cancellationToken);

        var rows = groupBy switch
        {
            PlatformUsageGroups.User => await ByUserAsync(sums, cancellationToken),
            PlatformUsageGroups.Plan => await ByPlanAsync(sums, cancellationToken),
            PlatformUsageGroups.Feature => Group(sums, s => s.Feature, key => key ?? string.Empty),
            _ => await ByHouseholdAsync(sums, cancellationToken),
        };

        return new PlatformUsageDto
        {
            From = from,
            To = to,
            GroupBy = groupBy,
            Rows = rows
                .OrderByDescending(r => r.CostMicros)
                .ThenByDescending(r => r.Tokens)
                .ThenBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Total = new PlatformUsageRowDto
            {
                Key = null,
                Label = "Total",
                Requests = sums.Sum(s => s.Requests),
                Tokens = sums.Sum(s => s.Tokens),
                CostMicros = sums.Sum(s => s.CostMicros),
                Cost = sums.Sum(s => s.CostMicros) / 1_000_000m,
                Refusals = sums.Sum(s => s.Refusals),
            },
        };
    }

    private sealed record Sum(Guid UserId, Guid? HouseholdId, string Feature, int Requests, long Tokens, long CostMicros, int Refusals);

    private static List<PlatformUsageRowDto> Group(IEnumerable<Sum> sums, Func<Sum, string?> key, Func<string?, string> label) =>
        sums.GroupBy(key)
            .Select(g => new PlatformUsageRowDto
            {
                Key = g.Key,
                Label = label(g.Key),
                Requests = g.Sum(s => s.Requests),
                Tokens = g.Sum(s => s.Tokens),
                CostMicros = g.Sum(s => s.CostMicros),
                Cost = g.Sum(s => s.CostMicros) / 1_000_000m,
                Refusals = g.Sum(s => s.Refusals),
            })
            .ToList();

    private async Task<List<PlatformUsageRowDto>> ByHouseholdAsync(List<Sum> sums, CancellationToken ct)
    {
        var ids = sums.Where(s => s.HouseholdId != null).Select(s => s.HouseholdId!.Value).Distinct().ToList();
        var names = await _context.Households.AsNoTracking()
            .Where(h => ids.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id.ToString(), h => h.Name, ct);
        return Group(sums, s => s.HouseholdId?.ToString(), key => key is null
            ? "No household"
            : names.GetValueOrDefault(key) ?? "Deleted household");
    }

    private async Task<List<PlatformUsageRowDto>> ByUserAsync(List<Sum> sums, CancellationToken ct)
    {
        var ids = sums.Select(s => s.UserId).Distinct().ToList();
        var names = await _context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id.ToString(), u => u.Name ?? u.Email, ct);
        return Group(sums, s => s.UserId.ToString(), key => names.GetValueOrDefault(key!) ?? "Deleted account");
    }

    private async Task<List<PlatformUsageRowDto>> ByPlanAsync(List<Sum> sums, CancellationToken ct)
    {
        var ids = sums.Select(s => s.UserId).Distinct().ToList();
        var subscriptions = await _context.Subscriptions.AsNoTracking()
            .Where(s => ids.Contains(s.UserId))
            .Select(s => new { s.UserId, s.Status, s.IsLifetime, s.PaddlePriceId, s.CurrentPeriodEnd })
            .ToListAsync(ct);
        var plans = await _context.BillingPlans.AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.PaddlePriceId })
            .ToListAsync(ct);

        const string Free = "free";
        const string OtherPro = "pro";
        var planOf = new Dictionary<Guid, string>();
        foreach (var subscription in subscriptions)
        {
            var paying = subscription.IsLifetime
                || subscription.Status is "active" or "trialing" or "past_due"
                || (subscription.Status == "canceled" && subscription.CurrentPeriodEnd > DateTime.UtcNow);
            if (!paying) continue;
            var plan = plans.FirstOrDefault(p => p.PaddlePriceId == subscription.PaddlePriceId);
            planOf[subscription.UserId] = plan?.Id.ToString() ?? OtherPro;
        }

        var labels = plans.ToDictionary(p => p.Id.ToString(), p => p.Name);
        return Group(
            sums,
            s => planOf.GetValueOrDefault(s.UserId) ?? Free,
            key => key switch
            {
                Free => "Free",
                OtherPro => "Pro (other)",
                _ => labels.GetValueOrDefault(key!) ?? key!,
            });
    }
}

/// <summary>
/// The usage table as RFC 4180 CSV: a header, then one line per group, CRLF
/// line ends, a field quoted when it holds a comma, a quote or a line break. A
/// label that a spreadsheet would read as a formula is prefixed with an
/// apostrophe, so an exported household name cannot run as one.
/// </summary>
public static class PlatformUsageCsv
{
    public const string Header = "group,key,label,requests,tokens,cost_micros,refusals";

    public static IEnumerable<string> Lines(PlatformUsageDto usage)
    {
        yield return Header;
        foreach (var row in usage.Rows)
        {
            yield return string.Join(',',
                Field(usage.GroupBy),
                Field(row.Key ?? string.Empty),
                Field(Neutralize(row.Label)),
                row.Requests.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.Tokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.CostMicros.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.Refusals.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public static string Field(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string Neutralize(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
}
