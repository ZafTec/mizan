using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record SendChatMessageCommand(Guid ConversationId, Guid UserId, string Content, string MessageType = "text") : IRequest<SendChatMessageResult>;

public record SendChatMessageResult
{
    public Guid Id { get; init; }
    public DateTime SentAt { get; init; }
    public Guid RecipientId { get; init; }
}

public class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.MessageType).Must(x => new[] { "text", "image", "workout_share", "recipe_share" }.Contains(x));
    }
}

public class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, SendChatMessageResult>
{
    private readonly IMizanDbContext _context;

    public SendChatMessageCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<SendChatMessageResult> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _context.ChatConversations
            .Include(c => c.Relationship)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (conversation == null)
        {
            throw new InvalidOperationException("Conversation not found");
        }

        // Verify user is part of this conversation
        var relationship = conversation.Relationship;
        if (relationship.TrainerId != request.UserId && relationship.ClientId != request.UserId)
        {
            throw new UnauthorizedAccessException("User is not part of this conversation");
        }

        if (!relationship.CanMessage)
        {
            throw new InvalidOperationException("Messaging is disabled for this relationship");
        }

        // Determine recipient
        var recipientId = relationship.TrainerId == request.UserId
            ? relationship.ClientId
            : relationship.TrainerId;

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = request.ConversationId,
            SenderId = request.UserId,
            Content = request.Content,
            MessageType = request.MessageType,
            SentAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);

        conversation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new SendChatMessageResult
        {
            Id = message.Id,
            SentAt = message.SentAt,
            RecipientId = recipientId
        };
    }
}
