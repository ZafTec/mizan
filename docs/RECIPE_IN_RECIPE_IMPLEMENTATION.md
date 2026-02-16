# Recipe-in-Recipe Implementation Guide

## Overview

This feature allows using entire recipes as ingredients in other recipes. For example:
- Use "Tomato Sauce" recipe as an ingredient in "Lasagna"
- Use "Pizza Dough" recipe as an ingredient in "Homemade Pizza"
- Use "Chicken Stock" recipe as an ingredient in "Chicken Soup"

## Benefits

1. **Reusability**: Define common components once, reuse everywhere
2. **Accuracy**: Nutrition calculated automatically from sub-recipes
3. **Convenience**: Update sub-recipe once, all parent recipes update
4. **Clarity**: Recipe instructions can reference other recipes

---

## Implementation Plan

### Phase 1: Database Schema Changes

#### 1.1 Add `SubRecipeId` to RecipeIngredient

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Domain/Entities/RecipeIngredient.cs`

```csharp
public class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid? FoodId { get; set; }
    public Guid? SubRecipeId { get; set; } // NEW: Link to another recipe
    public string IngredientText { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Unit { get; set; }
    public int SortOrder { get; set; }

    // Navigation properties
    public virtual Recipe Recipe { get; set; } = null!;
    public virtual Food? Food { get; set; }
    public virtual Recipe? SubRecipe { get; set; } // NEW: Navigation to sub-recipe
}
```

**Constraint**: Either `FoodId` OR `SubRecipeId` must be set (not both, not neither, unless `IngredientText` is used for freeform)

#### 1.2 Add Migration

```bash
cd backend
dotnet ef migrations add AddSubRecipeIdToRecipeIngredient \
  --project Mizan.Infrastructure \
  --startup-project Mizan.Api
```

**Migration content** (auto-generated, verify):
```csharp
migrationBuilder.AddColumn<Guid>(
    name: "SubRecipeId",
    table: "recipe_ingredients",
    type: "uuid",
    nullable: true);

migrationBuilder.AddForeignKey(
    name: "FK_recipe_ingredients_recipes_SubRecipeId",
    table: "recipe_ingredients",
    column: "SubRecipeId",
    principalTable: "recipes",
    principalColumn: "id",
    onDelete: ReferentialAction.Restrict); // Prevent cascade delete loops
```

#### 1.3 Update DbContext Configuration

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Infrastructure/Data/MizanDbContext.cs`

Add to `OnModelCreating`:
```csharp
modelBuilder.Entity<RecipeIngredient>()
    .HasOne(ri => ri.SubRecipe)
    .WithMany()
    .HasForeignKey(ri => ri.SubRecipeId)
    .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete issues

// Add check constraint: FoodId OR SubRecipeId must be set (if IngredientText is empty)
modelBuilder.Entity<RecipeIngredient>()
    .ToTable(t => t.HasCheckConstraint(
        "CK_RecipeIngredient_HasFoodOrSubRecipeOrText",
        "(\"FoodId\" IS NOT NULL) OR (\"SubRecipeId\" IS NOT NULL) OR (\"IngredientText\" IS NOT NULL AND \"IngredientText\" <> '')"
    ));
```

#### 1.4 Apply Migration

```bash
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api
```

---

### Phase 2: Backend Logic Changes

#### 2.1 Update CreateRecipeCommand Validation

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Application/Commands/CreateRecipeCommand.cs`

Add validation rule in `CreateRecipeCommandValidator`:
```csharp
RuleForEach(x => x.Ingredients).ChildRules(ingredient =>
{
    ingredient.Custom((ing, context) =>
    {
        var hasFoodId = ing.FoodId.HasValue;
        var hasSubRecipeId = ing.SubRecipeId.HasValue;
        var hasText = !string.IsNullOrWhiteSpace(ing.IngredientText);

        // Exactly one must be set (mutually exclusive)
        if ((hasFoodId && hasSubRecipeId) || (hasFoodId && !hasSubRecipeId && !hasText && ing.Amount == null))
        {
            context.AddFailure("Each ingredient must have either FoodId, SubRecipeId, or IngredientText (not multiple)");
        }

        // If SubRecipeId is set, Amount should represent servings
        if (hasSubRecipeId && ing.Unit != null && ing.Unit != "serving" && ing.Unit != "servings")
        {
            context.AddFailure("When using a recipe as an ingredient, Unit should be 'serving' or 'servings'");
        }
    });
});
```

#### 2.2 Prevent Circular Dependencies

**Create new validator**: `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Application/Validators/RecipeCircularDependencyValidator.cs`

```csharp
public class RecipeCircularDependencyValidator
{
    private readonly IMizanDbContext _context;

