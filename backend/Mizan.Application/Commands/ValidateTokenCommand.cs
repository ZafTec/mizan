using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Mizan.Application.Commands;

public record ValidateTokenCommand : IRequest<ValidateTokenResult>, ISkipAudit
{
    public string Token { get; init; } = string.Empty;
}

public record ValidateTokenResult
{
    public Guid UserId { get; init; }
    public bool IsValid { get; init; }
    public Guid? TokenId { get; init; }
    public string Role { get; init; } = "user";
    public string Plan { get; init; } = "free";
    public int? MonthlyLimit { get; init; }
    public int UsedThisMonth { get; init; }
    public int? RemainingThisMonth => MonthlyLimit.HasValue ? Math.Max(0, MonthlyLimit.Value - UsedThisMonth) : null;
    public bool QuotaExceeded => MonthlyLimit.HasValue && UsedThisMonth >= MonthlyLimit.Value;
}

public class ValidateTokenCommandHandler : IRequestHandler<ValidateTokenCommand, ValidateTokenResult>
{
    private readonly IMizanDbContext _context;

    public ValidateTokenCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<ValidateTokenResult> Handle(ValidateTokenCommand request, CancellationToken cancellationToken)
    {
        // Validate token format
        if (string.IsNullOrWhiteSpace(request.Token) ||
            !request.Token.StartsWith("mcp_") ||
            request.Token.Length != 68)
        {
            return new ValidateTokenResult { IsValid = false };
        }

        // Compute hash
        var tokenHash = ComputeHash(request.Token);

        // Find token in database
        var mcpToken = await _context.McpTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.IsActive, cancellationToken);

        if (mcpToken == null)
        {
            return new ValidateTokenResult { IsValid = false };
        }

        // Check expiration
        if (mcpToken.ExpiresAt.HasValue && mcpToken.ExpiresAt.Value < DateTime.UtcNow)
        {
            return new ValidateTokenResult { IsValid = false };
        }

        mcpToken.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var plan = await _context.Subscriptions.Where(s => s.UserId == mcpToken.UserId).Select(s => s.Plan).FirstOrDefaultAsync(cancellationToken) ?? "free";
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var used = await _context.McpUsageLogs.CountAsync(log => log.UserId == mcpToken.UserId && log.Success && log.Timestamp >= monthStart, cancellationToken);

        return new ValidateTokenResult
        {
            UserId = mcpToken.UserId,
            TokenId = mcpToken.Id,
            IsValid = true,
            Role = mcpToken.User.Role,
            Plan = plan,
            MonthlyLimit = string.Equals(plan, "free", StringComparison.OrdinalIgnoreCase) ? McpUsagePolicy.FreeMonthlyToolCalls : null,
            UsedThisMonth = used
        };
    }

    private static string ComputeHash(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
