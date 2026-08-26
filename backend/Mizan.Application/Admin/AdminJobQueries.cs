using System.Linq.Expressions;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Admin;

public record AdminJobDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Attempts { get; init; }
    public DateTime RunAfter { get; init; }
    public string? LastError { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// Counts by status, for the header of the jobs page. The number that matters
/// is dead-lettered: it is the count of things a user asked for that never
/// happened, and before this table existed it was unobservable.
/// </summary>
public record AdminJobStats
{
    public int Pending { get; init; }
    public int Running { get; init; }
    public int Failed { get; init; }
    public int DeadLettered { get; init; }
    public int Succeeded { get; init; }
    public IReadOnlyList<string> Types { get; init; } = Array.Empty<string>();
}

public record ListAdminJobsQuery : IRequest<PagedResult<AdminJobDto>>, IPagedQuery, ISortableQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Type { get; init; }
    public string? Status { get; init; }
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record GetAdminJobStatsQuery : IRequest<AdminJobStats>;

public class ListAdminJobsQueryHandler : IRequestHandler<ListAdminJobsQuery, PagedResult<AdminJobDto>>
{
    private static readonly Dictionary<string, Expression<Func<OutboxJob, object>>> SortMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = j => j.Type,
            ["status"] = j => j.Status,
            ["attempts"] = j => j.Attempts,
            ["runafter"] = j => j.RunAfter,
            ["createdat"] = j => j.CreatedAt,
        };

    private readonly IMizanDbContext _context;

    public ListAdminJobsQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<PagedResult<AdminJobDto>> Handle(
        ListAdminJobsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.OutboxJobs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            query = query.Where(j => j.Type == request.Type);
        }

        if (AdminJobStatus.TryParse(request.Status, out var status))
        {
            query = query.Where(j => j.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .ApplySorting(request, SortMappings, j => j.CreatedAt, defaultDescending: true)
            .ThenBy(j => j.Id)
            .ApplyPaging(request)
            .Select(j => new
            {
                j.Id,
                j.Type,
                j.Status,
                j.Attempts,
                j.RunAfter,
                j.LastError,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminJobDto>
        {
            Items = rows.Select(j => new AdminJobDto
            {
                Id = j.Id,
                Type = j.Type,
                Status = j.Status.ToString(),
                Attempts = j.Attempts,
                RunAfter = j.RunAfter,
                LastError = AdminJobStatus.Redact(j.LastError),
                CreatedAt = j.CreatedAt,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
            }).ToList(),
            TotalCount = total,
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 100),
        };
    }
}

public class GetAdminJobStatsQueryHandler : IRequestHandler<GetAdminJobStatsQuery, AdminJobStats>
{
    private readonly IMizanDbContext _context;

    public GetAdminJobStatsQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<AdminJobStats> Handle(GetAdminJobStatsQuery request, CancellationToken cancellationToken)
    {
        var counts = await _context.OutboxJobs.AsNoTracking()
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        var types = await _context.OutboxJobs.AsNoTracking()
            .Select(j => j.Type)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);

        return new AdminJobStats
        {
            Pending = counts.GetValueOrDefault(OutboxJobStatus.Pending),
            Running = counts.GetValueOrDefault(OutboxJobStatus.Running),
            Failed = counts.GetValueOrDefault(OutboxJobStatus.Failed),
            DeadLettered = counts.GetValueOrDefault(OutboxJobStatus.DeadLettered),
            Succeeded = counts.GetValueOrDefault(OutboxJobStatus.Succeeded),
            Types = types,
        };
    }
}

/// <summary>
/// Puts a dead-lettered job back in the queue.
///
/// The attempt counter resets, because the operator retrying it has usually
/// just fixed the thing that broke it - a wrong SMTP host, an expired provider
/// key - and leaving the count at its cap would dead-letter it again on the
/// first hiccup.
/// </summary>
public record RetryAdminJobCommand(Guid Id) : IRequest;

public class RetryAdminJobCommandHandler : IRequestHandler<RetryAdminJobCommand>
{
    private readonly IMizanDbContext _context;

    public RetryAdminJobCommandHandler(IMizanDbContext context) => _context = context;

    public async Task Handle(RetryAdminJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _context.OutboxJobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new Exceptions.EntityNotFoundException("Job", request.Id);

        if (job.Status is not (OutboxJobStatus.DeadLettered or OutboxJobStatus.Failed))
        {
            throw new Exceptions.DomainValidationException("Only a failed or dead-lettered job can be retried.");
        }

        job.Status = OutboxJobStatus.Pending;
        job.Attempts = 0;
        job.RunAfter = DateTime.UtcNow;
        job.StartedAt = null;
        job.CompletedAt = null;

        await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Drops a job nobody intends to run again.
///
/// Only from a terminal state: deleting a Pending row is how you lose a
/// password reset somebody is currently waiting on.
/// </summary>
public record DeleteAdminJobCommand(Guid Id) : IRequest;

public class DeleteAdminJobCommandHandler : IRequestHandler<DeleteAdminJobCommand>
{
    private readonly IMizanDbContext _context;

    public DeleteAdminJobCommandHandler(IMizanDbContext context) => _context = context;

    public async Task Handle(DeleteAdminJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _context.OutboxJobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new Exceptions.EntityNotFoundException("Job", request.Id);

        if (job.Status is not (OutboxJobStatus.DeadLettered or OutboxJobStatus.Succeeded))
        {
            throw new Exceptions.DomainValidationException("Only a dead-lettered or succeeded job can be deleted.");
        }

        _context.OutboxJobs.Remove(job);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public static partial class AdminJobStatus
{
    public static bool TryParse(string? value, out OutboxJobStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) && !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// An SMTP rejection quotes the recipient back at you, and this view is
    /// read by operators who have no business seeing who was mailed. The
    /// diagnostic value is in the rest of the message, so the address goes.
    /// </summary>
    public static string? Redact(string? error) =>
        string.IsNullOrEmpty(error) ? error : EmailPattern().Replace(error, "[redacted]");

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+")]
    private static partial Regex EmailPattern();
}