    public RecipeCircularDependencyValidator(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<bool> WouldCreateCircularDependency(Guid recipeId, Guid subRecipeId)
    {
        // Check if subRecipeId (directly or indirectly) uses recipeId as an ingredient
        var visited = new HashSet<Guid>();
        return await HasCircularDependency(subRecipeId, recipeId, visited);
    }

    private async Task<bool> HasCircularDependency(Guid currentRecipeId, Guid targetRecipeId, HashSet<Guid> visited)
    {
        if (currentRecipeId == targetRecipeId)
            return true;

        if (visited.Contains(currentRecipeId))
            return false;

        visited.Add(currentRecipeId);

        var subRecipeIds = await _context.RecipeIngredients
            .Where(ri => ri.RecipeId == currentRecipeId && ri.SubRecipeId.HasValue)
            .Select(ri => ri.SubRecipeId!.Value)
            .ToListAsync();

        foreach (var subRecipeId in subRecipeIds)
        {
            if (await HasCircularDependency(subRecipeId, targetRecipeId, visited))
                return true;
        }

        return false;
    }
}
```

**Use in CreateRecipeCommandHandler**:
```csharp
public async Task<RecipeDto> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
{
    // ... existing code ...

    // Validate circular dependencies
    foreach (var ingredient in request.Ingredients.Where(i => i.SubRecipeId.HasValue))
    {
        var validator = new RecipeCircularDependencyValidator(_context);
        if (await validator.WouldCreateCircularDependency(recipe.Id, ingredient.SubRecipeId.Value))
        {
            throw new ValidationException("Cannot add recipe as ingredient: would create circular dependency");
        }
    }

    // ... rest of handler ...
}
```

#### 2.3 Update Nutrition Calculation

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Application/Commands/CreateRecipeCommand.cs` (handler)

When calculating recipe nutrition, include sub-recipe nutrition:

```csharp
private async Task<(decimal calories, decimal protein, decimal carbs, decimal fat, decimal fiber)>
    CalculateRecipeNutrition(List<RecipeIngredientDto> ingredients)
{
    decimal totalCalories = 0, totalProtein = 0, totalCarbs = 0, totalFat = 0, totalFiber = 0;

    foreach (var ingredient in ingredients)
    {
        if (ingredient.FoodId.HasValue)
        {
            // Existing food-based calculation
            var food = await _context.Foods.FindAsync(ingredient.FoodId.Value);
            if (food != null && ingredient.Amount.HasValue)
            {
                var multiplier = ingredient.Amount.Value / 100m;
                totalCalories += food.Calories * multiplier;
                totalProtein += food.Protein * multiplier;
                totalCarbs += food.Carbohydrates * multiplier;
                totalFat += food.Fat * multiplier;
                totalFiber += (food.Fiber ?? 0) * multiplier;
            }
        }
        else if (ingredient.SubRecipeId.HasValue)
        {
            // NEW: Sub-recipe based calculation
            var subRecipe = await _context.Recipes.FindAsync(ingredient.SubRecipeId.Value);
            if (subRecipe != null && ingredient.Amount.HasValue)
            {
                // Amount represents servings of the sub-recipe
                var servings = ingredient.Amount.Value;
                totalCalories += (subRecipe.Calories ?? 0) * servings;
                totalProtein += (subRecipe.Protein ?? 0) * servings;
                totalCarbs += (subRecipe.Carbohydrates ?? 0) * servings;
                totalFat += (subRecipe.Fat ?? 0) * servings;
                totalFiber += (subRecipe.Fiber ?? 0) * servings;
            }
        }
    }

    return (totalCalories, totalProtein, totalCarbs, totalFat, totalFiber);
}
```

#### 2.4 Update GetRecipeByIdQuery

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Application/Queries/GetRecipeByIdQuery.cs`

Include `SubRecipe` in the query:

```csharp
var recipe = await _context.Recipes
    .Include(r => r.RecipeIngredients)
        .ThenInclude(ri => ri.Food)
    .Include(r => r.RecipeIngredients)
        .ThenInclude(ri => ri.SubRecipe) // NEW: Include sub-recipe details
    .Include(r => r.User)
    .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
```

#### 2.5 Update DTOs

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Application/DTOs/RecipeIngredientDto.cs`

```csharp
public class RecipeIngredientDto
{
    public Guid Id { get; set; }
    public Guid? FoodId { get; set; }
    public Guid? SubRecipeId { get; set; } // NEW
    public string? FoodName { get; set; }
    public string? SubRecipeName { get; set; } // NEW: Name of the sub-recipe
    public string IngredientText { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Unit { get; set; }
    public int SortOrder { get; set; }

    // NEW: Nested sub-recipe nutrition info
    public RecipeNutritionSummary? SubRecipeNutrition { get; set; }
}

public class RecipeNutritionSummary
{
    public decimal? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbohydrates { get; set; }
    public decimal? Fat { get; set; }
    public decimal? Fiber { get; set; }
}
```

**Mapping** (in query handler):
```csharp
SubRecipeId = ri.SubRecipeId,
SubRecipeName = ri.SubRecipe?.Name,
SubRecipeNutrition = ri.SubRecipe != null ? new RecipeNutritionSummary
{
    Calories = ri.SubRecipe.Calories,
    Protein = ri.SubRecipe.Protein,
    Carbohydrates = ri.SubRecipe.Carbohydrates,
    Fat = ri.SubRecipe.Fat,
    Fiber = ri.SubRecipe.Fiber
} : null
```

---

### Phase 3: Frontend Implementation

#### 3.1 Update TypeScript Types

After backend changes, run:
```bash
cd frontend
bun run codegen
```

This updates `/Users/nttyy/Documents/_Projects/macro_chef/frontend/types/api.generated.ts` with new `SubRecipeId` field.

#### 3.2 Enhance Ingredient Input Component

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/frontend/components/AddIngredient/index.tsx`

Add recipe search alongside food search:

```tsx
'use client'

import { useState } from 'react'
import { Search } from 'lucide-react'

type IngredientType = 'food' | 'recipe'

export function AddIngredientModal({ onAdd, onClose }) {
  const [ingredientType, setIngredientType] = useState<IngredientType>('food')
  const [searchQuery, setSearchQuery] = useState('')
  const [searchResults, setSearchResults] = useState([])

  const handleSearch = async () => {
    if (ingredientType === 'food') {
      // Existing food search
      const response = await fetch(`/api/Foods?search=${searchQuery}`)
      const data = await response.json()
      setSearchResults(data.items)
    } else {
      // NEW: Recipe search
      const response = await fetch(`/api/Recipes?search=${searchQuery}`)
      const data = await response.json()
      setSearchResults(data.items)
    }
  }

  return (
    <div className="modal">
      {/* Type selector */}
      <div className="flex gap-2 mb-4">
        <button
          className={`btn ${ingredientType === 'food' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setIngredientType('food')}
        >
          <i className="ri-restaurant-line" />
          Food
        </button>
        <button
          className={`btn ${ingredientType === 'recipe' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setIngredientType('recipe')}
        >
          <i className="ri-file-list-3-line" />
          Recipe
        </button>
      </div>

      {/* Search input */}
      <div className="relative mb-4">
        <input
          type="text"
          className="input pl-10"
          placeholder={`Search ${ingredientType}s...`}
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
        />
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 h-5 w-5" />
      </div>

      {/* Search results */}
      <div className="space-y-2 max-h-96 overflow-y-auto">
        {searchResults.map((item) => (
          <div
            key={item.id}
            className="card-hover p-3 flex items-center justify-between cursor-pointer"
            onClick={() => {
              onAdd({
                type: ingredientType,
                id: item.id,
                name: item.name,
                // For recipes, default unit is "serving"
                unit: ingredientType === 'recipe' ? 'serving' : 'g'
              })
              onClose()
            }}
          >
            <div>
              <h4 className="font-medium">{item.name}</h4>
              {ingredientType === 'recipe' && item.servings && (
                <p className="text-sm text-slate-500">
                  {item.servings} servings · {item.calories} cal/serving
                </p>
              )}
              {ingredientType === 'food' && item.caloriesPer100g && (
                <p className="text-sm text-slate-500">
                  {item.caloriesPer100g} cal/100g
                </p>
              )}
            </div>
            <button className="btn-primary">
              <i className="ri-add-line" />
            </button>
          </div>
        ))}
      </div>
    </div>
  )
}
```

#### 3.3 Update Recipe Form Display

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/frontend/components/Recipes/EditRecipeForm.tsx`

Display sub-recipes differently from foods:

```tsx
{recipe.ingredients.map((ingredient, index) => (
  <div key={index} className="card p-4 flex items-center gap-4">
    {/* Icon based on type */}
    <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
      ingredient.subRecipeId
        ? 'bg-purple-100 text-purple-600'
        : 'bg-brand-100 text-brand-600'
    }`}>
      {ingredient.subRecipeId ? (
        <i className="ri-file-list-3-line text-xl" />
      ) : (
        <i className="ri-restaurant-line text-xl" />
      )}
    </div>

