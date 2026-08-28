using System.Net;
using System.Net.Http.Headers;
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

    private async Task<HttpClient> SignedInAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        await _fixture.SeedUserAsync(userId, $"upload-{userId:N}@example.com", emailVerified: true);
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
