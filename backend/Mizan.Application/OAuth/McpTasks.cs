using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.OAuth;

/// <summary>What the MCP service needs to report about a task. The result and error are raw JSON.</summary>
public record McpTaskDto
{
    public string Id { get; init; } = string.Empty;
    public string Status { get; init; } = McpTask.Working;
    public string? StatusMessage { get; init; }
    public JsonElement? Result { get; init; }
    public JsonElement? Error { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int TtlSeconds { get; init; }
    public int PollIntervalMs { get; init; }
}

internal static class McpTaskRules
{
    /// <summary>A working task nobody has touched for this long lost its worker, for example in a restart.</summary>
    public static readonly TimeSpan Stale = TimeSpan.FromMinutes(15);

    public const string InterruptedError =
        """{"code":-32603,"message":"The task was interrupted before it finished. Start it again."}""";

    public static McpTaskDto ToDto(McpTask task) => new()
    {
        Id = task.Id,
        Status = task.Status,
        StatusMessage = task.StatusMessage,
        Result = task.ResultJson is null ? null : JsonDocument.Parse(task.ResultJson).RootElement.Clone(),
        Error = task.ErrorJson is null ? null : JsonDocument.Parse(task.ErrorJson).RootElement.Clone(),
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt,
        TtlSeconds = task.TtlSeconds,
        PollIntervalMs = task.PollIntervalMs,
    };

    public static async Task<McpTask?> OwnedAsync(IMizanDbContext context, string id, Guid userId, CancellationToken ct)
    {
        var task = await context.McpTasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
        if (task is null) return null;

        var now = DateTime.UtcNow;
        if (now > task.CreatedAt.AddSeconds(task.TtlSeconds)) return null;

        if (task.Status == McpTask.Working && now - task.UpdatedAt > Stale)
        {
            task.Status = McpTask.Failed;
            task.ErrorJson = InterruptedError;
            task.UpdatedAt = now;
            await context.SaveChangesAsync(ct);
        }

        return task;
    }
}

public record CreateMcpTaskCommand(Guid? GrantId) : IRequest<McpTaskDto>;

public class CreateMcpTaskCommandHandler : IRequestHandler<CreateMcpTaskCommand, McpTaskDto>
{
    private const int MaxOpenPerUser = 20;
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateMcpTaskCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<McpTaskDto> Handle(CreateMcpTaskCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;

        // Finished tasks have outlived their time to live, so the table does not grow without bound.
        await _context.McpTasks
            .Where(t => t.UserId == userId && t.CreatedAt < now.AddHours(-24))
            .ExecuteDeleteAsync(cancellationToken);

        var open = await _context.McpTasks.CountAsync(
            t => t.UserId == userId && t.Status == McpTask.Working && t.UpdatedAt > now.AddMinutes(-15), cancellationToken);
        if (open >= MaxOpenPerUser)
            throw new Exceptions.DomainValidationException("Too many tasks are still running. Wait for one to finish.");

        var task = new McpTask
        {
            Id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            UserId = userId,
            GrantId = request.GrantId ?? _currentUser.Grant?.GrantId,
            Status = McpTask.Working,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.McpTasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);
        return McpTaskRules.ToDto(task);
    }
}

public record GetMcpTaskQuery(string Id) : IRequest<McpTaskDto?>;

public class GetMcpTaskQueryHandler : IRequestHandler<GetMcpTaskQuery, McpTaskDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMcpTaskQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<McpTaskDto?> Handle(GetMcpTaskQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var task = await McpTaskRules.OwnedAsync(_context, request.Id, userId, cancellationToken);
        return task is null ? null : McpTaskRules.ToDto(task);
    }
}

/// <summary>Moves a working task to completed, failed or cancelled. A task already finished stays as it is.</summary>
public record FinishMcpTaskCommand(string Id, string Outcome, string? Json) : IRequest<bool>;

public class FinishMcpTaskCommandHandler : IRequestHandler<FinishMcpTaskCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FinishMcpTaskCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(FinishMcpTaskCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var task = await McpTaskRules.OwnedAsync(_context, request.Id, userId, cancellationToken);
        if (task is null || task.Status != McpTask.Working) return false;

        if (request.Json is not null)
        {
            try { JsonDocument.Parse(request.Json).Dispose(); }
            catch (JsonException) { throw new Exceptions.DomainValidationException("The task result is not valid JSON."); }
        }

        switch (request.Outcome)
        {
            case McpTask.Completed: task.Status = McpTask.Completed; task.ResultJson = request.Json; break;
            case McpTask.Failed: task.Status = McpTask.Failed; task.ErrorJson = request.Json; break;
            case McpTask.Cancelled: task.Status = McpTask.Cancelled; break;
            default: throw new Exceptions.DomainValidationException("Unknown task outcome.");
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
