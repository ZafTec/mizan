using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Application;

public class SendChatMessageCommandTests
{
    private readonly MizanDbContext _context;

    public SendChatMessageCommandTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new MizanDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldSendMessage_WhenUserIsParticipant()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        
        var relationship = new TrainerClientRelationship
        {
            Id = Guid.NewGuid(),
            TrainerId = senderId,
            ClientId = recipientId,
            Status = "active",
            CanMessage = true
        };
        _context.TrainerClientRelationships.Add(relationship);

        var conversation = new ChatConversation
        {
            Id = Guid.NewGuid(),
            TrainerClientRelationshipId = relationship.Id
        };
        _context.ChatConversations.Add(conversation);
        await _context.SaveChangesAsync();

        var command = new SendChatMessageCommand(conversation.Id, senderId, "Hello!");
        var handler = new SendChatMessageCommandHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RecipientId.Should().Be(recipientId);

        var message = await _context.ChatMessages.FindAsync(result.Id);
        message.Should().NotBeNull();
        message.Content.Should().Be("Hello!");
        message.SenderId.Should().Be(senderId);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUserIsNotParticipant()
    {
        // Arrange
        var relationship = new TrainerClientRelationship
        {
            Id = Guid.NewGuid(),
            TrainerId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            Status = "active",
            CanMessage = true
        };
        _context.TrainerClientRelationships.Add(relationship);

        var conversation = new ChatConversation
        {
            Id = Guid.NewGuid(),
            TrainerClientRelationshipId = relationship.Id
        };
        _context.ChatConversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Random user
        var command = new SendChatMessageCommand(conversation.Id, Guid.NewGuid(), "Hello!");
        var handler = new SendChatMessageCommandHandler(_context);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }
}
