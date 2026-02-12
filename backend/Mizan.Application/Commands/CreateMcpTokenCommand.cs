using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace Mizan.Application.Commands;

public record CreateMcpTokenCommand : IRequest<CreateMcpTokenResult>
{
    public string Name { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
}

public record CreateMcpTokenResult
{
    public Guid Id { get; init; }
    public string PlaintextToken { get; init; } = string.Empty; // Shown only once
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public class CreateMcpTokenCommandHandler : IRequestHandler<CreateMcpTokenCommand, CreateMcpTokenResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateMcpTokenCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateMcpTokenResult> Handle(CreateMcpTokenCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated to create MCP tokens");
        }

        // Validate name is not empty
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Token name cannot be empty");
        }

        // Check if user already has a token with this name
        var existingToken = await _context.McpTokens
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId && t.Name == request.Name, cancellationToken);

        if (existingToken != null)
        {
            throw new InvalidOperationException($"You already have an MCP token named '{request.Name}'");
        }

        // Generate random token (64 chars)
        var plaintextToken = GenerateToken();

        // Compute SHA256 hash
        var tokenHash = ComputeHash(plaintextToken);

        var mcpToken = new McpToken
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            TokenHash = tokenHash,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IsActive = true
        };

        _context.McpTokens.Add(mcpToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateMcpTokenResult
        {
            Id = mcpToken.Id,
            PlaintextToken = plaintextToken, // Include prefix
            Name = mcpToken.Name,
            CreatedAt = mcpToken.CreatedAt,
            ExpiresAt = mcpToken.ExpiresAt
        };
    }

    private static string GenerateToken()
    {
        const string prefix = "mcp_";
        const int tokenLength = 64;

        var randomBytes = new byte[tokenLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var tokenBody = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, tokenLength);

        return prefix + tokenBody;
    }

    private static string ComputeHash(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
