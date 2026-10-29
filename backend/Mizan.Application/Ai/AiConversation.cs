using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Ai;
using Mizan.Domain.Entities;
using Mizan.Domain.Features;
using Mizan.Application.Features;

namespace Mizan.Application.Ai;

/// <summary>How long a conversation may be quiet before a client that continues "the current one" starts afresh (<c>Ai.ConversationGapHours</c>).</summary>
public interface IAiConversationSettings
{
    ValueTask<TimeSpan> GapAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The person's current conversation (docs/AI.md#app-context), for a client that has no thread of its
/// own to hold, such as a chat app: the newest chat thread, unless it has been quiet longer than the
/// gap, when the next message starts a new one. App context still waiting for an answer outlives the
/// gap, so the new thread starts with it. Nothing here knows which client asks or what the context is about.
/// </summary>
public sealed class AiConversations
{
    public const int MaxNoteLength = 1000;
    public const int MaxHoldMinutes = 60;

    private readonly IMizanDbContext _context;
    private readonly IAiConversationSettings _settings;
    private readonly TimeProvider _time;

    public AiConversations(IMizanDbContext context, IAiConversationSettings settings, TimeProvider time)
    {
        _context = context;
        _settings = settings;
        _time = time;
    }

    public DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>The newest chat thread, tracked, and whether it is still the current conversation.</summary>
    public async Task<(AiChatThread? Latest, bool Continues)> CurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var latest = await _context.AiChatThreads
            .Where(t => t.UserId == userId && t.Kind == AiChatThreadKind.Chat)
            .OrderByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null) return (null, false);
        var gap = await _settings.GapAsync(cancellationToken);
        return (latest, latest.UpdatedAt >= Now - gap);
    }

    /// <summary>The thread's waiting context, or null once it has expired.</summary>
    public string? Pending(AiChatThread? thread) =>
        thread is { PendingContext: { Length: > 0 } note, PendingContextUntil: { } until } && until > Now ? note : null;

    public AiConversationDto Describe(AiChatThread? latest, bool continues) =>
        new(continues ? latest!.Id : null, Pending(latest), Pending(latest) is null ? null : latest!.PendingContextUntil);

    /// <summary>The text of the thread's newest context message, so the same fact is not stored twice in a row.</summary>
    public Task<string?> LastNoteAsync(Guid threadId, CancellationToken cancellationToken) =>
        _context.AiChatMessages.AsNoTracking()
            .Where(m => m.ThreadId == threadId && m.Role == AiChatRole.Context)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.Content)
            .FirstOrDefaultAsync(cancellationToken);

    public AiChatThread NewThread(Guid userId, string title)
    {
        var now = Now;
        var thread = new AiChatThread
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Title = title,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.AiChatThreads.Add(thread);
        return thread;
    }

    public void AddNote(AiChatThread thread, string note, DateTime at) =>
        _context.AiChatMessages.Add(new AiChatMessage
        {
            Id = Guid.CreateVersion7(),
            ThreadId = thread.Id,
            Role = AiChatRole.Context,
            Content = note,
            CreatedAt = at,
        });
}

/// <summary>The current conversation and any app context waiting in it (<c>GET /api/Ai/conversation</c>).</summary>
public record GetAiConversationQuery : IRequest<AiConversationDto>;

public sealed class GetAiConversationQueryHandler : IRequestHandler<GetAiConversationQuery, AiConversationDto>
{
    private readonly AiConversations _conversations;
    private readonly ICurrentUserService _currentUser;

    public GetAiConversationQueryHandler(AiConversations conversations, ICurrentUserService currentUser)
    {
        _conversations = conversations;
        _currentUser = currentUser;
    }

    public async Task<AiConversationDto> Handle(GetAiConversationQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var (latest, continues) = await _conversations.CurrentAsync(userId, cancellationToken);
        return _conversations.Describe(latest, continues);
    }
}

/// <summary>
/// A fact the client reports into the current conversation, with no model call (<c>POST /api/Ai/conversation/context</c>).
/// After the gap it opens a new thread; a positive hold keeps it waiting for the person's next message.
/// </summary>
[RequiresFeature(FeatureFlags.AiAssistant)]
public record AddAiContextNoteCommand(string Note, int? HoldMinutes = null) : IRequest<AiConversationDto>;

public class AddAiContextNoteCommandValidator : AbstractValidator<AddAiContextNoteCommand>
{
    public AddAiContextNoteCommandValidator()
    {
        RuleFor(c => c.Note).NotEmpty().MaximumLength(AiConversations.MaxNoteLength);
        RuleFor(c => c.HoldMinutes).InclusiveBetween(0, AiConversations.MaxHoldMinutes).When(c => c.HoldMinutes is not null);
    }
}

public sealed class AddAiContextNoteCommandHandler : IRequestHandler<AddAiContextNoteCommand, AiConversationDto>
{
    /// <summary>A thread opened by a note is renamed by the person's first message.</summary>
    public const string UntitledThread = "New conversation";

    private readonly IMizanDbContext _context;
    private readonly AiConversations _conversations;
    private readonly ICurrentUserService _currentUser;

    public AddAiContextNoteCommandHandler(IMizanDbContext context, AiConversations conversations, ICurrentUserService currentUser)
    {
        _context = context;
        _conversations = conversations;
        _currentUser = currentUser;
    }

    public async Task<AiConversationDto> Handle(AddAiContextNoteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var note = request.Note.Trim();
        var (latest, continues) = await _conversations.CurrentAsync(userId, cancellationToken);
        var thread = continues ? latest! : _conversations.NewThread(userId, UntitledThread);
        var now = _conversations.Now;

        if (!continues || await _conversations.LastNoteAsync(thread.Id, cancellationToken) != note)
            _conversations.AddNote(thread, note, now);

        if (request.HoldMinutes is > 0 and var minutes)
        {
            thread.PendingContext = note;
            thread.PendingContextUntil = now.AddMinutes(minutes);
        }
        else
        {
            thread.PendingContext = null;
            thread.PendingContextUntil = null;
        }
        thread.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        return _conversations.Describe(thread, continues: true);
    }
}
