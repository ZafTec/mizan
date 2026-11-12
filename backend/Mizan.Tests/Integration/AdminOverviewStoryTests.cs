using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Admin;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>The overview's "Needs attention" list, through the API (docs/ADMIN.md#overview-and-health).</summary>
[Collection("ApiIntegration")]
public class AdminOverviewStoryTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task AnAdminOpensTheOverview_AndSeesWhatWaits_WithTheOldestItemOfEach()
    {
        await fixture.ResetDatabaseAsync();
        var adminId = Guid.NewGuid();
        var email = $"overview-admin-{adminId:N}@example.test";
        await fixture.SeedUserAsync(adminId, email, role: "admin");
        using var admin = fixture.CreateAuthenticatedClient(adminId, email, "admin");

        var calm = await admin.GetFromJsonAsync<AdminOverviewDto>("/api/admin/overview");
        calm!.Attention.Should().Be(new AdminAttentionDto(0, null, 0, 0, null, 0, 0));
        calm.JoinedThisWeek.Should().Be(1, "the administrator just joined");

        var now = DateTime.UtcNow;
        var oldestJob = now.AddDays(-3);
        var oldestMessage = now.AddHours(-30);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            foreach (var (status, at) in new[] { (OutboxJobStatus.DeadLettered, oldestJob), (OutboxJobStatus.DeadLettered, now.AddHours(-1)), (OutboxJobStatus.Succeeded, now.AddDays(-5)) })
            {
                db.OutboxJobs.Add(new OutboxJob
                {
                    Id = Guid.CreateVersion7(),
                    Type = "email",
                    Payload = "{}",
                    Status = status,
                    RunAfter = at,
                    CreatedAt = at,
                    CompletedAt = at,
                });
            }

            foreach (var (status, at) in new[] { (ContactSubmissionStatus.New, oldestMessage), (ContactSubmissionStatus.New, now.AddMinutes(-5)), (ContactSubmissionStatus.Handled, now.AddDays(-9)) })
            {
                db.ContactSubmissions.Add(new ContactSubmission
                {
                    Id = Guid.CreateVersion7(),
                    Name = "A visitor",
                    Email = "visitor@example.test",
                    Subject = "A question",
                    Message = "Hello.",
                    Status = status,
                    IpHash = "test",
                    CreatedAt = at,
                });
            }

            await db.SaveChangesAsync();
        }

        var busy = (await admin.GetFromJsonAsync<AdminOverviewDto>("/api/admin/overview"))!.Attention;

        busy.DeadJobs.Should().Be(2, "a job that succeeded waits for no one");
        busy.OldestDeadJobAt.Should().BeCloseTo(oldestJob, TimeSpan.FromMilliseconds(1));
        busy.NewContactMessages.Should().Be(2, "a handled message waits for no one");
        busy.OldestContactMessageAt.Should().BeCloseTo(oldestMessage, TimeSpan.FromMilliseconds(1));
    }
}
