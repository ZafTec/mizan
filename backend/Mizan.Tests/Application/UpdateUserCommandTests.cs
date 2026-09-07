using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Moq;
using Xunit;

namespace Mizan.Tests.Application;

public class UpdateUserCommandTests
{
    private const string SharedKey = "avatars/2026/09/shared.png";
    private const string SharedImage = "https://storage.example.test/mizan/" + SharedKey;

    [Theory]
    [InlineData("https://storage.example.test/mizan/avatars/2026/09/replacement.png")]
    [InlineData("")]
    public async Task ReplacingOrClearingACopiedAvatarLeavesTheSharedObjectIntact(string nextImage)
    {
        using var db = CreateDatabase();
        var owner = new User { Id = Guid.NewGuid(), Email = "owner@example.test", Image = SharedImage };
        var caller = new User { Id = Guid.NewGuid(), Email = "caller@example.test" };
        db.Users.AddRange(owner, caller);
        await db.SaveChangesAsync();

        var objects = new HashSet<string> { SharedKey };
        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        storage.Setup(service => service.TryGetKey(It.IsAny<string?>()))
            .Returns<string?>(url => url == SharedImage ? SharedKey : null);
        storage.Setup(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => objects.Remove(key))
            .Returns(Task.CompletedTask);
        var cache = CreateCache();
        using var services = CreateServices(db, storage.Object, cache.Object);
        var handler = ActivatorUtilities.CreateInstance<UpdateUserCommandHandler>(services);

        // Public URLs can be copied onto another profile before being changed.
        (await handler.Handle(new UpdateUserCommand(caller.Id, null, SharedImage), CancellationToken.None))
            .Should().BeTrue();
        (await handler.Handle(new UpdateUserCommand(caller.Id, "Updated name", nextImage), CancellationToken.None))
            .Should().BeTrue();

        db.ChangeTracker.Clear();
        var savedCaller = await db.Users.SingleAsync(user => user.Id == caller.Id);
        savedCaller.Image.Should().Be(nextImage);
        savedCaller.Name.Should().Be("Updated name");
        (await db.Users.SingleAsync(user => user.Id == owner.Id)).Image.Should().Be(SharedImage);
        objects.Should().Contain(SharedKey);
        storage.Verify(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(service => service.InvalidateAsync(caller.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
        cache.Verify(service => service.InvalidateAsync(owner.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatingPreferencesPreservesAnUnspecifiedImageAndInvalidatesTheUserCache()
    {
        using var db = CreateDatabase();
        var previousUpdate = DateTime.UtcNow.AddDays(-1);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "profile@example.test", Name = "Original name",
            Image = SharedImage, UpdatedAt = previousUpdate,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var cache = CreateCache();
        var handler = new UpdateUserCommandHandler(db, cache.Object);

        var updated = await handler.Handle(new UpdateUserCommand(
            user.Id, "Updated name", null, "dark", true, true, "Africa/Addis_Ababa"), CancellationToken.None);

        updated.Should().BeTrue();
        db.ChangeTracker.Clear();
        var saved = await db.Users.SingleAsync();
        saved.Name.Should().Be("Updated name");
        saved.Image.Should().Be(SharedImage);
        saved.ThemePreference.Should().Be("dark");
        saved.CompactMode.Should().BeTrue();
        saved.ReduceAnimations.Should().BeTrue();
        saved.TimeZoneId.Should().Be("Africa/Addis_Ababa");
        saved.UpdatedAt.Should().BeAfter(previousUpdate);
        cache.Verify(service => service.InvalidateAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MizanDbContext CreateDatabase() => new(new DbContextOptionsBuilder<MizanDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Mock<IUserCacheInvalidator> CreateCache()
    {
        var cache = new Mock<IUserCacheInvalidator>(MockBehavior.Strict);
        cache.Setup(service => service.InvalidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return cache;
    }

    private static ServiceProvider CreateServices(
        IMizanDbContext db, IStorageService storage, IUserCacheInvalidator cache)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(storage);
        services.AddSingleton(cache);
        return services.BuildServiceProvider();
    }
}
