using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Admin;
using Mizan.Domain.Entities;

namespace Mizan.Application.Mail;

/// <summary>
/// Who an audience reaches. The database narrows the candidates, the Domain
/// rule (<see cref="AnnouncementAudience"/>) decides each one, and the preview
/// and the send both come through here, so the count an administrator confirms
/// is the count that is queued.
/// </summary>
public sealed class AnnouncementRecipients
{
    private readonly IMizanDbContext _context;
    private readonly IEntitlementService _entitlements;

    public AnnouncementRecipients(IMizanDbContext context, IEntitlementService entitlements)
    {
        _context = context;
        _entitlements = entitlements;
    }

    public sealed record Recipient(Guid UserId, string Email, string? Name);

    public static AnnouncementAudience Parse(string? audience) =>
        AnnouncementAudience.TryParse(audience, out var parsed)
            ? parsed
            : throw new DomainValidationException("Audience is all, pro, free, household:{id} or user:{id}.");

    /// <summary>A short name for the confirmation step: "Everyone", the household's name, the user's address.</summary>
    public async Task<string> LabelAsync(AnnouncementAudience audience, CancellationToken cancellationToken) => audience.Kind switch
    {
        AudienceKind.All => "Everyone",
        AudienceKind.Pro => "Pro subscribers",
        AudienceKind.Free => "Free accounts",
        AudienceKind.Household => await _context.Households.AsNoTracking()
                .Where(h => h.Id == audience.Id).Select(h => h.Name).FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Household", audience.Id!),
        AudienceKind.User => await _context.Users.AsNoTracking()
                .Where(u => u.Id == audience.Id).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("User", audience.Id!),
        _ => audience.ToString(),
    };

    public async Task<IReadOnlyList<Recipient>> ResolveAsync(AnnouncementAudience audience, CancellationToken cancellationToken)
    {
        var users = _context.Users.AsNoTracking();
        users = audience.Kind switch
        {
            AudienceKind.Household => users.Where(u => _context.HouseholdMembers.Any(m => m.HouseholdId == audience.Id && m.UserId == u.Id)),
            AudienceKind.User => users.Where(u => u.Id == audience.Id),
            _ => users,
        };

        var rows = await users
            .Select(u => new { u.Id, u.Email, u.Name, u.EmailVerified, u.Banned, u.BanExpires })
            .ToListAsync(cancellationToken);

        var pro = audience.Kind is AudienceKind.Pro or AudienceKind.Free
            ? await _entitlements.GetProUserIdsAsync(rows.Select(r => r.Id).ToList(), cancellationToken)
            : new HashSet<Guid>();
        IReadOnlyCollection<Guid> households = audience.Kind == AudienceKind.Household ? [audience.Id!.Value] : [];

        var names = rows.ToDictionary(r => r.Id, r => r.Name);
        return audience
            .Resolve(
                rows.Select(r => new AnnouncementCandidate(r.Id, r.Email, r.EmailVerified, r.Banned, r.BanExpires, pro.Contains(r.Id), households)),
                DateTime.UtcNow)
            .Select(c => new Recipient(c.UserId, c.Email, names[c.UserId]))
            .ToList();
    }
}

public record GetAnnouncementAudienceQuery(string Audience) : IRequest<AnnouncementAudienceDto>;

public class GetAnnouncementAudienceQueryHandler : IRequestHandler<GetAnnouncementAudienceQuery, AnnouncementAudienceDto>
{
    private readonly AnnouncementRecipients _recipients;

    public GetAnnouncementAudienceQueryHandler(AnnouncementRecipients recipients) => _recipients = recipients;

    public async Task<AnnouncementAudienceDto> Handle(GetAnnouncementAudienceQuery request, CancellationToken cancellationToken)
    {
        var audience = AnnouncementRecipients.Parse(request.Audience);
        var label = await _recipients.LabelAsync(audience, cancellationToken);
        var recipients = await _recipients.ResolveAsync(audience, cancellationToken);
        return new AnnouncementAudienceDto(audience.ToString(), label, recipients.Count);
    }
}

/// <summary>
/// Queues one email per recipient, each with the dedupe key
/// <c>announcement:{announcementId}:{userId}</c>, and records the announcement,
/// all in one save. Sending the same announcement id again queues nothing new
/// and answers with the first count. An announcement to everyone is limited to
/// one an hour.
/// </summary>
public record SendAnnouncementCommand(string Subject, string Body, string Audience, Guid? AnnouncementId = null)
    : IRequest<AnnouncementResultDto>;

public class SendAnnouncementCommandValidator : AbstractValidator<SendAnnouncementCommand>
{
    public const int SubjectMax = 200;
    public const int BodyMax = 20_000;

    public SendAnnouncementCommandValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(SubjectMax);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(BodyMax);
        RuleFor(x => x.Audience).Must(a => AnnouncementAudience.TryParse(a, out _))
            .WithMessage("Audience is all, pro, free, household:{id} or user:{id}.");
    }
}

public class SendAnnouncementCommandHandler : IRequestHandler<SendAnnouncementCommand, AnnouncementResultDto>
{
    public static readonly TimeSpan EveryoneInterval = TimeSpan.FromHours(1);

    private readonly IMizanDbContext _context;
    private readonly IEmailQueue _emails;
    private readonly AnnouncementRecipients _recipients;
    private readonly ICurrentUserService _currentUser;

    public SendAnnouncementCommandHandler(
        IMizanDbContext context, IEmailQueue emails, AnnouncementRecipients recipients, ICurrentUserService currentUser)
    {
        _context = context;
        _emails = emails;
        _recipients = recipients;
        _currentUser = currentUser;
    }

    public async Task<AnnouncementResultDto> Handle(SendAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var audience = AnnouncementRecipients.Parse(request.Audience);
        var id = request.AnnouncementId is { } given && given != Guid.Empty ? given : Guid.CreateVersion7();

        var earlier = await _context.Announcements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (earlier is not null) return new AnnouncementResultDto(earlier.Id, earlier.Audience, earlier.RecipientCount);

        var now = DateTime.UtcNow;
        if (audience.IsEveryone)
        {
            var everyone = audience.ToString();
            var last = await _context.Announcements.AsNoTracking()
                .Where(a => a.Audience == everyone && a.CreatedAt > now - EveryoneInterval)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => (DateTime?)a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (last is { } at)
                throw new RateLimitedException("An announcement to everyone went out within the last hour.", at + EveryoneInterval - now);
        }

        // Before resolving, so a missing household or user is a 404 rather than an empty send.
        await _recipients.LabelAsync(audience, cancellationToken);
        var recipients = await _recipients.ResolveAsync(audience, cancellationToken);
        if (recipients.Count == 0) throw new DomainValidationException("Nobody in that audience can receive email.");

        var subject = request.Subject.Trim();
        foreach (var recipient in recipients)
        {
            await _emails.QueueAsync(
                MailTemplates.Announcement(recipient.Email, recipient.Name, subject, request.Body),
                EmailKinds.Announcement,
                toUserId: recipient.UserId,
                dedupeKey: $"announcement:{id}:{recipient.UserId}",
                createdByUserId: adminId,
                cancellationToken: cancellationToken);
        }

        _context.Announcements.Add(new Announcement
        {
            Id = id,
            Subject = subject,
            Audience = audience.ToString(),
            RecipientCount = recipients.Count,
            CreatedByUserId = adminId,
            CreatedAt = now,
        });

        await _context.SaveChangesAsync(cancellationToken);
        return new AnnouncementResultDto(id, audience.ToString(), recipients.Count);
    }
}
