using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMcpUsageAnalyticsQuery : IRequest<McpUsageAnalyticsResult>
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public record McpUsageAnalyticsResult
{
    public UsageOverview Overview { get; init; } = new();
    public List<ToolUsageDto> ToolUsage { get; init; } = new();
    public List<TokenUsageDto> TokenUsage { get; init; } = new();
    public List<DailyUsageDto> DailyUsage { get; init; } = new();
}

public record UsageOverview
{
    public int TotalCalls { get; init; }
    public int SuccessfulCalls { get; init; }
    public int FailedCalls { get; init; }
    public double SuccessRate { get; init; }
    public int AverageExecutionTimeMs { get; init; }
    public int UniqueTokensUsed { get; init; }
}

public record ToolUsageDto
{
    public string ToolName { get; init; } = string.Empty;
    public int CallCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public int AverageExecutionTimeMs { get; init; }
}

public record TokenUsageDto
{
    public Guid TokenId { get; init; }
    public string TokenName { get; init; } = string.Empty;
    public int CallCount { get; init; }
    public DateTime LastUsed { get; init; }
}

public record DailyUsageDto
{
    public DateOnly Date { get; init; }
    public int CallCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
}

public class GetMcpUsageAnalyticsQueryHandler : IRequestHandler<GetMcpUsageAnalyticsQuery, McpUsageAnalyticsResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMcpUsageAnalyticsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<McpUsageAnalyticsResult> Handle(GetMcpUsageAnalyticsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new McpUsageAnalyticsResult();
        }

        var startDate = request.StartDate ?? DateTime.UtcNow.AddDays(-30);
        var endDate = request.EndDate ?? DateTime.UtcNow;

        var logs = await _context.McpUsageLogs
            .Include(l => l.McpToken)
            .Where(l => l.UserId == _currentUser.UserId && l.Timestamp >= startDate && l.Timestamp <= endDate)
            .ToListAsync(cancellationToken);

        // Overview
        var totalCalls = logs.Count;
        var successfulCalls = logs.Count(l => l.Success);
        var failedCalls = totalCalls - successfulCalls;
        var successRate = totalCalls > 0 ? (double)successfulCalls / totalCalls * 100 : 0;
        var avgExecutionTime = logs.Any() ? (int)logs.Average(l => l.ExecutionTimeMs) : 0;
        var uniqueTokens = logs.Select(l => l.McpTokenId).Distinct().Count();

        // Tool Usage
        var toolUsage = logs
            .GroupBy(l => l.ToolName)
            .Select(g => new ToolUsageDto
            {
                ToolName = g.Key,
                CallCount = g.Count(),
                SuccessCount = g.Count(l => l.Success),
                FailureCount = g.Count(l => !l.Success),
                AverageExecutionTimeMs = (int)g.Average(l => l.ExecutionTimeMs)
            })
            .OrderByDescending(t => t.CallCount)
            .ToList();

        // Token Usage
        var tokenUsage = logs
            .GroupBy(l => new { l.McpTokenId, l.McpToken.Name })
            .Select(g => new TokenUsageDto
            {
                TokenId = g.Key.McpTokenId,
                TokenName = g.Key.Name,
                CallCount = g.Count(),
                LastUsed = g.Max(l => l.Timestamp)
            })
            .OrderByDescending(t => t.CallCount)
            .ToList();

        // Daily Usage
        var dailyUsage = logs
            .GroupBy(l => DateOnly.FromDateTime(l.Timestamp))
            .Select(g => new DailyUsageDto
            {
                Date = g.Key,
                CallCount = g.Count(),
                SuccessCount = g.Count(l => l.Success),
                FailureCount = g.Count(l => !l.Success)
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new McpUsageAnalyticsResult
        {
            Overview = new UsageOverview
            {
                TotalCalls = totalCalls,
                SuccessfulCalls = successfulCalls,
                FailedCalls = failedCalls,
                SuccessRate = successRate,
                AverageExecutionTimeMs = avgExecutionTime,
                UniqueTokensUsed = uniqueTokens
            },
            ToolUsage = toolUsage,
            TokenUsage = tokenUsage,
            DailyUsage = dailyUsage
        };
    }
}