    {/* Name and details */}
    <div className="flex-1">
      <h4 className="font-medium">
        {ingredient.subRecipeName || ingredient.foodName || ingredient.ingredientText}
      </h4>
      {ingredient.subRecipeId && ingredient.subRecipeNutrition && (
        <p className="text-sm text-slate-500">
          Sub-recipe · {ingredient.subRecipeNutrition.calories} cal/serving
        </p>
      )}
      {ingredient.foodId && (
        <p className="text-sm text-slate-500">
          Food ingredient
        </p>
      )}
    </div>

    {/* Amount */}
    <div className="flex items-center gap-2">
      <input
        type="number"
        step="0.1"
        className="input w-24"
        value={ingredient.amount || ''}
        onChange={(e) => updateIngredient(index, 'amount', parseFloat(e.target.value))}
      />
      <span className="text-sm text-slate-600">
        {ingredient.subRecipeId ? 'servings' : (ingredient.unit || 'g')}
      </span>
    </div>

    {/* Remove button */}
    <button
      className="btn-secondary text-red-600"
      onClick={() => removeIngredient(index)}
    >
      <i className="ri-delete-bin-line" />
    </button>
  </div>
))}
```

#### 3.4 Show Recipe Dependency Tree

**New component**: `/Users/nttyy/Documents/_Projects/macro_chef/frontend/components/Recipes/RecipeDependencyTree.tsx`

```tsx
'use client'

