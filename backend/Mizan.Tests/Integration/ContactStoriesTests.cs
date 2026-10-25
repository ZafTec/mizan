using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Common;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>The public contact form and the admin inbox, through the API (docs/ADMIN.md#contact).</summary>
[Collection("ApiIntegration")]
public class ContactStoriesTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task AContactSubmissionWritesTheRowAndEmailsEveryAdminInOneTransaction_AndTheHoneypotIsRefusedSilently()
    {
        await fixture.ResetDatabaseAsync();
        fixture.Email.Clear();
        var admins = new[] { Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in admins) await fixture.SeedUserAsync(id, $"contact-admin-{id:N}@example.test", role: "admin");
        var member = Guid.NewGuid();
        await fixture.SeedUserAsync(member, $"member-{member:N}@example.test");
        using var signedIn = fixture.CreateAuthenticatedClient(member, $"member-{member:N}@example.test");

        var accepted = await signedIn.PostAsJsonAsync("/api/Contact", new ContactRequest(
            "Sam Rivera", "sam@example.test", "Billing question", "Can I pay yearly?", Website: ""));
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            var row = await db.ContactSubmissions.SingleAsync();
            row.UserId.Should().Be(member, "a signed-in sender is linked from the session");
            row.Status.Should().Be(ContactSubmissionStatus.New);
            row.IpHash.Should().NotBeNullOrWhiteSpace();

            var notices = await db.EmailMessages.Where(m => m.Kind == EmailKinds.ContactReceived).ToListAsync();
            notices.Select(m => m.ToUserId).Should().BeEquivalentTo(admins.Cast<Guid?>());
            (await db.OutboxJobs.CountAsync(j => j.Type == OutboxJobTypes.Email)).Should().Be(2);
        }

        using var anonymous = fixture.CreateClient();
        var bot = await anonymous.PostAsJsonAsync("/api/Contact", new ContactRequest(
            "x", "not-an-address", "Buy now", "spam", Website: "http://spam.example"));
        bot.StatusCode.Should().Be(HttpStatusCode.Accepted, "a bot gets the ordinary answer");
        (await bot.Content.ReadFromJsonAsync<ContactAcceptedDto>())!.Accepted.Should().BeTrue();

        var invalid = await anonymous.PostAsJsonAsync("/api/Contact", new ContactRequest("Ann", "ann@example.test", "Hi", new string('x', 5001)));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest, "messages are capped");

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            (await db.ContactSubmissions.CountAsync()).Should().Be(1, "the honeypot and the oversized message wrote nothing");
            (await db.EmailMessages.CountAsync()).Should().Be(2);
        }
    }

    [Fact]
    public async Task OneAddressMayWriteAFewTimesAnHour()
    {
        await fixture.ResetDatabaseAsync();
        using var anonymous = fixture.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await anonymous.PostAsJsonAsync("/api/Contact", new ContactRequest("Ann", "ann@example.test", $"Hi {i}", "Hello")))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        (await anonymous.PostAsJsonAsync("/api/Contact", new ContactRequest("Ann", "ann@example.test", "Again", "Hello")))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AnAdminReplyQueuesTheEmail_AndMarksTheMessageHandled()
    {
        await fixture.ResetDatabaseAsync();
        fixture.Email.Clear();
        var adminId = Guid.NewGuid();
        await fixture.SeedUserAsync(adminId, $"reply-admin-{adminId:N}@example.test", role: "admin");
        using var admin = fixture.CreateAuthenticatedClient(adminId, $"reply-admin-{adminId:N}@example.test", "admin");
        using var anonymous = fixture.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/Contact", new ContactRequest("Sam", "sam@example.test", "Lost access", "I cannot sign in.")))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var inbox = await admin.GetFromJsonAsync<PagedResult<ContactSubmissionDto>>("/api/admin/contact?status=New&search=sign%20in");
        var message = inbox!.Items.Should().ContainSingle().Which;
        message.Email.Should().Be("sam@example.test");

        var replied = await (await admin.PostAsJsonAsync($"/api/admin/contact/{message.Id}/reply", new ContactReplyRequest("Use Forgot password on the sign-in page.")))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<ContactSubmissionDto>();
        replied!.Status.Should().Be("Handled");
        replied.HandledByUserId.Should().Be(adminId);
        replied.ReplyStatus.Should().Be("Queued");

        (await admin.PostAsJsonAsync($"/api/admin/contact/{message.Id}/reply", new ContactReplyRequest("Twice")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "one reply per message");

        var reply = await admin.GetFromJsonAsync<EmailMessageDto>($"/api/admin/mail/{replied.ReplyEmailMessageId}");
        reply!.Kind.Should().Be(EmailKinds.ContactReply);
        reply.ToAddress.Should().Be("sam@example.test");
        reply.Subject.Should().Be("Re: Lost access");

        await fixture.DrainOutboxAsync();
        fixture.Email.Sent.Should().ContainSingle(m => m.To == "sam@example.test" && m.Text.Contains("Use Forgot password"));
        (await admin.GetFromJsonAsync<ContactSubmissionDto>($"/api/admin/contact/{message.Id}"))!.ReplyStatus.Should().Be("Sent");
        (await admin.GetFromJsonAsync<PagedResult<ContactSubmissionDto>>("/api/admin/contact?status=New"))!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AnAdminCanMarkAMessageHandledWithoutReplying_AndNonAdminsCannotReadTheInbox()
    {
        await fixture.ResetDatabaseAsync();
        var adminId = Guid.NewGuid();
        await fixture.SeedUserAsync(adminId, $"handled-admin-{adminId:N}@example.test", role: "admin");
        using var admin = fixture.CreateAuthenticatedClient(adminId, $"handled-admin-{adminId:N}@example.test", "admin");
        var userId = Guid.NewGuid();
        await fixture.SeedUserAsync(userId, $"plain-{userId:N}@example.test");
        using var user = fixture.CreateAuthenticatedClient(userId, $"plain-{userId:N}@example.test");
        (await user.PostAsJsonAsync("/api/Contact", new ContactRequest("Pat", "pat@example.test", "Thanks", "Nice app."))).EnsureSuccessStatusCode();

        (await user.GetAsync("/api/admin/contact")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var id = (await admin.GetFromJsonAsync<PagedResult<ContactSubmissionDto>>("/api/admin/contact"))!.Items.Single().Id;
        var handled = await (await admin.PostAsync($"/api/admin/contact/{id}/handled", null))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<ContactSubmissionDto>();
        handled!.Status.Should().Be("Handled");
        handled.ReplyEmailMessageId.Should().BeNull();
    }
}
