using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Auth;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Identity;
using Moq;
using Xunit;

namespace Mizan.Tests.Authentication;

public class LoginPasswordMigrationTests
{
    [Fact]
    public async Task SuccessfulLegacyLoginPersistsIdentityHashAndCreatesSession()
    {
        using var db = CreateDatabase();
        var user = await AddUserAsync(db);
        using var services = CreateServices();
        var handler = CreateHandler(db, services);
        var command = new LoginCommand(user.Email, LegacyPasswordFixtures.Password, null, null);

        new LoginCommandValidator().Validate(command).IsValid.Should().BeTrue();
        var result = await handler.Handle(command, CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = await db.Users.SingleAsync();
        saved.Id.Should().Be(user.Id);
        saved.PasswordHash.Should().NotBe(LegacyPasswordFixtures.Hash);
        new PasswordHasher<User>().VerifyHashedPassword(saved, saved.PasswordHash!, LegacyPasswordFixtures.Password)
            .Should().Be(PasswordVerificationResult.Success);
        var session = await db.UserSessions.SingleAsync();
        session.UserId.Should().Be(user.Id);
        session.TokenHash.Should().Be(SecureToken.Hash(result.SessionToken));

        var upgradedHash = saved.PasswordHash;
        await handler.Handle(command, CancellationToken.None);
        db.ChangeTracker.Clear();
        (await db.Users.SingleAsync()).PasswordHash.Should().Be(upgradedHash);
    }

    [Theory]
    [InlineData("wrong-password", typeof(InvalidCredentialsException))]
    [InlineData("malformed-hash", typeof(InvalidCredentialsException))]
    [InlineData("unverified", typeof(EmailNotVerifiedException))]
    [InlineData("banned", typeof(ForbiddenAccessException))]
    [InlineData("locked", typeof(AccountLockedException))]
    public async Task RejectedLoginPreservesCredentialAndCreatesNoSession(string reason, Type exceptionType)
    {
        using var db = CreateDatabase();
        var user = await AddUserAsync(db);
        if (reason == "malformed-hash") user.PasswordHash = "betterauth-scrypt$invalid";
        if (reason == "unverified") user.EmailVerified = false;
        if (reason == "banned") user.Banned = true;
        if (reason == "locked") user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync();
        var originalHash = user.PasswordHash;
        using var services = CreateServices();
        var handler = CreateHandler(db, services);

        var exception = await Record.ExceptionAsync(() => handler.Handle(new LoginCommand(
            user.Email,
            reason == "wrong-password" ? "wrong-password" : LegacyPasswordFixtures.Password,
            null,
            null), CancellationToken.None));

        exception.Should().BeOfType(exceptionType);
        db.ChangeTracker.Clear();
        (await db.Users.SingleAsync()).PasswordHash.Should().Be(originalHash);
        (await db.UserSessions.CountAsync()).Should().Be(0);
    }

    private static MizanDbContext CreateDatabase() => new(new DbContextOptionsBuilder<MizanDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<User> AddUserAsync(MizanDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "legacy@example.com",
            EmailVerified = true,
            PasswordHash = LegacyPasswordFixtures.Hash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private static LoginCommandHandler CreateHandler(MizanDbContext db, IServiceProvider services) => new(
        db,
        new PasswordHasherAdapter(),
        new SessionService(db, services.GetRequiredService<HybridCache>()),
        Mock.Of<IOutbox>());
}
