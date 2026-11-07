using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Mail;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;

namespace Mizan.Application.Contact;

/// <summary>
/// The public contact form (POST /api/Contact). The row and one notice to every
/// administrator are saved together. A filled hidden field is a bot: the answer
/// is the same and nothing is kept. One address may write a few times an hour;
/// it is counted by a hash, so the address itself is never stored.
/// </summary>
public record SubmitContactCommand(string Name, string Email, string Subject, string Message, string? Website)
    : IRequest<ContactAcceptedDto>, IRedactedAudit
{
    /// <summary>The audit row records that a message arrived, not what it said or who sent it.</summary>
    public object AuditDetails => new { Subject, MessageLength = Message?.Length ?? 0, Dropped = !string.IsNullOrEmpty(Website) };
}

public class SubmitContactCommandValidator : AbstractValidator<SubmitContactCommand>
{
    public SubmitContactCommandValidator()
    {
        // A bot that filled the hidden field gets the ordinary answer, whatever else it sent.
        When(x => string.IsNullOrEmpty(x.Website), () =>
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(ContactSubmission.NameMax);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(ContactSubmission.EmailMax);
            RuleFor(x => x.Subject).NotEmpty().MaximumLength(ContactSubmission.SubjectMax);
            RuleFor(x => x.Message).NotEmpty().MaximumLength(ContactSubmission.MessageMax);
        });
    }
}

public class SubmitContactCommandHandler : IRequestHandler<SubmitContactCommand, ContactAcceptedDto>
{
    /// <summary>Messages one address may send in <see cref="Window"/>.</summary>
    public const int PerAddressLimit = 5;

    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly IMizanDbContext _context;
    private readonly IEmailQueue _emails;
    private readonly ICurrentUserService _currentUser;
    private readonly IAppUrls _urls;

    public SubmitContactCommandHandler(
        IMizanDbContext context, IEmailQueue emails, ICurrentUserService currentUser, IAppUrls urls)
    {
        _context = context;
        _emails = emails;
        _currentUser = currentUser;
        _urls = urls;
    }

    public async Task<ContactAcceptedDto> Handle(SubmitContactCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Website)) return new ContactAcceptedDto(true);

        var now = DateTime.UtcNow;
        var ipHash = HashAddress(_currentUser.IpAddress);
        var since = now - Window;
        var recent = await _context.ContactSubmissions.AsNoTracking()
            .CountAsync(c => c.IpHash == ipHash && c.CreatedAt > since, cancellationToken);
        if (recent >= PerAddressLimit)
            throw new RateLimitedException("Too many messages from this address. Try again later.", Window);

        var submission = new ContactSubmission
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            // From the session, never from the form.
            UserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null,
            Status = ContactSubmissionStatus.New,
            IpHash = ipHash,
            CreatedAt = now,
        };
        _context.ContactSubmissions.Add(submission);

        var admins = await _context.Users.AsNoTracking()
            .Where(u => u.Role == "admin" && u.EmailVerified && (!u.Banned || (u.BanExpires != null && u.BanExpires <= now)))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);
        var link = _urls.Asset($"/admin/contact?open={submission.Id}") ?? $"/admin/contact?open={submission.Id}";
        foreach (var admin in admins)
        {
            await _emails.QueueAsync(
                MailTemplates.ContactReceived(admin.Email, submission.Name, submission.Email, submission.Subject, submission.Message, link),
                EmailKinds.ContactReceived,
                toUserId: admin.Id,
                dedupeKey: $"contact:{submission.Id}:{admin.Id}",
                cancellationToken: cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new ContactAcceptedDto(true);
    }

    /// <summary>Enough to count one address's messages; not enough to read the address back.</summary>
    public static string HashAddress(string? ipAddress) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"mizan-contact:{ipAddress ?? "unknown"}"))).ToLowerInvariant();
}

/// <summary>GET /api/admin/contact. Status is New or Handled; search matches name, address, subject or message.</summary>
public record ListContactSubmissionsQuery : IRequest<PagedResult<ContactSubmissionDto>>, IPagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Status { get; init; }
    public string? Search { get; init; }
}

public record GetContactSubmissionQuery(Guid Id) : IRequest<ContactSubmissionDto>;

internal static class ContactRead
{
    internal sealed record Row(ContactSubmission Submission, string? HandledByName, EmailMessageStatus? ReplyStatus);

    public static IQueryable<Row> Project(IQueryable<ContactSubmission> submissions, IMizanDbContext context) =>
        submissions.Select(c => new Row(
            c,
            context.Users.Where(u => u.Id == c.HandledByUserId).Select(u => u.Name ?? u.Email).FirstOrDefault(),
            context.EmailMessages.Where(m => m.Id == c.ReplyEmailMessageId).Select(m => (EmailMessageStatus?)m.Status).FirstOrDefault()));

