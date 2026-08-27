using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

public record AiChatThreadDto(Guid Id, string Title, DateTime UpdatedAt);

public record ListAiChatThreadsQuery(int Take = 30) : IRequest<IReadOnlyList<AiChatThreadDto>>;

public class ListAiChatThreadsQueryHandler
    : IRequestHandler<ListAiChatThreadsQuery, IReadOnlyList<AiChatThreadDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ListAiChatThreadsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AiChatThreadDto>> Handle(
        ListAiChatThreadsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        // Onboarding lives on its own page with its own tools. Listing it here
        // offered a conversation the chat screen cannot actually continue.
        return await _context.AiChatThreads.AsNoTracking()
            .Where(t => t.UserId == userId && t.Kind == AiChatThreadKind.Chat)
            .OrderByDescending(t => t.UpdatedAt)
            .Take(Math.Clamp(request.Take, 1, 100))
            .Select(t => new AiChatThreadDto(t.Id, t.Title, t.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}

public record AiChatThreadDetailDto(
    Guid Id,
    string Title,
    DateTime UpdatedAt,
    IReadOnlyList<AiChatMessageDto> Messages);

public record GetAiChatThreadQuery(Guid Id) : IRequest<AiChatThreadDetailDto>;

public class GetAiChatThreadQueryHandler : IRequestHandler<GetAiChatThreadQuery, AiChatThreadDetailDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAiChatThreadQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AiChatThreadDetailDto> Handle(
        GetAiChatThreadQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var thread = await _context.AiChatThreads.AsNoTracking()
            .Where(t => t.Id == request.Id && t.UserId == userId)
            .Select(t => new { t.Id, t.Title, t.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Chat thread", request.Id);

        var messages = await _context.AiChatMessages.AsNoTracking()
            .Where(m => m.ThreadId == thread.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AiChatMessageDto(m.Id, m.Role == AiChatRole.User, m.Content, m.CreatedAt, m.ImageUrl))
            .ToListAsync(cancellationToken);

        return new AiChatThreadDetailDto(thread.Id, thread.Title, thread.UpdatedAt, messages);
    }
}

/// <summary>
/// The user's setup conversation, if they have started one. Null rather than a
/// 404: not having begun is the normal first case, not an error.
/// </summary>
public record GetAiOnboardingThreadQuery : IRequest<AiChatThreadDetailDto?>;

public class GetAiOnboardingThreadQueryHandler
    : IRequestHandler<GetAiOnboardingThreadQuery, AiChatThreadDetailDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAiOnboardingThreadQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AiChatThreadDetailDto?> Handle(
        GetAiOnboardingThreadQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var thread = await _context.AiChatThreads.AsNoTracking()
            .Where(t => t.UserId == userId && t.Kind == AiChatThreadKind.Onboarding)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new { t.Id, t.Title, t.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (thread is null) return null;

        var messages = await _context.AiChatMessages.AsNoTracking()
            .Where(m => m.ThreadId == thread.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AiChatMessageDto(m.Id, m.Role == AiChatRole.User, m.Content, m.CreatedAt, m.ImageUrl))
            .ToListAsync(cancellationToken);

        return new AiChatThreadDetailDto(thread.Id, thread.Title, thread.UpdatedAt, messages);
    }
}
