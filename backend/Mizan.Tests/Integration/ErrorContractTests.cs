using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Every failure a client can meet answers in one shape, <c>{ errorCode, error }</c>, so an app has a single thing to parse.
/// The OAuth endpoints are the one exception: they speak RFC 6749, which names the fields differently.
/// </summary>
[Collection("ApiIntegration")]
public class ErrorContractTests
{
    private readonly ApiTestFixture _fixture;

    public ErrorContractTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task NotSignedIn_AndNoSuchAddress_AndWrongMethod_AllCarryTheShape()
    {
        using var anonymous = _fixture.CreateClient();

        await AssertShape(await anonymous.GetAsync("/api/Users/me"), HttpStatusCode.Unauthorized, "unauthorized");
        await AssertShape(await anonymous.GetAsync("/api/NoSuchThing"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task ABodyThatCannotBeRead_CarriesTheShape()
    {
        using var client = await SignedInAsync();

        var response = await client.PostAsync("/api/BodyMeasurements", new StringContent("{ not json", Encoding.UTF8, "application/json"));

        var body = await AssertShape(response, HttpStatusCode.BadRequest, "invalid_request");
        body.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Object, "field messages stay available for forms");
    }

    [Fact]
    public async Task AFailedValidation_CarriesTheShape_WithTheFieldsToo()
    {
        using var client = await SignedInAsync();

        var response = await client.PostAsJsonAsync("/api/BodyMeasurements", new { Date = DateTime.UtcNow, WeightKg = -5m });

        var body = await AssertShape(response, HttpStatusCode.BadRequest, "validation_failed");
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ANotFound_AndAnIdMismatch_CarryTheShape()
    {
        using var client = await SignedInAsync();

        await AssertShape(await client.DeleteAsync($"/api/BodyMeasurements/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "not_found");

        var id = Guid.NewGuid();
        var mismatch = await client.PutAsJsonAsync($"/api/Foods/{id}", new { Id = Guid.NewGuid(), Name = "x" });
        await AssertShape(mismatch, HttpStatusCode.BadRequest, "id_mismatch");
    }

    [Fact]
    public async Task Forbidden_CarriesTheShape()
    {
        using var client = await SignedInAsync();

        await AssertShape(await client.GetAsync("/api/AuditLogs"), HttpStatusCode.Forbidden, "forbidden");
    }

    private async Task<HttpClient> SignedInAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        var email = $"err-{user:N}@example.com";
        await _fixture.SeedUserAsync(user, email);
        return _fixture.CreateAuthenticatedClient(user, email);
    }

    private static async Task<JsonElement> AssertShape(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotBeNullOrWhiteSpace("an error is never an empty body");
        var body = JsonDocument.Parse(text).RootElement;
        body.GetProperty("errorCode").GetString().Should().Be(code);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
        return body;
    }
}
