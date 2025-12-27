using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetChatConversationQuery(Guid RelationshipId) : IRequest<ChatConversationDto?>;

public record ChatConversationDto(
    Guid Id,
    Guid RelationshipId,
    List<ChatMessageDto> Messages
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
        if (!_currentUser.UserId.HasValue)
        {
            return null;
        }

        var conversation = await _context.ChatConversations
            .Include(c => c.Messages)
            .Include(c => c.Relationship)
            .FirstOrDefaultAsync(c => c.TrainerClientRelationshipId == request.RelationshipId, cancellationToken);

        if (conversation == null)
        {
            return null;
        }

        // Authorization: User must be trainer or client in the relationship
        var relationship = conversation.Relationship;
        if (relationship.TrainerId != _currentUser.UserId.Value && relationship.ClientId != _currentUser.UserId.Value)
        {
            return null;
        }

        return new ChatConversationDto(
            conversation.Id,
            conversation.TrainerClientRelationshipId,
            conversation.Messages.OrderBy(m => m.SentAt).Select(m => new ChatMessageDto(
                m.Id,
                m.SenderId,
                m.Content,
                m.SentAt
            )).ToList()
        );
    }
}
