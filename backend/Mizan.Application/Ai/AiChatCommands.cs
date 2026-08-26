using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Ai.Tools;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

public record AiChatMessageDto(Guid Id, bool FromUser, string Content, DateTime CreatedAt);

public record AiChatTurnDto(Guid ThreadId, string Title, AiChatMessageDto Reply);

/// <summary>
/// One turn. A null thread starts a new one, which is what the first message
/// on an empty screen does.
/// </summary>
public record SendAiChatMessageCommand(Guid? ThreadId, string Message) : IRequest<AiChatTurnDto>;

public class SendAiChatMessageCommandValidator : AbstractValidator<SendAiChatMessageCommand>
{
    public SendAiChatMessageCommandValidator()
    {
        RuleFor(c => c.Message).NotEmpty().MaximumLength(4000);
    }
}

public class SendAiChatMessageCommandHandler : IRequestHandler<SendAiChatMessageCommand, AiChatTurnDto>
{
    /// <summary>
    /// How far back a turn replays. Every earlier turn is prompt tokens paid
    /// for again, so this is a cost decision, not a memory one: ten turns is
    /// enough to follow a thread and cheap enough not to notice.
    /// </summary>
    private const int HistoryTurns = 10;

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INutritionAiService _ai;

    public SendAiChatMessageCommandHandler(
        IMizanDbContext context, ICurrentUserService currentUser, INutritionAiService ai)
    {
        _context = context;
        _currentUser = currentUser;
        _ai = ai;
    }

    public async Task<AiChatTurnDto> Handle(
        SendAiChatMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var message = request.Message.Trim();

        var thread = await ResolveThreadAsync(userId, request.ThreadId, message, cancellationToken);
        var history = await HistoryAsync(thread.Id, cancellationToken);

        // The provider call happens before either message is written: a call
        // that fails on quota or an outage must not leave a half turn behind.
        var turn = await _ai.GetNutritionAdviceAsync(userId, message, history, cancellationToken);

        var now = DateTime.UtcNow;
        var reply = new AiChatMessage
        {
            Id = Guid.CreateVersion7(),
            ThreadId = thread.Id,
            Role = AiChatRole.Assistant,
            Content = turn.Content,
            PromptVersionId = turn.PromptVersionId,
            CreatedAt = now.AddMilliseconds(1),
        };

        _context.AiChatMessages.Add(new AiChatMessage
        {
            Id = Guid.CreateVersion7(),
            ThreadId = thread.Id,
            Role = AiChatRole.User,
            Content = message,
            CreatedAt = now,
        });
        _context.AiChatMessages.Add(reply);
        thread.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return new AiChatTurnDto(
            thread.Id,
            thread.Title,
            new AiChatMessageDto(reply.Id, false, reply.Content, reply.CreatedAt));
    }

    private async Task<AiChatThread> ResolveThreadAsync(
        Guid userId, Guid? threadId, string firstMessage, CancellationToken cancellationToken)
    {
        if (threadId is { } id)
        {
            return await _context.AiChatThreads
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
                ?? throw new EntityNotFoundException("Chat thread", id);
        }

        var thread = new AiChatThread
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Title = Title(firstMessage),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _context.AiChatThreads.Add(thread);
        return thread;
    }

    private async Task<IReadOnlyList<AiChatHistoryTurn>> HistoryAsync(
        Guid threadId, CancellationToken cancellationToken)
    {
        var recent = await _context.AiChatMessages.AsNoTracking()
            .Where(m => m.ThreadId == threadId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(HistoryTurns)
            .Select(m => new { m.Role, m.Content, m.CreatedAt })
            .ToListAsync(cancellationToken);

        return recent
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AiChatHistoryTurn(m.Role == AiChatRole.User, m.Content))
            .ToList();
    }

    /// <summary>The opening question, trimmed to something that fits a list.</summary>
    private static string Title(string message)
    {
        var single = string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return single.Length <= 60 ? single : single[..57].TrimEnd() + "…";
    }
}

public record DeleteAiChatThreadCommand(Guid Id) : IRequest;

public class DeleteAiChatThreadCommandHandler : IRequestHandler<DeleteAiChatThreadCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteAiChatThreadCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteAiChatThreadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var deleted = await _context.AiChatThreads
            .Where(t => t.Id == request.Id && t.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0) throw new EntityNotFoundException("Chat thread", request.Id);
    }
}


public record AiOnboardingTurnDto(
    Guid ThreadId,
    AiChatMessageDto Reply,
    IReadOnlyList<AiToolInvocation> Performed);

/// <summary>
/// One turn of onboarding. Same thread store as chat, because it is the same
/// conversation from the user's point of view - what differs is that the model
/// has tools (docs/REFOCUS.md §10).
/// </summary>
public record SendAiOnboardingMessageCommand(Guid? ThreadId, string Message)
    : IRequest<AiOnboardingTurnDto>;

public class SendAiOnboardingMessageCommandValidator : AbstractValidator<SendAiOnboardingMessageCommand>
{
    public SendAiOnboardingMessageCommandValidator()
    {
        RuleFor(c => c.Message).NotEmpty().MaximumLength(4000);
    }
}

