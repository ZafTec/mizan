using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record LogMcpUsageCommand : IRequest
{
    public Guid GrantId { get; init; }
    public string Kind { get; init; } = "tool";
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

        var grantExists = await _context.OAuthGrants
            .AsNoTracking()
            .AnyAsync(g => g.Id == request.GrantId && g.UserId == _currentUser.UserId.Value, cancellationToken);
        if (!grantExists) throw new UnauthorizedAccessException("MCP connection not found");

        var kind = request.Kind is "tool" or "resource" or "prompt" or "task" ? request.Kind : "tool";

        var log = new McpUsageLog
        {
            Id = Guid.NewGuid(),
            GrantId = request.GrantId,
            Kind = kind,
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
