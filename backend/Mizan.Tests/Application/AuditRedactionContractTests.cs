using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Behaviors;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Moq;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The audit log stores a serialized copy of every command. A command that carries a secret must
/// therefore say what to store instead (IRedactedAudit) or opt out (ISkipAudit). The contract test
/// catches a new command at review time; the behavior tests show the runtime net that catches one
/// that still gets through.
/// </summary>
public class AuditRedactionContractTests
{
    [Fact]
    public void EveryCommandWithASecretLookingProperty_DeclaresItsOwnAuditDetails_OrSkipsAudit()
    {
        var commands = typeof(AuditBehavior<,>).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .OrderBy(t => t.FullName)
            .ToList();
        commands.Should().NotBeEmpty("the scan must find the application's commands");

        var offenders = commands
            .Where(t => !typeof(IRedactedAudit).IsAssignableFrom(t) && !typeof(ISkipAudit).IsAssignableFrom(t))
            .Select(t => (Type: t, Secrets: AuditRedaction.SensitiveProperties(t)))
            .Where(x => x.Secrets.Count > 0)
            .Select(x => $"{x.Type.Name} ({string.Join(", ", x.Secrets.Select(p => p.Name))})")
            .ToList();

        offenders.Should().BeEmpty(
            "a command whose string property is named like a credential would be stored verbatim in the audit log; " +
            "implement IRedactedAudit with details that leave the secret out, or ISkipAudit");
    }

    [Theory]
    [InlineData("Token", true)]
    [InlineData("fcmToken", true)]
    [InlineData("accessCode", true)]
    [InlineData("ClientSecret", true)]
    [InlineData("NewPassword", true)]
    [InlineData("ApiKey", true)]
    [InlineData("otp", true)]
    [InlineData("PinCode", true)]
    [InlineData("Email", false)]
    [InlineData("DeviceName", false)]
    public void TheSensitiveNameRule_IsCaseInsensitiveAndMatchesFragments(string name, bool sensitive) =>
        AuditRedaction.IsSensitiveName(name).Should().Be(sensitive);

    [Fact]
    public async Task ACommandThatSlipsThrough_IsStoredWithTheSecretRedacted_AndWarnedAboutOnce()
    {
        using var db = new MizanDbContext(new DbContextOptionsBuilder<MizanDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        var logger = new RecordingLogger<AuditBehavior<LeakyCommand, Guid>>();
        var behavior = new AuditBehavior<LeakyCommand, Guid>(db, user.Object, logger);

        await behavior.Handle(new LeakyCommand("fcm-abc-secret", "Pixel"), _ => Task.FromResult(Guid.NewGuid()), CancellationToken.None);
        await behavior.Handle(new LeakyCommand("fcm-def-secret", "Pixel"), _ => Task.FromResult(Guid.NewGuid()), CancellationToken.None);

        var rows = await db.AuditLogs.Where(a => a.Action == nameof(LeakyCommand)).ToListAsync();
        rows.Should().HaveCount(2);
        foreach (var row in rows)
        {
            row.Details.Should().NotContain("secret");
            var details = JsonDocument.Parse(row.Details!).RootElement;
            details.GetProperty("PushToken").GetString().Should().Be(AuditRedaction.Placeholder);
            details.GetProperty("DeviceName").GetString().Should().Be("Pixel", "only the secret is touched");
        }

        logger.Entries.Where(e => e.Level == LogLevel.Warning && e.Message.Contains("IRedactedAudit"))
            .Should().HaveCount(1, "the miss is reported once per command type, not on every call");
    }

    [Fact]
    public async Task ACommandWithExplicitDetails_StoresExactlyThose()
    {
        using var db = new MizanDbContext(new DbContextOptionsBuilder<MizanDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new Mock<ICurrentUserService>();
        var behavior = new AuditBehavior<RegisterDeviceCommand, DeviceDto>(db, user.Object, new RecordingLogger<AuditBehavior<RegisterDeviceCommand, DeviceDto>>());
        var dto = new DeviceDto(Guid.NewGuid(), "android", "Pixel", DateTime.UtcNow, DateTime.UtcNow);

        await behavior.Handle(new RegisterDeviceCommand("fcm-token-value", "android", "Pixel"), _ => Task.FromResult(dto), CancellationToken.None);

        var row = await db.AuditLogs.SingleAsync();
        row.Details.Should().NotContain("fcm-token-value").And.NotContain("Token");
        JsonDocument.Parse(row.Details!).RootElement.GetProperty("Platform").GetString().Should().Be("android");
    }

    /// <summary>A command the way a hurried change might write one: a secret in a plain property.</summary>
    public sealed record LeakyCommand(string PushToken, string DeviceName) : IRequest<Guid>;

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
