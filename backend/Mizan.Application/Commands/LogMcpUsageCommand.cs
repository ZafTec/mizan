using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record LogMcpUsageCommand : IRequest
{
    public Guid McpTokenId { get; init; }
    public string ToolName { get; init; } = string.Empty;
    public string? Parameters { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int ExecutionTimeMs { get; init; }
}

public class LogMcpUsageCommandHandler : IRequestHandler<LogMcpUsageCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public LogMcpUsageCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(LogMcpUsageCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var tokenExists = await _context.McpTokens
            .AsNoTracking()
            .AnyAsync(t => t.Id == request.McpTokenId && t.UserId == _currentUser.UserId.Value, cancellationToken);

        if (!tokenExists)
        {
            throw new UnauthorizedAccessException("MCP token not found");
        }

        var log = new McpUsageLog
        {
            Id = Guid.NewGuid(),
            McpTokenId = request.McpTokenId,
            UserId = _currentUser.UserId.Value,
            ToolName = request.ToolName,
            Parameters = request.Parameters,
            Success = request.Success,
            ErrorMessage = request.ErrorMessage,
            ExecutionTimeMs = request.ExecutionTimeMs,
            Timestamp = DateTime.UtcNow
        };

        _context.McpUsageLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