import { useState, useEffect } from 'react'

interface DependencyNode {
  id: string
  name: string
  level: number
  children: DependencyNode[]
}

export function RecipeDependencyTree({ recipeId }: { recipeId: string }) {
  const [tree, setTree] = useState<DependencyNode | null>(null)

  useEffect(() => {
    fetchDependencyTree(recipeId)
  }, [recipeId])

  const fetchDependencyTree = async (id: string, level = 0): Promise<DependencyNode> => {
    const response = await fetch(`/api/Recipes/${id}`)
    const recipe = await response.json()

    const children = await Promise.all(
      recipe.ingredients
        .filter(ing => ing.subRecipeId)
        .map(ing => fetchDependencyTree(ing.subRecipeId, level + 1))
    )

    return {
      id: recipe.id,
      name: recipe.name,
      level,
      children
    }
  }

  const renderNode = (node: DependencyNode) => (
    <div key={node.id} className="ml-6">
      <div className="flex items-center gap-2 py-2">
        <i className="ri-arrow-right-s-line text-slate-400" />
        <a
          href={`/recipes/${node.id}`}
          className="text-brand-600 hover:underline"
        >
          {node.name}
        </a>
        <span className="text-xs text-slate-500">(Level {node.level})</span>
      </div>
      {node.children.map(renderNode)}
    </div>
  )

  if (!tree) return <div>Loading...</div>

  return (
    <div className="card p-6">
      <h3 className="font-semibold text-lg mb-4">
        <i className="ri-git-branch-line" />
        Recipe Dependencies
      </h3>
      <div className="text-sm">
        <div className="font-medium">{tree.name} (Main Recipe)</div>
        {tree.children.map(renderNode)}
      </div>
      {tree.children.length === 0 && (
        <p className="text-sm text-slate-500 italic">
          No sub-recipes used in this recipe
        </p>
      )}
    </div>
  )
}
```

---

### Phase 4: UI/UX Enhancements

#### 4.1 Visual Distinction

Use different colors/icons for sub-recipes vs foods:
- **Foods**: Green/brand color with fork icon
- **Sub-recipes**: Purple/indigo color with document icon

#### 4.2 Contextual Actions

When viewing a recipe that's used as an ingredient elsewhere:

```tsx
<div className="card p-4 bg-purple-50 border-purple-200">
  <div className="flex items-center gap-3">
    <i className="ri-information-line text-2xl text-purple-600" />
    <div>
      <h4 className="font-medium text-purple-900">
        Used as Ingredient
      </h4>
      <p className="text-sm text-purple-700">
        This recipe is used as an ingredient in 3 other recipes
      </p>
    </div>
    <button className="btn-secondary ml-auto">
      View Uses
    </button>
  </div>