    public static ContactSubmissionDto ToDto(Row row) => new()
    {
        Id = row.Submission.Id,
        Name = row.Submission.Name,
        Email = row.Submission.Email,
        Subject = row.Submission.Subject,
        Message = row.Submission.Message,
        UserId = row.Submission.UserId,
        Status = row.Submission.Status.ToString(),
        CreatedAt = row.Submission.CreatedAt,
        HandledAt = row.Submission.HandledAt,
        HandledByUserId = row.Submission.HandledByUserId,
        HandledByName = row.HandledByName,
        ReplyEmailMessageId = row.Submission.ReplyEmailMessageId,
        ReplyStatus = row.ReplyStatus?.ToString(),
    };

    public static async Task<ContactSubmissionDto> ReadAsync(IMizanDbContext context, Guid id, CancellationToken cancellationToken)
    {
        var row = await Project(context.ContactSubmissions.AsNoTracking().Where(c => c.Id == id), context)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Contact message", id);
        return ToDto(row);
    }
}

public class ListContactSubmissionsQueryHandler : IRequestHandler<ListContactSubmissionsQuery, PagedResult<ContactSubmissionDto>>
{
    private readonly IMizanDbContext _context;

    public ListContactSubmissionsQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<PagedResult<ContactSubmissionDto>> Handle(ListContactSubmissionsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ContactSubmissions.AsNoTracking();
        if (Enum.TryParse<ContactSubmissionStatus>(request.Status, ignoreCase: true, out var status)
            && !string.IsNullOrWhiteSpace(request.Status) && !int.TryParse(request.Status, out _))
            query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term)
                || c.Email.ToLower().Contains(term)
                || c.Subject.ToLower().Contains(term)
                || c.Message.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await ContactRead.Project(
                query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).ApplyPaging(request), _context)
            .ToListAsync(cancellationToken);

        return new PagedResult<ContactSubmissionDto>
        {
            Items = rows.Select(ContactRead.ToDto).ToList(),
            TotalCount = total,
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 100),
        };
    }
}

public class GetContactSubmissionQueryHandler : IRequestHandler<GetContactSubmissionQuery, ContactSubmissionDto>
{
    private readonly IMizanDbContext _context;

    public GetContactSubmissionQueryHandler(IMizanDbContext context) => _context = context;

    public Task<ContactSubmissionDto> Handle(GetContactSubmissionQuery request, CancellationToken cancellationToken) =>
        ContactRead.ReadAsync(_context, request.Id, cancellationToken);
}

public record MarkContactHandledCommand(Guid Id) : IRequest<ContactSubmissionDto>;

public class MarkContactHandledCommandHandler : IRequestHandler<MarkContactHandledCommand, ContactSubmissionDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkContactHandledCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ContactSubmissionDto> Handle(MarkContactHandledCommand request, CancellationToken cancellationToken)
    {
        var submission = await _context.ContactSubmissions.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Contact message", request.Id);

        if (submission.Status != ContactSubmissionStatus.Handled)
        {
            submission.Status = ContactSubmissionStatus.Handled;
            submission.HandledAt = DateTime.UtcNow;
            submission.HandledByUserId = _currentUser.UserId;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await ContactRead.ReadAsync(_context, submission.Id, cancellationToken);
    }
}

/// <summary>
/// Emails the person who wrote in, from the configured sender address, and
/// marks the message handled, in one save. One reply per message: a second
/// one is refused, so a double click cannot send two.
/// </summary>
public record ReplyContactCommand(Guid Id, string Body) : IRequest<ContactSubmissionDto>;

public class ReplyContactCommandValidator : AbstractValidator<ReplyContactCommand>
{
    public ReplyContactCommandValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(ContactSubmission.MessageMax);
    }
}

public class ReplyContactCommandHandler : IRequestHandler<ReplyContactCommand, ContactSubmissionDto>
{
    private readonly IMizanDbContext _context;
    private readonly IEmailQueue _emails;
    private readonly ICurrentUserService _currentUser;

    public ReplyContactCommandHandler(IMizanDbContext context, IEmailQueue emails, ICurrentUserService currentUser)
    {
        _context = context;
        _emails = emails;
        _currentUser = currentUser;
    }

    public async Task<ContactSubmissionDto> Handle(ReplyContactCommand request, CancellationToken cancellationToken)
    {
        var submission = await _context.ContactSubmissions.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Contact message", request.Id);
        if (submission.ReplyEmailMessageId is not null)
            throw new ConflictException("This message already has a reply.");

        var messageId = await _emails.QueueAsync(
            MailTemplates.ContactReply(submission.Email, submission.Name, submission.Subject, request.Body),
            EmailKinds.ContactReply,
            toUserId: submission.UserId,
            dedupeKey: $"contact-reply:{submission.Id}",
            createdByUserId: _currentUser.UserId,
            cancellationToken: cancellationToken);

        submission.ReplyEmailMessageId = messageId;
        submission.Status = ContactSubmissionStatus.Handled;
        submission.HandledAt ??= DateTime.UtcNow;
        submission.HandledByUserId ??= _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        return await ContactRead.ReadAsync(_context, submission.Id, cancellationToken);
    }
}
