using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

public record AiUsageDayDto(DateOnly Date, int Requests, int Tokens);

public record AiUsageFeatureDto(string Feature, int Requests, int Tokens);

public record MyAiUsageDto(
    AiQuotaSnapshot Today,
    IReadOnlyList<AiUsageDayDto> History,
    IReadOnlyList<AiUsageFeatureDto> ByFeature);

public record GetMyAiUsageQuery(int Days = 14) : IRequest<MyAiUsageDto>;

public class GetMyAiUsageQueryHandler : IRequestHandler<GetMyAiUsageQuery, MyAiUsageDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiQuotaService _quota;

    public GetMyAiUsageQueryHandler(
        IMizanDbContext context, ICurrentUserService currentUser, IAiQuotaService quota)
    {
        _context = context;
        _currentUser = currentUser;
        _quota = quota;
    }

    public async Task<MyAiUsageDto> Handle(GetMyAiUsageQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var days = Math.Clamp(request.Days, 1, 90);
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var rows = await _context.AiUsageLogs.AsNoTracking()
            .Where(log => log.UserId == userId && log.CreatedAt >= since)
            .Select(log => new { log.CreatedAt, log.Feature, log.PromptTokens, log.CompletionTokens })
            .ToListAsync(cancellationToken);

        var history = rows
            .GroupBy(row => DateOnly.FromDateTime(row.CreatedAt))
            .Select(g => new AiUsageDayDto(g.Key, g.Count(), g.Sum(r => r.PromptTokens + r.CompletionTokens)))
            .OrderByDescending(d => d.Date)
            .ToList();

        var byFeature = rows
            .GroupBy(row => row.Feature)
            .Select(g => new AiUsageFeatureDto(g.Key, g.Count(), g.Sum(r => r.PromptTokens + r.CompletionTokens)))
            .OrderByDescending(f => f.Tokens)
            .ToList();

        return new MyAiUsageDto(
            await _quota.GetUserSnapshotAsync(userId, cancellationToken), history, byFeature);
    }
}

public record GlobalAiUsageDto(
    long TokensToday,
    long TokenCeiling,
    long CostMicrosToday,
    long CostCeilingMicros,
    int RequestsToday,
    int FailuresToday,
    int ActiveUsersToday,
    IReadOnlyList<AiUsageFeatureDto> ByFeature);

/// <summary>
/// What the admin sees. The point of this view is noticing a cost problem
/// before the invoice does (docs/REFOCUS.md §10).
/// </summary>
public record GetGlobalAiUsageQuery : IRequest<GlobalAiUsageDto>;

public class GetGlobalAiUsageQueryHandler : IRequestHandler<GetGlobalAiUsageQuery, GlobalAiUsageDto>
{
    private readonly IMizanDbContext _context;
    private readonly IAiCeilings _ceilings;

    public GetGlobalAiUsageQueryHandler(IMizanDbContext context, IAiCeilings ceilings)
    {
        _context = context;
        _ceilings = ceilings;
    }

    public async Task<GlobalAiUsageDto> Handle(GetGlobalAiUsageQuery request, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.Date;

        var rows = await _context.AiUsageLogs.AsNoTracking()
            .Where(log => log.CreatedAt >= since)
            .Select(log => new
            {
                log.UserId,
                log.Feature,
                log.PromptTokens,
                log.CompletionTokens,
                log.EstimatedCostMicros,
                log.Outcome,
            })
            .ToListAsync(cancellationToken);

        return new GlobalAiUsageDto(
            rows.Sum(r => (long)r.PromptTokens + r.CompletionTokens),
            _ceilings.GlobalDailyTokens,
            rows.Sum(r => r.EstimatedCostMicros),
            _ceilings.GlobalDailyCostMicros,
            rows.Count,
            rows.Count(r => r.Outcome is not AiCallOutcome.Succeeded and not AiCallOutcome.Pending),
            rows.Select(r => r.UserId).Distinct().Count(),
            rows.GroupBy(r => r.Feature)
                .Select(g => new AiUsageFeatureDto(g.Key, g.Count(), g.Sum(r => r.PromptTokens + r.CompletionTokens)))
                .OrderByDescending(f => f.Tokens)
                .ToList());
    }
}
