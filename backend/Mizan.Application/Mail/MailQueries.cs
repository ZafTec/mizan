using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;

namespace Mizan.Application.Mail;

/// <summary>GET /api/admin/mail. Every filter is optional; from and to are inclusive dates in UTC.</summary>
public record ListEmailMessagesQuery : IRequest<PagedResult<EmailMessageDto>>, IPagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Queued, Sent or Failed.</summary>
    public string? Status { get; init; }

    public string? Kind { get; init; }

    /// <summary>Part of the recipient's address.</summary>
    public string? Search { get; init; }

    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public record GetEmailMessageQuery(Guid Id) : IRequest<EmailMessageDto>;

public record GetMailStatsQuery : IRequest<MailStatsDto>;

internal static class EmailMessageRead
{
    internal sealed record Row(EmailMessage Message, string? CreatedByName, bool JobRetryable);

    public static IQueryable<Row> Project(IQueryable<EmailMessage> messages, IMizanDbContext context) =>
        messages.Select(m => new Row(
            m,
            context.Users.Where(u => u.Id == m.CreatedByUserId).Select(u => u.Name ?? u.Email).FirstOrDefault(),
            context.OutboxJobs.Any(j => j.Id == m.OutboxJobId
                && (j.Status == OutboxJobStatus.Failed || j.Status == OutboxJobStatus.DeadLettered))));

    public static EmailMessageDto ToDto(Row row) => new()
    {
        Id = row.Message.Id,
        ToUserId = row.Message.ToUserId,
        ToAddress = row.Message.ToAddress,
        Subject = row.Message.Subject,
        Kind = row.Message.Kind,
        Status = row.Message.Status.ToString(),
        Attempts = row.Message.Attempts,
        LastError = OutboxError.Redact(row.Message.LastError),
        OutboxJobId = row.Message.OutboxJobId,
        CreatedAt = row.Message.CreatedAt,
        SentAt = row.Message.SentAt,
        CreatedByUserId = row.Message.CreatedByUserId,
        CreatedByName = row.CreatedByName,
        CanRetry = row.Message.Status != EmailMessageStatus.Sent && row.JobRetryable,
    };

    public static bool TryParseStatus(string? value, out EmailMessageStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) && !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _);
}

public class ListEmailMessagesQueryHandler : IRequestHandler<ListEmailMessagesQuery, PagedResult<EmailMessageDto>>
{
    private readonly IMizanDbContext _context;

    public ListEmailMessagesQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<PagedResult<EmailMessageDto>> Handle(ListEmailMessagesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.EmailMessages.AsNoTracking();

        if (EmailMessageRead.TryParseStatus(request.Status, out var status))
            query = query.Where(m => m.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Kind))
            query = query.Where(m => m.Kind == request.Kind.Trim());
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(m => m.ToAddress.ToLower().Contains(term));
        }
        if (request.From is { } from)
        {
            var since = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(m => m.CreatedAt >= since);
        }
        if (request.To is { } to)
        {
            var before = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(m => m.CreatedAt < before);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await EmailMessageRead.Project(
                query.OrderByDescending(m => m.CreatedAt).ThenBy(m => m.Id).ApplyPaging(request),
                _context)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmailMessageDto>
        {
            Items = rows.Select(EmailMessageRead.ToDto).ToList(),
            TotalCount = total,
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 100),
        };
    }
}

public class GetEmailMessageQueryHandler : IRequestHandler<GetEmailMessageQuery, EmailMessageDto>
{
    private readonly IMizanDbContext _context;

    public GetEmailMessageQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<EmailMessageDto> Handle(GetEmailMessageQuery request, CancellationToken cancellationToken)
    {
        var row = await EmailMessageRead.Project(_context.EmailMessages.AsNoTracking().Where(m => m.Id == request.Id), _context)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Email", request.Id);
        return EmailMessageRead.ToDto(row);
    }
}

public class GetMailStatsQueryHandler : IRequestHandler<GetMailStatsQuery, MailStatsDto>
{
    private readonly IMizanDbContext _context;

    public GetMailStatsQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<MailStatsDto> Handle(GetMailStatsQuery request, CancellationToken cancellationToken)
    {
        var counts = await _context.EmailMessages.AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);
        var kinds = await _context.EmailMessages.AsNoTracking()
            .Select(m => m.Kind)
            .Distinct()
            .OrderBy(k => k)
            .ToListAsync(cancellationToken);

        return new MailStatsDto(
            counts.GetValueOrDefault(EmailMessageStatus.Queued),
            counts.GetValueOrDefault(EmailMessageStatus.Sent),
            counts.GetValueOrDefault(EmailMessageStatus.Failed),
            kinds);
    }
}

/// <summary>
/// Puts a message that did not go back in the queue. Its body lives only in its
/// outbox job, so this resets that job rather than writing a new one, and a
/// message whose job was deleted cannot be sent again. A sent message is never
/// sent twice from here.
/// </summary>
public record RetryEmailMessageCommand(Guid Id) : IRequest<EmailMessageDto>;

public class RetryEmailMessageCommandHandler : IRequestHandler<RetryEmailMessageCommand, EmailMessageDto>
{
    private readonly IMizanDbContext _context;
    private readonly ISender _mediator;

    public RetryEmailMessageCommandHandler(IMizanDbContext context, ISender mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<EmailMessageDto> Handle(RetryEmailMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await _context.EmailMessages.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Email", request.Id);

        if (message.Status == EmailMessageStatus.Sent)
            throw new ConflictException("This email was sent. Sending it again is not possible from the log.");

        var job = message.OutboxJobId is { } jobId
            ? await _context.OutboxJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            : null;
        if (job is null)
            throw new ConflictException("This email's job was deleted, and its body with it, so it cannot be sent again.");
        if (job.Status is OutboxJobStatus.Pending or OutboxJobStatus.Running)
            throw new ConflictException("This email is already queued.");
        if (job.Status == OutboxJobStatus.Succeeded)
            throw new ConflictException("This email's job finished. Sending it again is not possible from the log.");

        // As RetryAdminJobCommand does: the operator has usually just fixed
        // the cause, so the job gets its full set of attempts back.
        job.Status = OutboxJobStatus.Pending;
        job.Attempts = 0;
        job.RunAfter = DateTime.UtcNow;
        job.StartedAt = null;
        job.CompletedAt = null;
        message.Status = EmailMessageStatus.Queued;

        await _context.SaveChangesAsync(cancellationToken);
        return await _mediator.Send(new GetEmailMessageQuery(message.Id), cancellationToken);
    }
}
