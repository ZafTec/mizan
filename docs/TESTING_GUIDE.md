# MacroChef Testing Guide

**Version:** 1.0
**Last Updated:** 2025-12-27

---

## Table of Contents

- [Overview](#overview)
- [Testing Strategy](#testing-strategy)
- [Backend Testing](#backend-testing)
- [Frontend Testing](#frontend-testing)
- [Integration Testing](#integration-testing)
- [E2E Testing](#e2e-testing)
- [Role-Based Testing](#role-based-testing)
- [CI/CD Integration](#cicd-integration)

---

## Overview

MacroChef follows a **testing pyramid** approach:

```
      /\
     /E2E\      (Few - User flows)
    /------\
   /  Integ \   (Some - API + DB)
  /-----------\
 /    Unit     \ (Many - Pure functions)
/_______________\
```

**Test Preference Order:** E2E > Integration > Unit

---

## Testing Strategy

### Philosophy

**Test contracts, not implementation:**
- If function signature is the contract → test the contract
- Public interfaces and use cases only
- Never test private methods directly

**Never test:**
- Private methods
- Implementation details
- Mocks of things you own
- Getters/setters
- Framework code

**The rule:** If refactoring internals breaks tests but behavior unchanged, tests are bad.

---

## Backend Testing

### Setup

**Technology:**
- **Framework:** xUnit
- **Assertions:** FluentAssertions
- **Mocking:** Moq
- **Database:** Testcontainers (PostgreSQL)

**Test Project Structure:**
```
backend/Mizan.Tests/
├── Commands/           # Command handler tests
├── Queries/            # Query handler tests
├── Authorization/      # Authorization handler tests
├── Integration/        # API endpoint tests
└── Fixtures/           # Test fixtures and helpers
```

### Running Tests

**Docker Compose (Recommended):**
```bash
docker-compose --profile test up test
```

**Local (Fallback):**
```bash
cd backend
ConnectionStrings__PostgreSQL="Host=localhost;Database=mizan_test;Username=mizan;Password=mizan_dev_password" \
  dotnet test
```

**Run Specific Test:**
```bash
dotnet test --filter "FullyQualifiedName~GetRecipeByIdQueryTests.Handle_ExistingRecipe_ReturnsRecipe"
```

### Unit Test Example

**Test Command Handler:**

```csharp
public class CreateRecipeCommandTests
{
    private readonly Mock<IMizanDbContext> _contextMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly CreateRecipeCommandHandler _handler;

    public CreateRecipeCommandTests()
    {
        _contextMock = new Mock<IMizanDbContext>();
        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        _handler = new CreateRecipeCommandHandler(
            _contextMock.Object,
            _currentUserMock.Object
        );
    }

    [Fact]
    public async Task Handle_ValidRecipe_CreatesRecipe()
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Name = "Test Recipe",
            Description = "Test Description",
            Servings = 2,
            Ingredients = new List<RecipeIngredientDto>
            {
                new() { FoodId = Guid.NewGuid(), Quantity = 100, Unit = "g" }
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Recipe");
        _contextMock.Verify(x => x.Recipes.AddAsync(
            It.IsAny<Recipe>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task Handle_Unauthenticated_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);
        var command = new CreateRecipeCommand { Name = "Test" };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None)
        );
    }
}
```

### Authorization Test Example

```csharp
public class GetShoppingListAuthorizationTests
{
    [Fact]
    public async Task GetShoppingList_AsOwner_ReturnsData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var listId = await CreateShoppingListAsync(userId);
        AuthenticateAs(userId);

        // Act
        var response = await Client.GetAsync($"/api/ShoppingLists/{listId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetShoppingList_AsDifferentUser_ReturnsNotFound()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var listId = await CreateShoppingListAsync(ownerId);
        AuthenticateAs(attackerId);

        // Act
        var response = await Client.GetAsync($"/api/ShoppingLists/{listId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

### Integration Test Example

**Using Testcontainers:**

```csharp
public class RecipeIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    public RecipeIntegrationTests()
    {
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("mizan_test")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<MizanDbContext>>();
                    services.AddDbContext<MizanDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString())
                    );
                });
            });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateRecipe_ValidData_ReturnsCreated()
    {
        // Arrange
        var recipe = new CreateRecipeCommand
        {
            Name = "Integration Test Recipe",
            Servings = 2,
            Ingredients = new List<RecipeIngredientDto>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Recipes", recipe);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<RecipeDto>();
        created.Name.Should().Be("Integration Test Recipe");
    }

    public async Task DisposeAsync()
    {
        await _postgres.StopAsync();
    }
}
```

---

## Frontend Testing

### Setup

**Technology:**
- **Framework:** Vitest
- **React Testing:** Testing Library
- **E2E:** Playwright

**Test Structure:**
```
frontend/
├── app/
│   └── __tests__/          # Page tests
├── components/
│   └── __tests__/          # Component tests
└── lib/
    └── __tests__/          # Utility tests
```

### Running Tests

```bash
cd frontend

# Unit/Integration tests
bun run test

# Watch mode
bun run test --watch

# Coverage
bun run test --coverage

# E2E tests
bun run test:e2e
```

### Component Test Example

```typescript
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import RecipeForm from '@/components/RecipeForm';

describe('RecipeForm', () => {
  it('renders all form fields', () => {
    render(<RecipeForm onSubmit={vi.fn()} />);

    expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/servings/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument();
  });

  it('validates required fields', async () => {
    const onSubmit = vi.fn();
    render(<RecipeForm onSubmit={onSubmit} />);

    const submitButton = screen.getByRole('button', { name: /save/i });
    await userEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText(/name is required/i)).toBeInTheDocument();
    });
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('submits form with valid data', async () => {
    const onSubmit = vi.fn();
    render(<RecipeForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(/name/i), 'Test Recipe');
    await userEvent.type(screen.getByLabelText(/servings/i), '2');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        name: 'Test Recipe',
        servings: 2
      });
    });
  });
});
```

### Hook Test Example

```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { useFormValidation } from '@/lib/hooks/useFormValidation';
import { z } from 'zod';

