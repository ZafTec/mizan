using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;

namespace Mizan.Api.Hubs;

public record ChatMessageDto
{
    public Guid ConversationId { get; init; }
    public string Content { get; init; } = string.Empty;
    public string MessageType { get; init; } = "text";
}

public record IncomingChatMessage
{
    public Guid Id { get; init; }
    public Guid SenderId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string MessageType { get; init; } = "text";
    public DateTime SentAt { get; init; }
}

public record WorkoutProgressUpdate
{
    public Guid WorkoutId { get; init; }
    public int CompletedSets { get; init; }
    public int TotalSets { get; init; }
    public int? CaloriesBurned { get; init; }
}

[Authorize]
public class ChatHub : Hub
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatHub> _logger;
    private readonly IMizanDbContext _context;

    public ChatHub(IMediator mediator, ILogger<ChatHub> logger, IMizanDbContext context)
    {
        _mediator = mediator;
        _logger = logger;
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} connected to chat hub", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} disconnected from chat hub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        // Authorization: Validate user is participant in this conversation
        var conversation = await _context.ChatConversations
            .Include(c => c.Relationship)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
        {
            throw new HubException("Conversation not found");
        }

        var relationship = conversation.Relationship;
        if (relationship.TrainerId != userId.Value && relationship.ClientId != userId.Value)
        {
            throw new HubException("Access denied to this conversation");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
    }

    public async Task SendMessage(ChatMessageDto message)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        try
        {
            var command = new SendChatMessageCommand(message.ConversationId, userId.Value, message.Content, message.MessageType);

            var result = await _mediator.Send(command);

            var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "User";

            var outgoingMessage = new IncomingChatMessage
            {
                Id = result.Id,
                SenderId = userId.Value,
                SenderName = userName,
                Content = message.Content,
                MessageType = message.MessageType,
                SentAt = result.SentAt
            };

            // Send to conversation group
            await Clients.Group($"conversation_{message.ConversationId}")
                .SendAsync("ReceiveMessage", outgoingMessage);

            // Also send notification to recipient's user group
            await Clients.Group($"user_{result.RecipientId}")
                .SendAsync("NewMessageNotification", new
                {
                    ConversationId = message.ConversationId,
                    SenderId = userId.Value,
                    SenderName = userName,
                    Preview = message.Content.Length > 50
                        ? message.Content[..50] + "..."
                        : message.Content
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message for user {UserId}", userId);
            throw new HubException("Failed to send message");
        }
    }

    public async Task SyncWorkoutProgress(WorkoutProgressUpdate progress)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User not authenticated");
        }

        // Broadcast to all devices of the same user
        await Clients.Group($"user_{userId}")
            .SendAsync("WorkoutProgressUpdated", progress);
    }

    public async Task TypingIndicator(Guid conversationId, bool isTyping)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        await Clients.OthersInGroup($"conversation_{conversationId}")
            .SendAsync("UserTyping", new { UserId = userId.Value, IsTyping = isTyping });
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
