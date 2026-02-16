using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Validators;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Application;

public class RecipeCircularDependencyValidatorTests : IDisposable
{
    private readonly MizanDbContext _context;
    private readonly RecipeCircularDependencyValidator _validator;
    private readonly Guid _userId = Guid.NewGuid();

    public RecipeCircularDependencyValidatorTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MizanDbContext(options);
        _validator = new RecipeCircularDependencyValidator(_context);
    }

    [Fact]
    public async Task WouldCreateCircularDependency_ShouldDetect_DirectCircularReference()
    {
        // Arrange: Recipe A → Recipe B, trying to add Recipe A as ingredient to Recipe B
        var recipeA = CreateRecipe("Recipe A");
        var recipeB = CreateRecipe("Recipe B");
        await _context.Recipes.AddRangeAsync(recipeA, recipeB);
        await _context.SaveChangesAsync();

        // Recipe A uses Recipe B as ingredient
        _context.RecipeIngredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeA.Id,
            SubRecipeId = recipeB.Id,
            IngredientText = "Recipe B",
            Amount = 1,
            Unit = "serving"
        });
        await _context.SaveChangesAsync();

        // Act: Try to add Recipe A as ingredient to Recipe B (would create A → B → A)
        var result = await _validator.WouldCreateCircularDependency(recipeB.Id, recipeA.Id);

        // Assert
        result.Should().BeTrue("Recipe B already uses Recipe A, so adding A to B creates a cycle");
    }

    [Fact]
    public async Task WouldCreateCircularDependency_ShouldDetect_SelfReference()
    {
        // Arrange: Trying to add Recipe A as ingredient to itself
        var recipeA = CreateRecipe("Recipe A");
        await _context.Recipes.AddAsync(recipeA);
        await _context.SaveChangesAsync();

        // Act: Try to add Recipe A to itself
        var result = await _validator.WouldCreateCircularDependency(recipeA.Id, recipeA.Id);

        // Assert
        result.Should().BeTrue("A recipe cannot use itself as an ingredient");
    }

    [Fact]
    public async Task WouldCreateCircularDependency_ShouldDetect_IndirectCircularReference()
    {
        // Arrange: Recipe A → Recipe B → Recipe C, trying to add Recipe A to Recipe C
        var recipeA = CreateRecipe("Recipe A");
        var recipeB = CreateRecipe("Recipe B");
        var recipeC = CreateRecipe("Recipe C");
        await _context.Recipes.AddRangeAsync(recipeA, recipeB, recipeC);
        await _context.SaveChangesAsync();

        // Recipe A uses Recipe B
        _context.RecipeIngredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeA.Id,
            SubRecipeId = recipeB.Id,
            IngredientText = "Recipe B",
            Amount = 1,
            Unit = "serving"
        });

        // Recipe B uses Recipe C
        _context.RecipeIngredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeB.Id,
            SubRecipeId = recipeC.Id,
            IngredientText = "Recipe C",
            Amount = 1,
            Unit = "serving"
        });
        await _context.SaveChangesAsync();

        // Act: Try to add Recipe A to Recipe C (would create A → B → C → A)
        var result = await _validator.WouldCreateCircularDependency(recipeC.Id, recipeA.Id);

        // Assert
        result.Should().BeTrue("Recipe C is transitively used by Recipe A, so adding A to C creates a cycle");
    }

    [Fact]
    public async Task WouldCreateCircularDependency_ShouldAllow_IndependentRecipes()
    {
        // Arrange: Recipe A → Recipe B, Recipe C → Recipe D (no connection)
        var recipeA = CreateRecipe("Recipe A");
        var recipeB = CreateRecipe("Recipe B");
        var recipeC = CreateRecipe("Recipe C");
        var recipeD = CreateRecipe("Recipe D");
        await _context.Recipes.AddRangeAsync(recipeA, recipeB, recipeC, recipeD);
        await _context.SaveChangesAsync();

        // Recipe A uses Recipe B
        _context.RecipeIngredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeA.Id,
            SubRecipeId = recipeB.Id,
            IngredientText = "Recipe B",
            Amount = 1,
            Unit = "serving"
        });

        // Recipe C uses Recipe D
        _context.RecipeIngredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeC.Id,
            SubRecipeId = recipeD.Id,
            IngredientText = "Recipe D",
            Amount = 1,
            Unit = "serving"
        });
        await _context.SaveChangesAsync();

        // Act: Try to add Recipe A to Recipe C (should be allowed - no cycle)
        var result = await _validator.WouldCreateCircularDependency(recipeC.Id, recipeA.Id);

        // Assert
        result.Should().BeFalse("Recipe A and Recipe C are in independent dependency chains");
    }

    [Fact]
    public async Task WouldCreateCircularDependency_ShouldAllow_LinearDependencyChain()
    {
        // Arrange: Recipe A → Recipe B → Recipe C → Recipe D (linear chain, no cycle)
        var recipeA = CreateRecipe("Recipe A");
        var recipeB = CreateRecipe("Recipe B");
        var recipeC = CreateRecipe("Recipe C");
        var recipeD = CreateRecipe("Recipe D");
        await _context.Recipes.AddRangeAsync(recipeA, recipeB, recipeC, recipeD);
        await _context.SaveChangesAsync();

        // Recipe A uses Recipe B
        _context.RecipeIngredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeA.Id,
            SubRecipeId = recipeB.Id,
            IngredientText = "Recipe B",
            Amount = 1,
            Unit = "serving"
        });

        // Recipe B uses Recipe C
        _context.RecipeIngredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeB.Id,
            SubRecipeId = recipeC.Id,
            IngredientText = "Recipe C",
            Amount = 1,
            Unit = "serving"
        });
        await _context.SaveChangesAsync();

        // Act: Try to add Recipe D to Recipe C (extends the chain: A → B → C → D)
        var result = await _validator.WouldCreateCircularDependency(recipeC.Id, recipeD.Id);

        // Assert
        result.Should().BeFalse("Adding Recipe D to Recipe C extends the chain but doesn't create a cycle");
    }

    [Fact]
    public async Task WouldCreateCircularDependency_ShouldThrow_WhenMaxDepthExceeded()
    {
        // Arrange: Create a very deep chain (>20 levels)
        var recipes = new List<Recipe>();
        for (int i = 0; i < 25; i++)
        {
            recipes.Add(CreateRecipe($"Recipe {i}"));
        }
        await _context.Recipes.AddRangeAsync(recipes);
        await _context.SaveChangesAsync();

        // Create chain: Recipe 0 → Recipe 1 → Recipe 2 → ... → Recipe 24
        for (int i = 0; i < 24; i++)
        {
            _context.RecipeIngredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipes[i].Id,
                SubRecipeId = recipes[i + 1].Id,
                IngredientText = $"Recipe {i + 1}",
                Amount = 1,
                Unit = "serving"
            });
        }
        await _context.SaveChangesAsync();

        // Act & Assert: Try to check if adding Recipe 0 to Recipe 24 would create a cycle
        // This should throw because the chain is too deep
        var act = async () => await _validator.WouldCreateCircularDependency(recipes[24].Id, recipes[0].Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maximum depth*");
    }

    [Fact]
    public async Task WouldCreateCircularDependency_ShouldHandle_ComplexDiamondStructure()
    {
        // Arrange: Diamond structure
        //     A
        //    / \
        //   B   C
        //    \ /
        //     D
        var recipeA = CreateRecipe("Recipe A");
        var recipeB = CreateRecipe("Recipe B");
        var recipeC = CreateRecipe("Recipe C");
        var recipeD = CreateRecipe("Recipe D");
        await _context.Recipes.AddRangeAsync(recipeA, recipeB, recipeC, recipeD);
        await _context.SaveChangesAsync();

        // Recipe A uses both Recipe B and Recipe C
        _context.RecipeIngredients.AddRange(
            new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeA.Id,
                SubRecipeId = recipeB.Id,
                IngredientText = "Recipe B",
                Amount = 1,
                Unit = "serving",
                SortOrder = 0
            },
            new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeA.Id,
                SubRecipeId = recipeC.Id,
                IngredientText = "Recipe C",
                Amount = 1,
                Unit = "serving",
                SortOrder = 1
            }
        );

        // Both Recipe B and Recipe C use Recipe D
        _context.RecipeIngredients.AddRange(
            new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeB.Id,
                SubRecipeId = recipeD.Id,
                IngredientText = "Recipe D",
                Amount = 1,
                Unit = "serving"
            },
            new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeC.Id,
                SubRecipeId = recipeD.Id,
                IngredientText = "Recipe D",
                Amount = 1,
                Unit = "serving"
            }
        );
        await _context.SaveChangesAsync();

        // Act: Try to add Recipe A to Recipe D (would create a cycle)
        var result = await _validator.WouldCreateCircularDependency(recipeD.Id, recipeA.Id);

        // Assert
        result.Should().BeTrue("Recipe D is used by both B and C, which are both used by A");
    }

    private Recipe CreateRecipe(string title)
    {
        return new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = title,
            Description = $"Test description for {title}",
            Servings = 2,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