describe('useFormValidation', () => {
  const schema = z.object({
    email: z.string().email(),
    password: z.string().min(8)
  });

  it('validates data against schema', async () => {
    const { result } = renderHook(() => useFormValidation(schema));

    const isValid = await result.current.validate({
      email: 'test@example.com',
      password: 'password123'
    });

    expect(isValid).toBe(true);
    expect(result.current.errors).toEqual({});
  });

  it('returns errors for invalid data', async () => {
    const { result } = renderHook(() => useFormValidation(schema));

    const isValid = await result.current.validate({
      email: 'invalid-email',
      password: 'short'
    });

    expect(isValid).toBe(false);
    expect(result.current.errors).toHaveProperty('email');
    expect(result.current.errors).toHaveProperty('password');
  });
});
```

---

## E2E Testing

### Playwright Configuration

**playwright.config.ts:**

```typescript
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'bun run dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
  },
});
```

### E2E Test Example

```typescript
import { test, expect } from '@playwright/test';

test.describe('Recipe Creation Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[name="email"]', 'test@example.com');
    await page.fill('input[name="password"]', 'password123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL('/dashboard');
  });

  test('creates a new recipe', async ({ page }) => {
    // Navigate to recipes
    await page.click('a[href="/recipes"]');
    await expect(page).toHaveURL('/recipes');

    // Click create button
    await page.click('button:has-text("Create Recipe")');
    await expect(page).toHaveURL('/recipes/new');

    // Fill form
    await page.fill('input[name="name"]', 'E2E Test Recipe');
    await page.fill('input[name="servings"]', '4');
    await page.fill('textarea[name="description"]', 'Created by E2E test');

    // Add ingredient
    await page.click('button:has-text("Add Ingredient")');
    await page.fill('input[name="ingredients.0.quantity"]', '200');

    // Submit
    await page.click('button[type="submit"]');

    // Verify redirect and success
    await expect(page).toHaveURL(/\/recipes\/[a-f0-9-]+/);
    await expect(page.locator('h1')).toContainText('E2E Test Recipe');
  });

  test('displays validation errors', async ({ page }) => {
    await page.goto('/recipes/new');

    // Submit empty form
    await page.click('button[type="submit"]');

    // Check for validation errors
    await expect(page.locator('text=Name is required')).toBeVisible();
    await expect(page.locator('text=Servings must be at least 1')).toBeVisible();
  });
});
```

---

## Role-Based Testing

See `.context/TESTING_ROLES.md` for comprehensive role-based testing scenarios.

### Quick Role Test Matrix

| Feature | User | Trainer | Admin |
|---------|------|---------|-------|
| Create Recipe | ✅ | ✅ | ✅ |
| View Own Meal Plan | ✅ | ✅ | ✅ |
| View Client Data | ❌ | ✅ (with permission) | ✅ |
| Access /admin | ❌ | ❌ | ✅ |
| Access /trainer | ❌ | ✅ | ✅ |
| Ban Users | ❌ | ❌ | ✅ |

### Role Test Example

```csharp
[Theory]
[InlineData("user", "/admin", HttpStatusCode.Forbidden)]
[InlineData("trainer", "/admin", HttpStatusCode.Forbidden)]
[InlineData("admin", "/admin", HttpStatusCode.OK)]
public async Task AdminRoute_Authorization(string role, string path, HttpStatusCode expected)
{
    // Arrange
    AuthenticateAs(role);

    // Act
    var response = await Client.GetAsync(path);

    // Assert
    response.StatusCode.Should().Be(expected);
}
```

---

## CI/CD Integration

### GitHub Actions Example

**.github/workflows/test.yml:**

```yaml
name: Tests