</div>
```

#### 4.3 Serving Size Calculator

When a recipe is used as ingredient, show serving calculator:

```tsx
<div className="card p-4">
  <label className="label">How many servings of "{subRecipe.name}"?</label>
  <div className="flex items-center gap-3">
    <button onClick={() => setServings(s => Math.max(0.5, s - 0.5))}>
      <i className="ri-subtract-line" />
    </button>
    <input
      type="number"
      step="0.5"
      min="0.5"
      className="input w-24 text-center"
      value={servings}
      onChange={(e) => setServings(parseFloat(e.target.value))}
    />
    <span className="text-sm text-slate-600">servings</span>
    <button onClick={() => setServings(s => s + 0.5)}>
      <i className="ri-add-line" />
    </button>
  </div>
  <p className="text-sm text-slate-500 mt-2">
    = {(subRecipe.calories * servings).toFixed(0)} calories total
  </p>
</div>
```

---

### Phase 5: Testing

#### 5.1 Backend Tests

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Tests/Integration/RecipeInRecipeTests.cs`

```csharp
[Collection("ApiIntegration")]
public class RecipeInRecipeTests
{
    private readonly ApiTestFixture _fixture;

    public RecipeInRecipeTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateRecipe_WithSubRecipe_CalculatesNutritionCorrectly()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await _fixture.SeedUserAsync();
        using var client = _fixture.CreateAuthenticatedClient(user.Id, user.Email);

        // Create sub-recipe (Tomato Sauce)
        var subRecipe = new CreateRecipeCommand
        {
            Name = "Tomato Sauce",
            Servings = 4,
            Ingredients = new List<RecipeIngredientDto>
            {
                new() { IngredientText = "Tomatoes", Amount = 500, Unit = "g" }
            }
        };
        var subResponse = await client.PostAsJsonAsync("/api/Recipes", subRecipe);
        var subRecipeDto = await subResponse.Content.ReadFromJsonAsync<RecipeDto>();

        // Create main recipe using sub-recipe
        var mainRecipe = new CreateRecipeCommand
        {
            Name = "Pasta with Tomato Sauce",
            Servings = 2,
            Ingredients = new List<RecipeIngredientDto>
            {
                new() { SubRecipeId = subRecipeDto.Id, Amount = 2, Unit = "serving" },
                new() { IngredientText = "Pasta", Amount = 200, Unit = "g" }
            }
        };
        var mainResponse = await client.PostAsJsonAsync("/api/Recipes", mainRecipe);
        mainResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var mainRecipeDto = await mainResponse.Content.ReadFromJsonAsync<RecipeDto>();
        mainRecipeDto.Ingredients.Should().ContainSingle(i => i.SubRecipeId == subRecipeDto.Id);
        mainRecipeDto.Ingredients.First(i => i.SubRecipeId != null).SubRecipeName.Should().Be("Tomato Sauce");
    }

    [Fact]
    public async Task CreateRecipe_WithCircularDependency_ReturnsValidationError()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await _fixture.SeedUserAsync();
        using var client = _fixture.CreateAuthenticatedClient(user.Id, user.Email);

        // Create Recipe A
        var recipeA = new CreateRecipeCommand { Name = "Recipe A", Servings = 1, Ingredients = new() };
        var responseA = await client.PostAsJsonAsync("/api/Recipes", recipeA);
        var recipeADto = await responseA.Content.ReadFromJsonAsync<RecipeDto>();

        // Try to update Recipe A to include itself (direct circular dependency)
        var updateA = new UpdateRecipeCommand
        {
            Id = recipeADto.Id,
            Name = "Recipe A",
            Servings = 1,
            Ingredients = new List<RecipeIngredientDto>
            {
                new() { SubRecipeId = recipeADto.Id, Amount = 1, Unit = "serving" }
            }
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/Recipes/{recipeADto.Id}", updateA);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRecipe_WithSubRecipe_IncludesSubRecipeDetails()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await _fixture.SeedUserAsync();
        using var client = _fixture.CreateAuthenticatedClient(user.Id, user.Email);

        var subRecipe = await _fixture.SeedRecipeAsync(user.Id, "Sub Recipe");
        var mainRecipe = await _fixture.SeedRecipeWithSubRecipeAsync(user.Id, "Main Recipe", subRecipe.Id);

        var response = await client.GetAsync($"/api/Recipes/{mainRecipe.Id}");
        var dto = await response.Content.ReadFromJsonAsync<RecipeDto>();

        dto.Ingredients.Should().Contain(i => i.SubRecipeId == subRecipe.Id);
        dto.Ingredients.First(i => i.SubRecipeId != null).SubRecipeName.Should().Be("Sub Recipe");
        dto.Ingredients.First(i => i.SubRecipeId != null).SubRecipeNutrition.Should().NotBeNull();
    }
}
```

