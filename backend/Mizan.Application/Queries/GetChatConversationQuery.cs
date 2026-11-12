using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

/// <summary>
/// One page of a coaching conversation, newest last: the <paramref name="Take"/> messages before the message
/// <paramref name="Before"/>, or the latest ones without it. A conversation is read a page at a time so a long
/// one does not load whole.
/// </summary>
public record GetChatConversationQuery(Guid RelationshipId, Guid? Before = null, int Take = GetChatConversationQuery.DefaultTake)
    : IRequest<ChatConversationDto?>
{
    public const int DefaultTake = 50;
    public const int MaxTake = 200;
}

/// <summary>
/// <paramref name="Messages"/> oldest first. <paramref name="HasOlder"/> is true when messages before the first
/// one remain; the first message's id is the <c>before</c> of the next page.
/// </summary>
public record ChatConversationDto(
    Guid Id,
    Guid RelationshipId,
    List<ChatMessageDto> Messages,
    bool HasOlder
);

public record ChatMessageDto(
    Guid Id,
    Guid SenderId,
    string Content,
    DateTime SentAt
);

public class GetChatConversationQueryHandler : IRequestHandler<GetChatConversationQuery, ChatConversationDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetChatConversationQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ChatConversationDto?> Handle(GetChatConversationQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return null;
        }

        // Only the trainer and the client in the relationship may read it.
        var conversation = await _context.ChatConversations.AsNoTracking()
            .Where(c => c.TrainerClientRelationshipId == request.RelationshipId
                && (c.Relationship.TrainerId == userId || c.Relationship.ClientId == userId))
            .Select(c => new { c.Id, c.TrainerClientRelationshipId })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation == null)
        {
            return null;
        }

        var messages = _context.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversation.Id);
        if (request.Before is { } beforeId)
        {
            // Keyset on (sent_at, id): the cursor is a message of this conversation; an unknown one reads nothing older.
            var cursor = await messages.Where(m => m.Id == beforeId)
                .Select(m => new { m.SentAt, m.Id })
                .FirstOrDefaultAsync(cancellationToken);
            if (cursor is null)
            {
                return new ChatConversationDto(conversation.Id, conversation.TrainerClientRelationshipId, [], false);
            }
            messages = messages.Where(m => m.SentAt < cursor.SentAt || (m.SentAt == cursor.SentAt && m.Id.CompareTo(cursor.Id) < 0));
        }

        var take = Math.Clamp(request.Take, 1, GetChatConversationQuery.MaxTake);
        var page = await messages
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(take + 1)
            .Select(m => new ChatMessageDto(m.Id, m.SenderId, m.Content, m.SentAt))
            .ToListAsync(cancellationToken);

        var hasOlder = page.Count > take;
        var shown = page.Take(take).Reverse().ToList();
        return new ChatConversationDto(conversation.Id, conversation.TrainerClientRelationshipId, shown, hasOlder);
    }
}
