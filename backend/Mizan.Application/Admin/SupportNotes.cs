using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;

namespace Mizan.Application.Admin;

/// <summary>
/// Notes administrators leave on a user or a household for each other
/// (docs/ADMIN.md#households-users-and-support-notes). The API serves them to
/// administrators only; any administrator may edit or delete any note, and the
/// audit log records who did.
/// </summary>
internal static class SupportNoteRead
{
    public static SupportNoteSubject ParseSubject(string? value) =>
        Enum.TryParse<SupportNoteSubject>(value, ignoreCase: true, out var subject)
            && !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _)
            ? subject
            : throw new DomainValidationException("Subject type is User or Household.");

    public static IQueryable<SupportNoteDto> Project(IQueryable<SupportNote> notes, IMizanDbContext context) =>
        notes.Select(n => new SupportNoteDto
        {
            Id = n.Id,
            SubjectType = n.SubjectType == SupportNoteSubject.User ? "User" : "Household",
            SubjectId = n.SubjectId,
            Body = n.Body,
            AuthorUserId = n.AuthorUserId,
            AuthorName = context.Users.Where(u => u.Id == n.AuthorUserId).Select(u => u.Name).FirstOrDefault(),
            AuthorEmail = context.Users.Where(u => u.Id == n.AuthorUserId).Select(u => u.Email).FirstOrDefault(),
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt,
        });

    public static async Task<SupportNoteDto> ReadAsync(IMizanDbContext context, Guid id, CancellationToken cancellationToken) =>
        await Project(context.SupportNotes.AsNoTracking().Where(n => n.Id == id), context).FirstOrDefaultAsync(cancellationToken)
        ?? throw new EntityNotFoundException("Support note", id);
}

public record ListSupportNotesQuery(string SubjectType, Guid SubjectId) : IRequest<IReadOnlyList<SupportNoteDto>>;

public class ListSupportNotesQueryHandler : IRequestHandler<ListSupportNotesQuery, IReadOnlyList<SupportNoteDto>>
{
    private readonly IMizanDbContext _context;

    public ListSupportNotesQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<IReadOnlyList<SupportNoteDto>> Handle(ListSupportNotesQuery request, CancellationToken cancellationToken)
    {
        var subject = SupportNoteRead.ParseSubject(request.SubjectType);
        return await SupportNoteRead.Project(
                _context.SupportNotes.AsNoTracking()
                    .Where(n => n.SubjectType == subject && n.SubjectId == request.SubjectId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ThenBy(n => n.Id),
                _context)
            .ToListAsync(cancellationToken);
    }
}

public record AddSupportNoteCommand(string SubjectType, Guid SubjectId, string Body) : IRequest<SupportNoteDto>;

public class AddSupportNoteCommandValidator : AbstractValidator<AddSupportNoteCommand>
{
    public AddSupportNoteCommandValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(SupportNote.BodyMax);
    }
}

public class AddSupportNoteCommandHandler : IRequestHandler<AddSupportNoteCommand, SupportNoteDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddSupportNoteCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SupportNoteDto> Handle(AddSupportNoteCommand request, CancellationToken cancellationToken)
    {
        var author = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var subject = SupportNoteRead.ParseSubject(request.SubjectType);
        var exists = subject == SupportNoteSubject.User
            ? await _context.Users.AnyAsync(u => u.Id == request.SubjectId, cancellationToken)
            : await _context.Households.AnyAsync(h => h.Id == request.SubjectId, cancellationToken);
        if (!exists) throw new EntityNotFoundException(subject.ToString(), request.SubjectId);

        var now = DateTime.UtcNow;
        var note = new SupportNote
        {
            Id = Guid.CreateVersion7(),
            SubjectType = subject,
            SubjectId = request.SubjectId,
            Body = request.Body.Trim(),
            AuthorUserId = author,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.SupportNotes.Add(note);
        await _context.SaveChangesAsync(cancellationToken);
        return await SupportNoteRead.ReadAsync(_context, note.Id, cancellationToken);
    }
}

public record UpdateSupportNoteCommand(Guid Id, string Body) : IRequest<SupportNoteDto>;

public class UpdateSupportNoteCommandValidator : AbstractValidator<UpdateSupportNoteCommand>
{
    public UpdateSupportNoteCommandValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(SupportNote.BodyMax);
    }
}

public class UpdateSupportNoteCommandHandler : IRequestHandler<UpdateSupportNoteCommand, SupportNoteDto>
{
    private readonly IMizanDbContext _context;

    public UpdateSupportNoteCommandHandler(IMizanDbContext context) => _context = context;

    public async Task<SupportNoteDto> Handle(UpdateSupportNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _context.SupportNotes.FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Support note", request.Id);
        note.Body = request.Body.Trim();
        note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return await SupportNoteRead.ReadAsync(_context, note.Id, cancellationToken);
    }
}

public record DeleteSupportNoteCommand(Guid Id) : IRequest;

public class DeleteSupportNoteCommandHandler : IRequestHandler<DeleteSupportNoteCommand>
{
    private readonly IMizanDbContext _context;

    public DeleteSupportNoteCommandHandler(IMizanDbContext context) => _context = context;

    public async Task Handle(DeleteSupportNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _context.SupportNotes.FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Support note", request.Id);
        _context.SupportNotes.Remove(note);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
