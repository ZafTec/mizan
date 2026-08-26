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

        return await _context.AiChatThreads.AsNoTracking()
            .Where(t => t.UserId == userId)
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
            .Select(m => new AiChatMessageDto(m.Id, m.Role == AiChatRole.User, m.Content, m.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AiChatThreadDetailDto(thread.Id, thread.Title, thread.UpdatedAt, messages);
    }
}