public class SendAiOnboardingMessageCommandHandler
    : IRequestHandler<SendAiOnboardingMessageCommand, AiOnboardingTurnDto>
{
    private const int HistoryTurns = 12;
    private const string ThreadTitle = "Getting set up";

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INutritionAiService _ai;

    public SendAiOnboardingMessageCommandHandler(
        IMizanDbContext context, ICurrentUserService currentUser, INutritionAiService ai)
    {
        _context = context;
        _currentUser = currentUser;
        _ai = ai;
    }

    public async Task<AiOnboardingTurnDto> Handle(
        SendAiOnboardingMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var message = request.Message.Trim();

        var thread = await ResolveThreadAsync(userId, request.ThreadId, cancellationToken);

        var history = await _context.AiChatMessages.AsNoTracking()
            .Where(m => m.ThreadId == thread.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(HistoryTurns)
            .Select(m => new { m.Role, m.Content, m.CreatedAt })
            .ToListAsync(cancellationToken);

        var turn = await _ai.RunOnboardingTurnAsync(
            userId,
            message,
            history.OrderBy(m => m.CreatedAt)
                .Select(m => new AiChatHistoryTurn(m.Role == AiChatRole.User, m.Content))
                .ToList(),
            cancellationToken);

        var now = DateTime.UtcNow;
        var reply = new AiChatMessage
        {
            Id = Guid.CreateVersion7(),
            ThreadId = thread.Id,
            Role = AiChatRole.Assistant,
            Content = turn.Content,
            PromptVersionId = turn.PromptVersionId,
            CreatedAt = now.AddMilliseconds(1),
        };

        _context.AiChatMessages.Add(new AiChatMessage
        {
            Id = Guid.CreateVersion7(),
            ThreadId = thread.Id,
            Role = AiChatRole.User,
            Content = message,
            CreatedAt = now,
        });
        _context.AiChatMessages.Add(reply);
        thread.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return new AiOnboardingTurnDto(
            thread.Id,
            new AiChatMessageDto(reply.Id, false, reply.Content, reply.CreatedAt),
            turn.Performed);
    }

    private async Task<AiChatThread> ResolveThreadAsync(
        Guid userId, Guid? threadId, CancellationToken cancellationToken)
    {
        if (threadId is { } id)
        {
            return await _context.AiChatThreads
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
                ?? throw new EntityNotFoundException("Chat thread", id);
        }

        var thread = new AiChatThread
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Title = ThreadTitle,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _context.AiChatThreads.Add(thread);
        return thread;
    }
}


public record AiTrainerAnswerDto(
    Guid ThreadId,
    AiChatMessageDto Reply,
    IReadOnlyList<string> AxesSeen);

/// <summary>
/// A coach asking about one client. The relationship is checked before the
/// question reaches the model, and the answer is billed to the coach.
/// </summary>
public record AskAboutClientCommand(Guid ClientId, Guid? ThreadId, string Message)
    : IRequest<AiTrainerAnswerDto>;

public class AskAboutClientCommandValidator : AbstractValidator<AskAboutClientCommand>
{
    public AskAboutClientCommandValidator()
    {
        RuleFor(c => c.ClientId).NotEmpty();
        RuleFor(c => c.Message).NotEmpty().MaximumLength(4000);
    }
}

public class AskAboutClientCommandHandler : IRequestHandler<AskAboutClientCommand, AiTrainerAnswerDto>
{
    private const int HistoryTurns = 10;

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ITrainerAuthorizationService _trainers;
    private readonly INutritionAiService _ai;

    public AskAboutClientCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        ITrainerAuthorizationService trainers,
        INutritionAiService ai)
    {
        _context = context;
        _currentUser = currentUser;
        _trainers = trainers;
        _ai = ai;
    }

    public async Task<AiTrainerAnswerDto> Handle(
        AskAboutClientCommand request, CancellationToken cancellationToken)
    {
        var trainerId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        // Throws when there is no active relationship. The axis question comes
        // later, in IDataAccessPolicy; this is only "is this your client".
        await _trainers.GetRelationshipForCurrentTrainerAndClientAsync(
            request.ClientId, requireActive: true, cancellationToken);

        var thread = await ResolveThreadAsync(trainerId, request, cancellationToken);

        var history = await _context.AiChatMessages.AsNoTracking()
            .Where(m => m.ThreadId == thread.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(HistoryTurns)
            .Select(m => new { m.Role, m.Content, m.CreatedAt })
            .ToListAsync(cancellationToken);

        var message = request.Message.Trim();
        var answer = await _ai.AskAboutClientAsync(
            trainerId,
            request.ClientId,
            message,
            history.OrderBy(m => m.CreatedAt)
                .Select(m => new AiChatHistoryTurn(m.Role == AiChatRole.User, m.Content))
                .ToList(),
            cancellationToken);

        var now = DateTime.UtcNow;
        var reply = new AiChatMessage
        {
            Id = Guid.CreateVersion7(),
            ThreadId = thread.Id,
            Role = AiChatRole.Assistant,
            Content = answer.Content,
            PromptVersionId = answer.PromptVersionId,
            CreatedAt = now.AddMilliseconds(1),
        };

        _context.AiChatMessages.Add(new AiChatMessage
        {
            Id = Guid.CreateVersion7(),
            ThreadId = thread.Id,
            Role = AiChatRole.User,
            Content = message,
            CreatedAt = now,
        });
        _context.AiChatMessages.Add(reply);
        thread.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return new AiTrainerAnswerDto(
            thread.Id,
            new AiChatMessageDto(reply.Id, false, reply.Content, reply.CreatedAt),
            answer.AxesSeen);
    }

    private async Task<AiChatThread> ResolveThreadAsync(
        Guid trainerId, AskAboutClientCommand request, CancellationToken cancellationToken)
    {
        if (request.ThreadId is { } id)
        {
            return await _context.AiChatThreads
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == trainerId, cancellationToken)
                ?? throw new EntityNotFoundException("Chat thread", id);
        }

        var clientName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == request.ClientId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var thread = new AiChatThread
        {
            Id = Guid.CreateVersion7(),
            // The coach owns the thread, not the client: it is the coach's
            // working notes, and a client must not find it in their history.
            UserId = trainerId,
            Title = string.IsNullOrWhiteSpace(clientName) ? "Client" : clientName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _context.AiChatThreads.Add(thread);
        return thread;
    }
}
