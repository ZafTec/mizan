using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Tests.Infrastructure;
using Xunit;

namespace Mizan.Tests.Application;

public class PickerRankingTests
{
    [Fact]
    public async Task AnOwnerlessPrivateRecipeDoesNotBecomeVisibleToAnAnonymousVisitor()
    {
        using var db = new MizanDbContext(new DbContextOptionsBuilder<MizanDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var recipe = new Recipe { Id = Guid.NewGuid(), UserId = null, IsPublic = false, Title = "Deleted author's private recipe" };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        var cache = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider().GetRequiredService<HybridCache>();

        var detail = await new GetRecipeByIdQueryHandler(db, new FakeCurrentUser(), cache)
            .Handle(new GetRecipeByIdQuery(recipe.Id), default);

        detail.Should().BeNull();
    }

    [Fact]
    public async Task PinsAndTheViewersHistoryRankBeforeUnusedRecipes_BeforePaging()
    {
        using var db = new MizanDbContext(new DbContextOptionsBuilder<MizanDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var pinned = new Recipe { Id = Guid.NewGuid(), UserId = userId, Title = "Pinned", CreatedAt = now.AddYears(-1) };
        var recent = new Recipe { Id = Guid.NewGuid(), IsPublic = true, Title = "Used", CreatedAt = now.AddMonths(-1) };
        var unused = new Recipe { Id = Guid.NewGuid(), IsPublic = true, Title = "New", CreatedAt = now };
        var privateRecipe = new Recipe { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Private" };
        db.Recipes.AddRange(pinned, recent, unused, privateRecipe);
        db.FavoriteRecipes.AddRange(new FavoriteRecipe { UserId = userId, RecipeId = pinned.Id },
            new FavoriteRecipe { UserId = userId, RecipeId = privateRecipe.Id });
        db.FoodDiaryEntries.AddRange(
            new FoodDiaryEntry { Id = Guid.NewGuid(), UserId = userId, RecipeId = recent.Id, LoggedAt = now.AddDays(-3) },
            new FoodDiaryEntry { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RecipeId = unused.Id, LoggedAt = now });
        await db.SaveChangesAsync();
        var cache = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider().GetRequiredService<HybridCache>();
        var handler = new GetRecipesQueryHandler(db, new FakeCurrentUser { UserId = userId }, cache);

        var first = await handler.Handle(new GetRecipesQuery { PageSize = 2 }, default);
        first.TotalCount.Should().Be(3);
        first.Items.Select(i => i.Id).Should().Equal(pinned.Id, recent.Id);
        first.Items[0].IsFavorited.Should().BeTrue();
        first.Items[1].LastUsedAt.Should().Be(now.AddDays(-3));
        var favorites = await handler.Handle(new GetRecipesQuery { FavoritesOnly = true }, default);
        favorites.Items.Should().ContainSingle().Which.Id.Should().Be(pinned.Id);
    }
}
