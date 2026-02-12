using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Mizan.Application.Commands;

public record ValidateTokenCommand : IRequest<ValidateTokenResult>
{
    public string Token { get; init; } = string.Empty;
}

public record ValidateTokenResult
{
    public Guid UserId { get; init; }
    public bool IsValid { get; init; }
    public Guid? TokenId { get; init; }
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

        // Update last used timestamp (fire-and-forget)
        try
        {
            mcpToken.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Ignore errors in update
        }

        return new ValidateTokenResult
        {
            UserId = mcpToken.UserId,
            TokenId = mcpToken.Id,
            IsValid = true
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
