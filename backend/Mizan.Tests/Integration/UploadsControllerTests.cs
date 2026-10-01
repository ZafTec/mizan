using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class UploadsControllerTests
{
    private readonly ApiTestFixture _fixture;

    public UploadsControllerTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UploadImage_RequiresASignedInUser()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = _fixture.CreateClient();

        var response = await client.PostAsync("/api/Uploads/image", Png("shot.png"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The content type is the caller's claim; the bytes decide. This file
    /// says image/png and is a shell script, and it never reaches the store.
    /// </summary>
    [Fact]
    public async Task UploadImage_RejectsAFileThatIsNotAnImage()
    {
        var client = await SignedInAsync();

        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.ASCII.GetBytes("#!/bin/sh\nrm -rf /\n"));
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(part, "file", "totally-a.png");

        var response = await client.PostAsync("/api/Uploads/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        client.Dispose();
    }

    /// <summary>
    /// With no object store configured the endpoint has to say so, not fall
    /// over with a connection error.
    /// </summary>
    [Fact]
    public async Task UploadImage_SaysSoWhenStorageIsNotConfigured()
    {
        var client = await SignedInAsync();

        var response = await client.PostAsync("/api/Uploads/image", Png("shot.png"));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        client.Dispose();
    }

    // ---- 3D models for the app ----

    [Fact]
    public async Task UploadModel_IsForAdministratorsOnly()
    {
        var client = await SignedInAsync();

        (await client.PostAsync("/api/Uploads/model", Glb())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.Dispose();
    }

    [Fact]
    public async Task UploadModel_RejectsAFileThatIsNotAGlb()
    {
        var admin = await SignedInAsync(role: "admin");

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.ASCII.GetBytes("<html>not a model</html>")), "file", "evil.glb");

        (await admin.PostAsync("/api/Uploads/model", content)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        admin.Dispose();
    }

    [Fact]
    public async Task UploadModel_AcceptsAGlb_AndReachesTheStore()
    {
        var admin = await SignedInAsync(role: "admin");

        // Storage is not configured in tests, so getting that answer proves the file passed every check before it.
        (await admin.PostAsync("/api/Uploads/model", Glb())).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        admin.Dispose();
    }

    [Fact]
    public async Task TheImageDoor_IsClosedToTheModelsFolder_AndToExerciseMediaForOrdinaryUsers()
    {
        var user = await SignedInAsync();

        (await user.PostAsync("/api/Uploads/image?folder=Models", Png("x.png"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await user.PostAsync("/api/Uploads/image?folder=Exercises", Png("x.png"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        user.Dispose();
    }

    [Fact]
    public async Task AnExerciseModel_MustBeOneWeStored()
    {
        var admin = await SignedInAsync(role: "admin");
        var id = await FirstExerciseAsync(admin);

        var foreign = await admin.PutAsJsonAsync($"/api/Exercises/{id}/model", new { ModelUrl = "https://evil.example/x.glb" });
        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.PutAsJsonAsync($"/api/Exercises/{id}/model", new { ModelUrl = (string?)null })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        admin.Dispose();
    }

    private async Task<Guid> FirstExerciseAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/Exercises", new { Name = "Model test squat", Category = "strength" });
        created.EnsureSuccessStatusCode();
        var list = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/Exercises?search=Model test squat");
        return list.GetProperty("items")[0].GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent Glb()
    {
        // "glTF", version 2, total length 12: a valid header, which is all the check reads.
        var part = new ByteArrayContent(new byte[] { 0x67, 0x6C, 0x54, 0x46, 2, 0, 0, 0, 12, 0, 0, 0 });
        part.Headers.ContentType = new MediaTypeHeaderValue("model/gltf-binary");
        var content = new MultipartFormDataContent();
        content.Add(part, "file", "squat.glb");
        return content;
    }

    private async Task<HttpClient> SignedInAsync(string role = "user")
    {
        await _fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        await _fixture.SeedUserAsync(userId, $"upload-{userId:N}@example.com", emailVerified: true, role: role);
        return _fixture.CreateAuthenticatedClient(userId, $"upload-{userId:N}@example.com");
    }

    private static MultipartFormDataContent Png(string fileName)
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3 };
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var content = new MultipartFormDataContent();
        content.Add(part, "file", fileName);
        return content;
    }
}