#### 5.2 Frontend E2E Tests

**File**: `/Users/nttyy/Documents/_Projects/macro_chef/frontend/tests/e2e/recipe-in-recipe.spec.ts`

```typescript
import { test, expect } from '@playwright/test'

test.describe('Recipe in Recipe', () => {
  test('can add recipe as ingredient', async ({ page }) => {
    await page.goto('/recipes/new')

    // Fill basic info
    await page.fill('[name="name"]', 'Test Recipe with Sub-Recipe')
    await page.fill('[name="servings"]', '2')

    // Click "Add Ingredient"
    await page.click('button:has-text("Add Ingredient")')

    // Switch to "Recipe" tab
    await page.click('button:has-text("Recipe")')

    // Search for a recipe
    await page.fill('input[placeholder*="Search"]', 'Tomato Sauce')
    await page.press('input[placeholder*="Search"]', 'Enter')

    // Select first result
    await page.click('[data-testid="recipe-result-0"]')

    // Verify ingredient added
    await expect(page.locator('[data-testid="ingredient-list"]')).toContainText('Tomato Sauce')

    // Verify unit is "servings"
    await expect(page.locator('[data-testid="ingredient-0-unit"]')).toHaveValue('serving')

    // Save recipe
    await page.click('button:has-text("Save Recipe")')

    // Verify success
    await expect(page).toHaveURL(/\/recipes\/[a-f0-9-]+/)
  })

  test('shows recipe dependency tree', async ({ page }) => {
    // Assumes a recipe exists with sub-recipes
    await page.goto('/recipes/[id]')

    // Scroll to dependency tree section
    await page.locator('h3:has-text("Recipe Dependencies")').scrollIntoViewIfNeeded()

    // Verify tree is visible
    await expect(page.locator('[data-testid="dependency-tree"]')).toBeVisible()
  })
})
```

---

### Phase 6: Documentation