on: [push, pull_request]

jobs:
  backend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0'

      - name: Run tests
        run: docker-compose --profile test up --abort-on-container-exit test

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: backend-test-results
          path: backend/Mizan.Tests/TestResults/

  frontend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup Bun
        uses: oven-sh/setup-bun@v2

      - name: Install dependencies
        working-directory: frontend
        run: bun install

      - name: Run unit tests
        working-directory: frontend
        run: bun run test

      - name: Run E2E tests
        working-directory: frontend
        run: |
          bunx playwright install
          bun run test:e2e

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: frontend-test-results
          path: frontend/test-results/
```

---

## Test Data Management

### Fixtures

**Backend:**

```csharp
public static class TestDataFactory
{
    public static Recipe CreateRecipe(Guid userId)
    {
        return new Recipe
        {
            Id = Guid.NewGuid(),
            Name = "Test Recipe",
            UserId = userId,
            Servings = 2,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Food CreateFood()
    {
        return new Food
        {
            Id = Guid.NewGuid(),
            Name = "Test Food",
            Calories = 100,
            Protein = 10,
            Carbohydrates = 20,
            Fat = 5
        };
    }
}
```

**Frontend:**

```typescript
export const createMockRecipe = (overrides = {}) => ({
  id: '550e8400-e29b-41d4-a716-446655440000',
  name: 'Mock Recipe',
  servings: 2,
  ingredients: [],
  ...overrides
});

export const createMockUser = (role = 'user') => ({
  id: '650e8400-e29b-41d4-a716-446655440001',
  email: 'test@example.com',
  name: 'Test User',
  role
});
```

---

## Coverage Goals

| Type | Target | Current |
|------|--------|---------|
| Backend Unit Tests | 80% | TBD |
| Backend Integration Tests | 60% | TBD |
| Frontend Component Tests | 70% | TBD |
| E2E Critical Paths | 100% | TBD |

**Critical Paths (must have E2E):**
- User sign up/login
- Recipe creation and viewing
- Meal plan creation and editing
- Trainer-client relationship establishment
- Admin user management

---

## Best Practices

1. **Test behavior, not implementation** - Focus on what, not how
2. **Use descriptive test names** - `Test_Scenario_ExpectedResult`
3. **Arrange-Act-Assert** - Clear test structure
4. **One assertion concept per test** - Keep tests focused
5. **Avoid test interdependence** - Tests should run independently
6. **Use factories for test data** - Reusable, maintainable fixtures
7. **Mock external dependencies** - Database, APIs, third-party services
8. **Test edge cases** - Null, empty, max values, invalid data
9. **Keep tests fast** - Unit tests < 100ms, Integration < 1s
10. **Clean up after tests** - Reset database, clear cache

---

## Troubleshooting

**Issue: Testcontainers fails to start**
```bash
# Ensure Docker is running
docker ps

# Check Docker permissions
docker run hello-world
```

**Issue: Frontend tests fail with "Cannot find module"**
```bash
cd frontend
bun install
bun run test --clearCache
```

**Issue: E2E tests timeout**
```bash
# Increase timeout in playwright.config.ts
timeout: 60000  // 60 seconds
```

**Issue: Flaky tests**
- Add explicit waits: `await waitFor()`
- Use `data-testid` instead of text selectors
- Avoid time-based assertions
- Check for race conditions

---

## Additional Resources

- Backend Tests: `backend/Mizan.Tests/`
- Frontend Tests: `frontend/**/__tests__/`
- E2E Tests: `frontend/e2e/`
- Role Testing Guide: `.context/TESTING_ROLES.md`
- CI/CD: `.github/workflows/test.yml`
