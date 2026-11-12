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

    [Fact]
    public async Task AClientReadsALongConversationAPageAtATime_NewestFirst()
    {
        await _fixture.ResetDatabaseAsync();

        var trainerId = Guid.NewGuid();
        await _fixture.SeedUserAsync(trainerId, $"trainer-{trainerId:N}@example.com", role: "trainer");
        var clientId = Guid.NewGuid();
        var clientEmail = $"client-{clientId:N}@example.com";
        await _fixture.SeedUserAsync(clientId, clientEmail);
        var (relationshipId, conversationId) = await SeedTrainerClientConversation(trainerId, clientId);

        // Five messages a minute apart; the last two share a timestamp, so the id breaks the tie.
        var start = DateTime.UtcNow.AddHours(-1);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            for (var i = 0; i < 5; i++)
            {
                db.ChatMessages.Add(new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderId = i % 2 == 0 ? trainerId : clientId,
                    Content = $"message {i}",
                    SentAt = start.AddMinutes(Math.Min(i, 3)),
                });
            }
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateAuthenticatedClient(clientId, clientEmail);

        var whole = await client.GetFromJsonAsync<ConversationResult>($"/api/Chat/{relationshipId}");
        whole!.Messages.Select(m => m.Content).Should().HaveCount(5).And.StartWith("message 0").And.ContainInOrder("message 0", "message 1", "message 2");
        whole.HasOlder.Should().BeFalse();

        var seen = new List<string>();
        var latest = await client.GetFromJsonAsync<ConversationResult>($"/api/Chat/{relationshipId}?take=2");
        latest!.Messages.Should().HaveCount(2);
        latest.HasOlder.Should().BeTrue();
        seen.InsertRange(0, latest.Messages.Select(m => m.Content));

        var middle = await client.GetFromJsonAsync<ConversationResult>($"/api/Chat/{relationshipId}?take=2&before={latest.Messages[0].Id}");
        middle!.Messages.Should().HaveCount(2);
        middle.HasOlder.Should().BeTrue();
        seen.InsertRange(0, middle.Messages.Select(m => m.Content));

        var oldest = await client.GetFromJsonAsync<ConversationResult>($"/api/Chat/{relationshipId}?take=2&before={middle.Messages[0].Id}");
        oldest!.Messages.Select(m => m.Content).Should().Equal("message 0");
        oldest.HasOlder.Should().BeFalse();
        seen.InsertRange(0, oldest.Messages.Select(m => m.Content));

        // The pages put together are the whole conversation, once each, in the order it was read whole.
        seen.Should().Equal(whole.Messages.Select(m => m.Content));
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

    private sealed record ConversationResult(Guid Id, List<MessageResult> Messages, bool HasOlder);
    private sealed record MessageResult(Guid Id, string Content, Guid SenderId);
}
