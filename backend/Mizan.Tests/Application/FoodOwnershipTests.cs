using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Commands;
using Mizan.Application.Exceptions;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Tests.Infrastructure;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// Foods belong to someone - see docs/REFOCUS.md §4. Admins curate the shared
/// catalogue; everyone else creates foods private to them. Before Food.UserId
/// existed the endpoint had to be admin-only, because every food was everyone's.
/// </summary>
public class FoodOwnershipTests
{
    private static readonly Guid UserId = Guid.Parse("11110000-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("11110000-0000-0000-0000-000000000002");

    private static (MizanDbContext db, HybridCache cache, FakeCurrentUser user) Make(string role)
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var services = new ServiceCollection();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();
        return (new MizanDbContext(options), cache, new FakeCurrentUser { UserId = UserId, Role = role });
    }

    private static CreateFoodCommand NewFood() => new()
    {
        Name = "Homemade granola",
        CaloriesPer100g = 450,
        ProteinPer100g = 10,
        CarbsPer100g = 60,
        FatPer100g = 18,
        ServingSize = 100,
        ServingUnit = "g",
        IsVerified = true
    };

    [Fact]
    public async Task AUserCreatesAPrivateFood_AndCannotSelfVerifyIt()
    {
        var (db, cache, user) = Make("user");
        var handler = new CreateFoodCommandHandler(db, cache, user);

        var result = await handler.Handle(NewFood(), CancellationToken.None);

        var food = await db.Foods.SingleAsync(f => f.Id == result.Id);
        food.UserId.Should().Be(UserId);
        food.IsVerified.Should().BeFalse("verification is an admin judgement, not a self-assertion");
    }

    [Fact]
    public async Task AnAdminCreatesAPublicFood()
    {
        var (db, cache, user) = Make("admin");
        var handler = new CreateFoodCommandHandler(db, cache, user);

        var result = await handler.Handle(NewFood(), CancellationToken.None);

        var food = await db.Foods.SingleAsync(f => f.Id == result.Id);
        food.UserId.Should().BeNull("null means public/global");
        food.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task AUserCannotEditSomeoneElsesFood()
    {
        var (db, cache, user) = Make("user");
        var foodId = Guid.NewGuid();
        db.Foods.Add(new Food { Id = foodId, UserId = OtherUserId, Name = "Theirs" });
        await db.SaveChangesAsync();

        var handler = new DeleteFoodCommandHandler(db, cache, user);

        var act = () => handler.Handle(new DeleteFoodCommand { Id = foodId }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task AUserCannotDeleteAPublicFood()
    {
        var (db, cache, user) = Make("user");
        var foodId = Guid.NewGuid();
        db.Foods.Add(new Food { Id = foodId, UserId = null, Name = "Shared catalogue entry" });
        await db.SaveChangesAsync();

        var handler = new DeleteFoodCommandHandler(db, cache, user);

        var act = () => handler.Handle(new DeleteFoodCommand { Id = foodId }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task AUserCanDeleteTheirOwnFood()
    {
        var (db, cache, user) = Make("user");
        var foodId = Guid.NewGuid();
        db.Foods.Add(new Food { Id = foodId, UserId = UserId, Name = "Mine" });
        await db.SaveChangesAsync();

        var handler = new DeleteFoodCommandHandler(db, cache, user);
        var result = await handler.Handle(new DeleteFoodCommand { Id = foodId }, CancellationToken.None);

        result.Success.Should().BeTrue();
        db.Foods.Should().BeEmpty();
    }

    [Fact]
    public async Task AnAdminCanDeleteAPublicFood()
    {
        var (db, cache, user) = Make("admin");
        var foodId = Guid.NewGuid();
        db.Foods.Add(new Food { Id = foodId, UserId = null, Name = "Shared catalogue entry" });
        await db.SaveChangesAsync();

        var handler = new DeleteFoodCommandHandler(db, cache, user);
        var result = await handler.Handle(new DeleteFoodCommand { Id = foodId }, CancellationToken.None);

        result.Success.Should().BeTrue();
    }
}
