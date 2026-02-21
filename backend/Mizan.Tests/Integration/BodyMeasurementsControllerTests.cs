using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class BodyMeasurementsControllerTests
{
    private readonly ApiTestFixture _fixture;

    public BodyMeasurementsControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UserCanLogAndRetrieveBodyMeasurements()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"athlete-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var logCommand = new
        {
            Date = DateTime.UtcNow,
            WeightKg = 80.5m,
            BodyFatPercentage = 18.5m,
            Notes = "Morning measurement"
        };

        var logResponse = await client.PostAsJsonAsync("/api/BodyMeasurements", logCommand);
        logResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var measurementId = await logResponse.Content.ReadFromJsonAsync<Guid>();
        measurementId.Should().NotBe(Guid.Empty);

        var listResponse = await client.GetAsync("/api/BodyMeasurements");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var measurements = await listResponse.Content.ReadFromJsonAsync<MeasurementPagedResult>();
        measurements.Should().NotBeNull();
        measurements!.Items.Should().Contain(m => m.WeightKg == 80.5m);
    }

    [Fact]
    public async Task UserCanDeleteOwnMeasurement()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"athlete-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var logCommand = new { Date = DateTime.UtcNow, WeightKg = 75.0m };
        var logResponse = await client.PostAsJsonAsync("/api/BodyMeasurements", logCommand);
        logResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var measurementId = await logResponse.Content.ReadFromJsonAsync<Guid>();

        var deleteResponse = await client.DeleteAsync($"/api/BodyMeasurements/{measurementId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await client.GetAsync("/api/BodyMeasurements");
        var measurements = await listResponse.Content.ReadFromJsonAsync<MeasurementPagedResult>();
        measurements!.Items.Should().NotContain(m => m.Id == measurementId);
    }

    [Fact]
    public async Task UserCannotSeeOtherUsersMeasurements()
    {
        await _fixture.ResetDatabaseAsync();

        var userId1 = Guid.NewGuid();
        var email1 = $"athlete1-{userId1:N}@example.com";
        await _fixture.SeedUserAsync(userId1, email1);

        var userId2 = Guid.NewGuid();
        var email2 = $"athlete2-{userId2:N}@example.com";
        await _fixture.SeedUserAsync(userId2, email2);

        using var client1 = _fixture.CreateAuthenticatedClient(userId1, email1);
        await client1.PostAsJsonAsync("/api/BodyMeasurements", new { Date = DateTime.UtcNow, WeightKg = 90.0m });

        using var client2 = _fixture.CreateAuthenticatedClient(userId2, email2);
        var listResponse = await client2.GetAsync("/api/BodyMeasurements");
        var measurements = await listResponse.Content.ReadFromJsonAsync<MeasurementPagedResult>();
        measurements!.Items.Should().BeEmpty();
    }

    private sealed record MeasurementPagedResult(List<MeasurementItem> Items, int TotalCount);
    private sealed record MeasurementItem(Guid Id, decimal? WeightKg, decimal? BodyFatPercentage, DateTime Date);
}