#### 6.1 Update API Documentation

Add to `/Users/nttyy/Documents/_Projects/macro_chef/docs/API_REFERENCE.md`:

```markdown
### Recipe Ingredients

Recipes can include three types of ingredients:

1. **Food-based ingredients**: Link to a `Food` entity via `FoodId`
2. **Sub-recipe ingredients**: Link to another `Recipe` entity via `SubRecipeId`
3. **Freeform ingredients**: Text-only via `IngredientText` (no nutrition data)

#### Sub-Recipe Ingredient Example

```json
{
  "name": "Lasagna",
  "servings": 8,
  "ingredients": [
    {
      "subRecipeId": "550e8400-e29b-41d4-a716-446655440000",
      "amount": 2,
      "unit": "serving"
    },
    {
      "foodId": "650e8400-e29b-41d4-a716-446655440000",
      "amount": 500,
      "unit": "g"
    }
  ]
}
```

#### Circular Dependency Prevention

The API prevents circular dependencies where Recipe A uses Recipe B, and Recipe B uses Recipe A (directly or indirectly). If detected, a `400 Bad Request` is returned.
```

#### 6.2 User Guide

Add to `/Users/nttyy/Documents/_Projects/macro_chef/docs/USER_GUIDE.md`:

```markdown
## Using Recipes as Ingredients

You can use entire recipes as ingredients in other recipes. This is useful for:
- Reusing common components (sauces, doughs, stocks)
- Building complex multi-course meals
- Meal prep (combine prepared recipes into final dishes)

### How to Add a Recipe as an Ingredient

1. Navigate to **Recipes** → **Create Recipe**
2. Click **Add Ingredient**
3. Switch to the **Recipe** tab (instead of Food)
4. Search for the recipe you want to use
5. Select the recipe from the results
6. Set the number of **servings** (e.g., "2 servings of Tomato Sauce")
7. The nutrition is calculated automatically based on the sub-recipe's nutrition per serving

### Example: Lasagna with Tomato Sauce

1. Create "Tomato Sauce" recipe (4 servings)
2. Create "Lasagna" recipe
3. Add "Tomato Sauce" as ingredient with amount "2 servings"
4. Lasagna nutrition will include 2 servings worth of Tomato Sauce nutrition

### Tips

- **Servings matter**: The amount represents servings of the sub-recipe
- **Update propagation**: If you update the sub-recipe, parent recipes automatically reflect the changes
- **Dependency tree**: View all recipes that use a particular recipe in the "Used As Ingredient" section
```

---

## Summary

### Database Changes
- Add `SubRecipeId` column to `recipe_ingredients` table
- Add foreign key to `recipes` table with `RESTRICT` delete behavior
- Add check constraint: `FoodId` OR `SubRecipeId` OR `IngredientText` must be set

### Backend Changes
- Update `RecipeIngredient` entity with `SubRecipeId` and navigation property
- Add circular dependency validator
- Update nutrition calculation to include sub-recipe nutrition
- Enhance DTOs to include `SubRecipeName` and `SubRecipeNutrition`
- Update queries to include sub-recipe data

### Frontend Changes
- Add recipe search to ingredient selector
- Visual distinction (purple for recipes, green for foods)
- Display sub-recipe nutrition info
- Show recipe dependency tree
- Serving size calculator for sub-recipes

### Testing
- Backend: Circular dependency tests, nutrition calculation tests
- Frontend: E2E tests for adding recipe ingredients
- Integration: Full flow from creation to usage

---

## Rollout Plan

1. **Phase 1** (Week 1): Database migration + backend validation
2. **Phase 2** (Week 2): Backend nutrition calculation + API updates
3. **Phase 3** (Week 3): Frontend ingredient selector + display
4. **Phase 4** (Week 4): Testing + bug fixes
5. **Phase 5** (Week 5): Documentation + user onboarding

---

**Estimated Effort**: 3-4 weeks
**Complexity**: High (requires careful handling of circular dependencies)
**Priority**: Medium (powerful feature, but not critical for MVP)

---

**Document Version**: 1.0
**Last Updated**: 2026-02-16
**Author**: Claude (AI Assistant)
