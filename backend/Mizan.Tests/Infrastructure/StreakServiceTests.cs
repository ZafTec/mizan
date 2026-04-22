using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Services;
using Xunit;

namespace Mizan.Tests.Infrastructure;

public class StreakServiceTests
{
    private static (MizanDbContext db, StreakService svc, Guid userId) Make(FakeCurrentUser? user = null)
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new MizanDbContext(options);
        user ??= new FakeCurrentUser { UserId = Guid.NewGuid() };
        var svc = new StreakService(db, user);
        return (db, svc, user.UserId ?? Guid.Empty);
    }

    [Fact]
    public async Task RecordActivity_FirstEver_CreatesStreakAtOne_AndFlagsNewRecord()
    {
        var (db, svc, userId) = Make();

        var result = await svc.RecordActivityAsync("nutrition");

        result.CurrentCount.Should().Be(1);
        result.LongestCount.Should().Be(1);
        result.IsNewRecord.Should().BeTrue();
        result.Extended.Should().BeTrue();
        result.StreakType.Should().Be("nutrition");
        result.LastActivityDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));

        var row = await db.Streaks.SingleAsync(s => s.UserId == userId);
        row.CurrentCount.Should().Be(1);
        row.LongestCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordActivity_SameDayTwice_IsIdempotent_AndExtendedFalse()
    {
        var (db, svc, userId) = Make();

        await svc.RecordActivityAsync("nutrition");
        var second = await svc.RecordActivityAsync("nutrition");

        second.CurrentCount.Should().Be(1);
        second.Extended.Should().BeFalse();
        second.IsNewRecord.Should().BeFalse();
        (await db.Streaks.CountAsync(s => s.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task RecordActivity_ConsecutiveDay_IncrementsCount()
    {
        var (db, svc, userId) = Make();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        db.Streaks.Add(new Streak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StreakType = "nutrition",
            CurrentCount = 3,
            LongestCount = 5,
            LastActivityDate = yesterday
        });
        await db.SaveChangesAsync();

        var result = await svc.RecordActivityAsync("nutrition");

        result.CurrentCount.Should().Be(4);
        result.LongestCount.Should().Be(5); // unchanged, still below record
        result.IsNewRecord.Should().BeFalse();
        result.Extended.Should().BeTrue();
    }

    [Fact]
    public async Task RecordActivity_AfterGap_ResetsCountToOne()
    {
        var (db, svc, userId) = Make();
        var fiveDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        db.Streaks.Add(new Streak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StreakType = "nutrition",
            CurrentCount = 10,
            LongestCount = 10,
            LastActivityDate = fiveDaysAgo
        });
        await db.SaveChangesAsync();

        var result = await svc.RecordActivityAsync("nutrition");

        result.CurrentCount.Should().Be(1);
        result.LongestCount.Should().Be(10); // record preserved across reset
        result.IsNewRecord.Should().BeFalse();
        result.Extended.Should().BeTrue();
    }

    [Fact]
    public async Task RecordActivity_BeatingLongest_FlagsNewRecord()
    {
        var (db, svc, userId) = Make();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        db.Streaks.Add(new Streak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StreakType = "nutrition",
            CurrentCount = 7,
            LongestCount = 7,
            LastActivityDate = yesterday
        });
        await db.SaveChangesAsync();

        var result = await svc.RecordActivityAsync("nutrition");

        result.CurrentCount.Should().Be(8);
        result.LongestCount.Should().Be(8);
        result.IsNewRecord.Should().BeTrue();
    }

    [Fact]
    public async Task RecordActivity_SeparatesStreaksByType()
    {
        var (db, svc, userId) = Make();

        var n = await svc.RecordActivityAsync("nutrition");
        var w = await svc.RecordActivityAsync("workout");

        n.CurrentCount.Should().Be(1);
        w.CurrentCount.Should().Be(1);
        (await db.Streaks.CountAsync(s => s.UserId == userId)).Should().Be(2);
    }

    [Fact]
    public async Task RecordActivity_Unauthenticated_Throws()
    {
        var user = new FakeCurrentUser { UserId = null };
        var (_, svc, _) = Make(user);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.RecordActivityAsync("nutrition"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordActivity_RejectsEmptyType(string? type)
    {
        var (_, svc, _) = Make();

        await Assert.ThrowsAsync<ArgumentException>(() => svc.RecordActivityAsync(type!));
    }
}

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; } = "user";
    public string? IpAddress { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
    public bool IsInRole(string role) => string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
}
