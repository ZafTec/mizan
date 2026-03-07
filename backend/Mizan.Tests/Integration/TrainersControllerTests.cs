using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class TrainersControllerTests
{
    private readonly ApiTestFixture _fixture;

    public TrainersControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SendRequest_ReturnsBadRequest_WhenTargetIsNotTrainer()
    {
        await _fixture.ResetDatabaseAsync();

        var clientId = Guid.NewGuid();
        var clientEmail = $"client-{clientId:N}@example.com";
        await _fixture.SeedUserAsync(clientId, clientEmail);

        var regularUserId = Guid.NewGuid();
        var regularUserEmail = $"user-{regularUserId:N}@example.com";
        await _fixture.SeedUserAsync(regularUserId, regularUserEmail);

        using var client = _fixture.CreateAuthenticatedClient(clientId, clientEmail);

        var response = await client.PostAsJsonAsync("/api/Trainers/request", new { TrainerId = regularUserId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetClients_ReturnsForbidden_WhenUserIsNotTrainer()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"user-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, role: "user");

        using var client = _fixture.CreateAuthenticatedClient(userId, email, role: "user");

        var response = await client.GetAsync("/api/Trainers/clients");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Respond_ReturnsForbidden_WhenTrainerDoesNotOwnRelationship()
    {
        await _fixture.ResetDatabaseAsync();

        var ownerTrainerId = Guid.NewGuid();
        var ownerTrainerEmail = $"trainer-{ownerTrainerId:N}@example.com";
        await _fixture.SeedUserAsync(ownerTrainerId, ownerTrainerEmail, role: "trainer");

        var otherTrainerId = Guid.NewGuid();
        var otherTrainerEmail = $"trainer-{otherTrainerId:N}@example.com";
        await _fixture.SeedUserAsync(otherTrainerId, otherTrainerEmail, role: "trainer");

        var clientId = Guid.NewGuid();
        var clientEmail = $"client-{clientId:N}@example.com";
        await _fixture.SeedUserAsync(clientId, clientEmail);

        var relationshipId = await SeedRelationshipAsync(ownerTrainerId, clientId, status: "pending");

        using var client = _fixture.CreateAuthenticatedClient(otherTrainerId, otherTrainerEmail, role: "trainer");

        var response = await client.PostAsJsonAsync("/api/Trainers/respond", new
        {
            RelationshipId = relationshipId,
            Accept = true,
            CanViewNutrition = true,
            CanViewWorkouts = true,
            CanViewMeasurements = false,
            CanMessage = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPendingRequests_ReturnsClientIdentityFields()
    {
        await _fixture.ResetDatabaseAsync();

        var trainerId = Guid.NewGuid();
        var trainerEmail = $"trainer-{trainerId:N}@example.com";
        await _fixture.SeedUserAsync(trainerId, trainerEmail, role: "trainer");

        var clientId = Guid.NewGuid();
        var clientEmail = $"client-{clientId:N}@example.com";
        var seededClient = await _fixture.SeedUserAsync(clientId, clientEmail);

        await SeedRelationshipAsync(trainerId, clientId, status: "pending");

        using var client = _fixture.CreateAuthenticatedClient(trainerId, trainerEmail, role: "trainer");

        var response = await client.GetAsync("/api/Trainers/requests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PendingRequestResult>>();
        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle();
        result.Items[0].ClientEmail.Should().Be(clientEmail);
        result.Items[0].ClientName.Should().Be(seededClient.Name);
    }

    private async Task<Guid> SeedRelationshipAsync(Guid trainerId, Guid clientId, string status)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var relationship = new TrainerClientRelationship
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            ClientId = clientId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            StartedAt = status == "active" ? DateTime.UtcNow : null,
        };

        db.TrainerClientRelationships.Add(relationship);
        await db.SaveChangesAsync();

        return relationship.Id;
    }

    private sealed record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);
    private sealed record PendingRequestResult(Guid RelationshipId, Guid ClientId, string? ClientName, string? ClientEmail, DateTime RequestedAt);
}
