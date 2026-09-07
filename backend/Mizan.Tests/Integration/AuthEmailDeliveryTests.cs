using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class AuthEmailDeliveryTests(ApiTestFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, OutboxJobStatus.Pending)]
    [InlineData(false, OutboxJobStatus.Succeeded)]
    [InlineData(false, OutboxJobStatus.DeadLettered)]
    [InlineData(true, OutboxJobStatus.Pending)]
    [InlineData(true, OutboxJobStatus.Succeeded)]
    [InlineData(true, OutboxJobStatus.DeadLettered)]
    public async Task ReissuedLinkQueuesAndDeliversANewEmail_RegardlessOfEarlierDeliveryState(
        bool passwordReset, OutboxJobStatus earlierStatus)
    {
        await fixture.ResetDatabaseAsync();
        fixture.Email.Clear();
        using var client = fixture.CreateClient();
        var email = $"delivery-{Guid.NewGuid():N}@example.test";
        var requestPath = passwordReset ? "/api/Auth/forgot-password" : "/api/Auth/resend-verification";
        var tokenPath = passwordReset ? "reset-password" : "verifyemail";

        if (passwordReset)
        {
            await fixture.SeedUserAsync(Guid.NewGuid(), email);
            (await client.PostAsJsonAsync(requestPath, new { Email = email }))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
        }
        else
        {
            (await client.PostAsJsonAsync("/api/Auth/register", new
            {
                Email = email, Password = "synthetic-long-password", Name = "Delivery test"
            })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var earlier = (await EmailJobsAsync()).Should().ContainSingle().Which;
        var earlierMessage = ReadMessage(earlier);
        var earlierToken = await ReadTokenAsync(earlierMessage, tokenPath);

        if (earlierStatus == OutboxJobStatus.Succeeded)
        {
            await fixture.DrainOutboxAsync();
            fixture.Email.Clear();
        }
        else if (earlierStatus == OutboxJobStatus.DeadLettered)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            var job = await db.OutboxJobs.SingleAsync(j => j.Id == earlier.Id);
            job.Status = OutboxJobStatus.DeadLettered;
            job.Attempts = 5;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // Retrying the identical queued delivery still uses the same job, even
        // in a terminal state. A new token below must be a different delivery.
        using (var scope = fixture.Services.CreateScope())
        {
            var duplicateId = await scope.ServiceProvider.GetRequiredService<IOutbox>()
                .EnqueueAsync(OutboxJobTypes.Email, earlierMessage, earlier.DedupeKey);
            await scope.ServiceProvider.GetRequiredService<MizanDbContext>().SaveChangesAsync();
            duplicateId.Should().Be(earlier.Id);
        }
        (await EmailJobsAsync()).Should().ContainSingle();

        (await client.PostAsJsonAsync(requestPath, new { Email = email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var jobs = await EmailJobsAsync();
        jobs.Should().HaveCount(2, "a fresh token needs a fresh email, even when its subject is unchanged");
        jobs.Single(j => j.Id == earlier.Id).Status.Should().Be(earlierStatus);
        var replacement = jobs.Single(j => j.Id != earlier.Id);
        replacement.Status.Should().Be(OutboxJobStatus.Pending);
        replacement.DedupeKey.Should().NotBe(earlier.DedupeKey);
        var replacementMessage = ReadMessage(replacement);
        var replacementToken = await ReadTokenAsync(replacementMessage, tokenPath);
        replacementToken.Should().NotBe(earlierToken);
        replacement.DedupeKey.Should().NotContain(replacementToken);
        earlier.DedupeKey.Should().NotContain(earlierToken);

        (await FollowLinkAsync(client, passwordReset, earlierToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await fixture.DrainOutboxAsync();
        fixture.Email.Sent.Should().Contain(replacementMessage);
        (await EmailJobsAsync()).Single(j => j.Id == replacement.Id)
            .Status.Should().Be(OutboxJobStatus.Succeeded);
        (await FollowLinkAsync(client, passwordReset, replacementToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<List<OutboxJob>> EmailJobsAsync()
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs.AsNoTracking()
            .Where(j => j.Type == OutboxJobTypes.Email).ToListAsync();
    }

    private static EmailMessage ReadMessage(OutboxJob job) =>
        JsonSerializer.Deserialize<EmailMessage>(job.Payload, Json)!;

    private static async Task<string> ReadTokenAsync(EmailMessage message, string tokenPath)
    {
        var recording = new RecordingEmailSender();
        await recording.SendAsync(message);
        var token = recording.LastTokenFor(message.To, tokenPath);
        token.Should().NotBeNullOrWhiteSpace();
        return token!;
    }

    private static Task<HttpResponseMessage> FollowLinkAsync(HttpClient client, bool passwordReset, string token) =>
        passwordReset
            ? client.PostAsJsonAsync("/api/Auth/reset-password", new { Token = token, Password = "replacement-long-password" })
            : client.PostAsJsonAsync("/api/Auth/verify-email", new { Token = token });
}
