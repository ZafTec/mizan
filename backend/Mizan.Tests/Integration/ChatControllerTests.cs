using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mizan.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class ChatControllerTests
{
    private readonly ApiTestFixture _fixture;

    public ChatControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TrainerCanSendMessageToClient()
    {
        await _fixture.ResetDatabaseAsync();

        var trainerId = Guid.NewGuid();
        var trainerEmail = $"trainer-{trainerId:N}@example.com";
        await _fixture.SeedUserAsync(trainerId, trainerEmail, role: "trainer");

        var clientId = Guid.NewGuid();
        var clientEmail = $"client-{clientId:N}@example.com";
        await _fixture.SeedUserAsync(clientId, clientEmail);

        var (relationshipId, conversationId) = await SeedTrainerClientConversation(trainerId, clientId);

        using var trainerClient = _fixture.CreateAuthenticatedClient(trainerId, trainerEmail, role: "trainer");

        var sendResponse = await trainerClient.PostAsJsonAsync("/api/Chat/send", new
        {
            ConversationId = conversationId,
            Content = "Hello, how is your training going?"
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await trainerClient.GetAsync($"/api/Chat/{relationshipId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var conversation = await getResponse.Content.ReadFromJsonAsync<ConversationResult>();
        conversation.Should().NotBeNull();
        conversation!.Messages.Should().Contain(m => m.Content == "Hello, how is your training going?");
    }

    [Fact]
    public async Task NonParticipantCannotSendMessage()
    {
        await _fixture.ResetDatabaseAsync();

        var trainerId = Guid.NewGuid();
        var trainerEmail = $"trainer-{trainerId:N}@example.com";
        await _fixture.SeedUserAsync(trainerId, trainerEmail, role: "trainer");

        var clientId = Guid.NewGuid();
        var clientEmail = $"client-{clientId:N}@example.com";
        await _fixture.SeedUserAsync(clientId, clientEmail);

        var intruderId = Guid.NewGuid();
        var intruderEmail = $"intruder-{intruderId:N}@example.com";
        await _fixture.SeedUserAsync(intruderId, intruderEmail);

        var (_, conversationId) = await SeedTrainerClientConversation(trainerId, clientId);

        using var intruderClient = _fixture.CreateAuthenticatedClient(intruderId, intruderEmail);

        var sendResponse = await intruderClient.PostAsJsonAsync("/api/Chat/send", new
        {
            ConversationId = conversationId,
            Content = "Unauthorized message"
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(Guid RelationshipId, Guid ConversationId)> SeedTrainerClientConversation(Guid trainerId, Guid clientId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var relationship = new TrainerClientRelationship
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            ClientId = clientId,
            Status = "active",
            CanMessage = true,
            CreatedAt = DateTime.UtcNow
        };
        db.TrainerClientRelationships.Add(relationship);

        var conversation = new ChatConversation
        {
            Id = Guid.NewGuid(),
            TrainerClientRelationshipId = relationship.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ChatConversations.Add(conversation);
        await db.SaveChangesAsync();

        return (relationship.Id, conversation.Id);
    }

    private sealed record ConversationResult(Guid Id, List<MessageResult> Messages);
    private sealed record MessageResult(Guid Id, string Content, Guid SenderId);
}
