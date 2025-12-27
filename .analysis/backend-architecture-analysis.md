# Project Architecture Index: MacroChef (Mizan) Backend

## Executive Summary

MacroChef backend is a production-grade ASP.NET Core 10 Web API implementing Clean Architecture with CQRS pattern. The system handles meal planning, nutrition tracking, fitness monitoring, and trainer-client collaboration with real-time features via SignalR. Architecture emphasizes separation of concerns, testability, and functional core/imperative shell paradigm.

**Primary Technologies:**
- **Language & Runtime**: C# 12 / .NET 10.0
- **Framework**: ASP.NET Core 10 Web API
- **Database**: PostgreSQL 18 (via EF Core 10)
- **Cache**: Redis 7 (SignalR backplane + application caching)
- **Patterns**: Clean Architecture, CQRS, MediatR pipeline
- **Real-time**: SignalR with Redis backplane
- **AI**: Microsoft Semantic Kernel 1.29.0 with OpenAI GPT-4o
- **Testing**: xUnit, FluentAssertions, Moq, WebApplicationFactory

---

## Technology Stack Deep Dive

### Runtime & Framework

#### .NET 10.0 (Latest LTS)
- **Current Version**: 10.0.0
- **Target Framework**: `net10.0`
- **Key Features Used**:
  - Nullable reference types enabled project-wide
  - Implicit usings for cleaner code
  - Record types for immutable DTOs
  - Top-level statements in Program.cs

**Configuration (All .csproj files):**
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

#### ASP.NET Core 10.0 Web API
- **Purpose**: HTTP API layer + SignalR hubs
- **Package**: `Microsoft.AspNetCore.App` (framework reference)
- **Hosting Model**: Kestrel server (Docker optimized)
- **Key Middleware Stack**:
  1. Serilog request logging
  2. CORS (configurable origins)
  3. Authentication (JWT Bearer)
  4. Authorization
  5. MVC Controllers
  6. SignalR Hubs

### Database & ORM

#### Entity Framework Core 10.0.0
- **Purpose**: ORM for PostgreSQL, migrations, change tracking
- **Packages**:
  - `Microsoft.EntityFrameworkCore` (10.0.0)
  - `Microsoft.EntityFrameworkCore.Design` (10.0.0)
  - `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.0)

**Key Patterns**:
- DbContext per request (scoped lifetime)
- Fluent API for entity configuration (all in `OnModelCreating`)
- Snake_case column naming convention
- Automatic timestamp management via `HasDefaultValueSql("NOW()")`
- Lazy loading disabled (explicit `.Include()` everywhere)

**Migration Strategy**:
```bash
# Create migration
dotnet ef migrations add <MigrationName> --project Mizan.Infrastructure --startup-project Mizan.Api

# Apply migrations (automatic in dev via Program.cs)
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api
```

**Existing Migrations** (as of analysis):
1. `20251211145151_InitialCreate` - Initial schema with all core tables
2. `20251219180033_AddGoalProgressTable` - Goal tracking feature
3. `20251220104818_AddBetterAuthFields` - Auth schema sync with frontend

#### PostgreSQL 18
- **Connection**: `ConnectionStrings__PostgreSQL` env var
- **Schema Ownership**: Backend is source of truth for business tables
- **Shared Table**: `households` (coordinated with frontend BetterAuth schema)
- **Features Used**:
  - `gen_random_uuid()` for ID defaults
  - `jsonb` column type for AI chat thread data
  - Composite indexes for performance
  - Foreign key cascades for referential integrity

### Caching & Real-Time

#### Redis 7 (StackExchange.Redis 2.8.16)
- **Purpose**:
  1. SignalR backplane (horizontal scaling)
  2. JWKS cache (1-minute TTL)
  3. Application cache layer (ingredient search, etc.)
- **Connection**: `ConnectionStrings__Redis` env var
- **Fallback**: Graceful degradation to in-memory cache if Redis unavailable

**JWKS Cache Strategy** (`JwksCache.cs`):
- **TTL**: 1 minute
- **Fallback**: In-memory cache with same TTL
- **Stale Cache**: Returns stale data on fetch failure
- **Key Pattern**: `jwks:{url}`
- **Why**: Reduces latency for JWT validation (ES256 verification)

**SignalR Backplane** (`Program.cs`):
```csharp
signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
{
    options.Configuration.ChannelPrefix = "Mizan";
});
```

#### SignalR
- **Packages**:
  - `Microsoft.AspNetCore.SignalR.StackExchangeRedis` (10.0.0)
- **Hubs**: `ChatHub` (`/hubs/chat`)
- **Features**:
  - User-specific groups (`user_{userId}`)
  - Conversation rooms (`conversation_{conversationId}`)
  - Typing indicators
  - Workout progress sync (multi-device)
  - Message notifications

### Validation & Pipeline

#### FluentValidation 11.10.0
- **Purpose**: Input validation for Commands/Queries
- **Integration**: MediatR pipeline behavior (`ValidationBehavior`)
- **Discovery**: Auto-registration via `services.AddValidatorsFromAssembly()`
- **Swagger Integration**: `MicroElements.Swashbuckle.FluentValidation` (6.0.0)

**Example Validator** (`CreateRecipeCommandValidator`):
```csharp
public class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.Ingredients).NotEmpty().WithMessage("At least one ingredient is required");
        RuleForEach(x => x.Ingredients).ChildRules(ingredient =>
        {
            ingredient.RuleFor(i => i.IngredientText).NotEmpty();
        });
    }
}
```

#### MediatR 12.4.1
- **Purpose**: CQRS mediator, request/response pipeline
- **Pipeline Behaviors** (executed in order):
  1. `LoggingBehavior` - Request timing and error logging
  2. `ValidationBehavior` - FluentValidation enforcement
  3. Command/Query Handler

**Configuration** (`DependencyInjection.cs`):
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});
```

### Authentication & Security

#### JWT Bearer (ES256) with BetterAuth
- **Package**: `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.0)
- **Algorithm**: ES256 (ECDSA P-256) - EdDSA compatible
- **Issuer**: Frontend BetterAuth service (`http://localhost:3000`)
- **Audience**: `mizan-api`
- **Token Lifetime**: 15 minutes (JWT), 7 days (session)
- **JWKS URL**: `{BETTERAUTH_JWKS_URL}` (configurable)

**Key Validation Parameters** (`JwtBearerOptionsSetup.cs`):
```csharp
ValidateIssuer = true,
ValidIssuer = _configuration["BetterAuth:Issuer"] ?? "http://localhost:3000",
ValidateAudience = true,
ValidAudience = _configuration["BetterAuth:Audience"] ?? "mizan-api",
ValidateIssuerSigningKey = true,
ValidateLifetime = true,
ClockSkew = TimeSpan.FromMinutes(5)
```

**SignalR Token Handling**:
- Query string token support for WebSocket connections
- Path-based token detection (`/hubs/*`)
- Same JWT validation as HTTP requests

**Current User Service** (`CurrentUserService.cs`):
- Extracts user ID from JWT claims (`ClaimTypes.NameIdentifier` or `sub`)
- Scoped service (per-request)
- Used by all Commands/Queries for authorization

### AI Integration

#### Microsoft Semantic Kernel 1.29.0
- **Purpose**: AI-powered nutrition advice and food image analysis
- **Model**: GPT-4o (configurable via `OpenAI:ModelId`)
- **Plugins**: `NutritionPlugin` for function calling
- **Features**:
  - Auto-invoke kernel functions
  - Multi-modal (text + image)
  - Context-aware chat with tools

**AI Services**:
1. **NutritionAiService** (`INutritionAiService`):
   - Chat-based nutrition advice
   - Food image analysis (portion estimation)
   - JSON-structured responses
   - System prompt: "Mizan AI" (Amharic for 'balance')

2. **NutritionPlugin** (Semantic Kernel plugin):
   - Exposes application methods as AI tools
   - Allows LLM to query database and log food entries

### Logging & Observability

#### Serilog 10.0.0
- **Sinks**: Console (`Serilog.Sinks.Console` 6.1.1)
- **Enrichment**: LogContext for structured logging
- **Configuration**: `appsettings.json` + environment variables
- **Request Logging**: `UseSerilogRequestLogging()` middleware

**Log Levels** (MediatR Pipeline):
- **Info**: Request start, request completion, timing
- **Error**: Exceptions with request context
- **Debug**: JWKS cache hits/misses, key parsing

### API Documentation

#### Swagger/OpenAPI (Swashbuckle 6.9.0)
- **Endpoint**: `/swagger` (dev only)
- **Security**: Bearer token UI integration
- **Validation**: FluentValidation rules reflected in schema
- **Code Generation**: Frontend uses this spec for TypeScript types

### Health Checks

#### AspNetCore.HealthChecks (9.0.0)
- **Packages**:
  - `AspNetCore.HealthChecks.NpgSql` (9.0.0)
  - `AspNetCore.HealthChecks.Redis` (9.0.0)
- **Endpoint**: `/health`
- **Checks**: PostgreSQL connection, Redis availability

---

## Architecture Overview

### Clean Architecture Layers

```
┌─────────────────────────────────────────────┐
│         Mizan.Api (Presentation)            │
│  - Controllers (HTTP endpoints)             │
│  - SignalR Hubs (real-time)                 │
│  - Middleware (auth, CORS, logging)         │
│  - Program.cs (DI container)                │
└─────────────────┬───────────────────────────┘
                  │ depends on
┌─────────────────▼───────────────────────────┐
│      Mizan.Application (Use Cases)          │
│  - Commands (write operations)              │
│  - Queries (read operations)                │
│  - DTOs (data transfer objects)             │
│  - Validators (FluentValidation)            │
│  - Pipeline Behaviors (logging, validation) │
│  - Interfaces (abstractions)                │
└─────────────────┬───────────────────────────┘
                  │ depends on
┌─────────────────▼───────────────────────────┐
│        Mizan.Domain (Core Logic)            │
│  - Entities (business objects)              │
│  - Value Objects (immutable concepts)       │
│  - Domain Events (future)                   │
│  - NO DEPENDENCIES (pure C#)                │
└─────────────────────────────────────────────┘
                  ▲
                  │ implements
┌─────────────────┴───────────────────────────┐
│    Mizan.Infrastructure (External I/O)      │
│  - MizanDbContext (EF Core)                 │
│  - Migrations                               │
│  - Services (CurrentUser, AI, Cache)        │
│  - External API clients (future)            │
└─────────────────────────────────────────────┘
```

**Dependency Rules**:
- Domain has ZERO dependencies (pure business logic)
- Application depends on Domain only
- Infrastructure implements Application interfaces
- API depends on Application + Infrastructure (composition root)

### CQRS Implementation

**Command Flow** (Write Operations):
```
HTTP POST → Controller → MediatR.Send(Command)
  → ValidationBehavior (FluentValidation)
    → LoggingBehavior (timing, errors)
      → CommandHandler
        → IMizanDbContext (write to DB)
          → SaveChangesAsync()
            → Return Result DTO
```

**Query Flow** (Read Operations):
```
HTTP GET → Controller → MediatR.Send(Query)
  → LoggingBehavior (timing, errors)
    → QueryHandler
      → IMizanDbContext (read from DB)
        → Return Result DTO
```

**Why CQRS?**
- Clear separation: Commands mutate state, Queries don't
- Different validation needs (commands strict, queries flexible)
- Future: Separate read/write databases (CQRS scaling)
- Testability: Handlers are pure functions (given context, produce result)

### Component Structure

#### Domain Entities (38 files)

**Core Categories**:

1. **Authentication** (BetterAuth compatibility):
   - `User` - Core user entity (shared with frontend)
   - `Account` - OAuth provider links
   - `Session` - Active user sessions
   - `Jwk` - JSON Web Keys for JWT validation
   - `Verification` - Email/phone verification codes

2. **Household/Organization**:
   - `Household` - Shared family/group container
   - `HouseholdMember` - User-household join with permissions

3. **Food & Nutrition**:
   - `Food` - Ingredient database (calories, macros)
   - `Recipe` - User-created recipes
   - `RecipeIngredient` - Recipe composition
   - `RecipeInstruction` - Step-by-step cooking
   - `RecipeNutrition` - Calculated nutrition per serving
   - `RecipeTag` - Categorization (vegetarian, ethiopian, etc.)
   - `FoodDiaryEntry` - Daily food log

4. **Meal Planning**:
   - `MealPlan` - Weekly/monthly meal schedules
   - `MealPlanRecipe` - Assigned recipes per day/meal
   - `ShoppingList` - Grocery lists
   - `ShoppingListItem` - Individual items with checkboxes

5. **Goals & Progress**:
   - `UserGoal` - Target calories/macros/weight
   - `GoalProgress` - Daily tracking against goals

6. **Fitness**:
   - `Exercise` - Exercise library (squats, bench press, etc.)
   - `Workout` - Workout sessions
   - `WorkoutExercise` - Exercises in a workout
   - `ExerciseSet` - Individual sets (reps, weight, duration)
   - `BodyMeasurement` - Weight, body fat, measurements

7. **Trainer/Client**:
   - `TrainerClientRelationship` - Permission-based connections
   - `ChatConversation` - 1-on-1 messaging
   - `ChatMessage` - Individual messages

8. **Gamification**:
   - `Achievement` - Unlockable badges
   - `UserAchievement` - Earned achievements
   - `Streak` - Consecutive activity tracking

9. **AI**:
   - `AiChatThread` - Persistent AI conversation context

**Entity Design Patterns**:
- **Anemic Domain Model**: Entities are data containers, logic in handlers
- **Virtual Navigation Properties**: EF Core lazy loading (disabled, but convention kept)
- **Guid Primary Keys**: `gen_random_uuid()` for distributed systems
- **Audit Timestamps**: `CreatedAt`, `UpdatedAt` on most entities
- **Soft Deletes**: Not used (hard deletes with cascades)

**Critical Entity: User** (`User.cs`):
```csharp
/// <summary>
/// User entity - MUST match frontend schema: frontend/db/schema.ts (users table)
/// ⚠️ When updating this entity, ensure frontend schema is updated first (source of truth)
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string? Name { get; set; }
    public string? Image { get; set; }
    public string Role { get; set; } = "user";
    public bool Banned { get; set; } = false;
    public string? BanReason { get; set; }
    public DateTime? BanExpires { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Rich navigation properties (14 collections)
}
```

**Schema Coordination**:
- Frontend Drizzle schema is source of truth for `users`, `accounts`, `sessions`, `jwks`, `verification`
- Backend EF Core must mirror these exactly
- `households` table: Backend creates, frontend references
- Comment in User.cs emphasizes this critical boundary

#### Application Commands (26 files)

**Pattern**: `IRequest<TResult>` records with validators and handlers

**Examples by Category**:

1. **Food Logging**:
   - `LogFoodCommand` - Add diary entry
   - `CreateFoodCommand` - Add new food to database
   - `CreateFoodDiaryEntryCommand` - Explicit diary entry

2. **Recipes**:
   - `CreateRecipeCommand` - Create with ingredients/instructions
   - `AddRecipeToMealPlanCommand` - Schedule recipe

3. **Meal Planning**:
   - `CreateMealPlanCommand` - New meal plan
   - `DeleteMealPlanCommand` - Remove plan

4. **Shopping**:
   - `CreateShoppingListCommand` - New list
   - `AddShoppingListItemCommand` - Add item
   - `UpdateShoppingListItemCommand` - Edit item
   - `ToggleShoppingListItemCommand` - Check/uncheck
   - `DeleteShoppingListCommand` - Remove list

5. **Fitness**:
   - `LogWorkoutCommand` - Record workout session
   - `CreateExerciseCommand` - Add custom exercise
   - `CreateBodyMeasurementCommand` - Log weight/measurements
   - `DeleteBodyMeasurementCommand` - Remove entry

6. **Goals**:
   - `CreateUserGoalCommand` - Set new goal
   - `UpdateStreakCommand` - Increment/reset streaks

7. **Trainer/Client**:
   - `SendTrainerRequestCommand` - Request connection
   - `RespondToTrainerRequestCommand` - Accept/reject
   - `SendChatMessageCommand` - Send message (also via SignalR)

8. **Households**:
   - `CreateHouseholdCommand` - Create household
   - `AddHouseholdMemberCommand` - Invite user

9. **User Management**:
   - `UpdateUserCommand` - Update profile

**Command Structure** (example: `CreateRecipeCommand.cs`):
```csharp
// Request record (immutable DTO)
public record CreateRecipeCommand : IRequest<CreateRecipeResult>
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Servings { get; init; } = 1;
    public List<CreateRecipeIngredientDto> Ingredients { get; init; } = new();
    public List<string> Instructions { get; init; } = new();
    public CreateRecipeNutritionDto? Nutrition { get; init; }
}

// Validator (auto-discovered)
public class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.Ingredients).NotEmpty();
    }
}

// Handler (application logic)
public class CreateRecipeCommandHandler : IRequestHandler<CreateRecipeCommand, CreateRecipeResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public async Task<CreateRecipeResult> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
    {
        // Authorization check
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User must be authenticated");

        // Create entity
        var recipe = new Recipe { /* ... */ };

        // Populate collections
        for (int i = 0; i < request.Ingredients.Count; i++) { /* ... */ }

        // Persist
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateRecipeResult { Id = recipe.Id, Title = recipe.Title };
    }
}
```

**Command Patterns**:
- **Immutable Requests**: Records with `init` properties
- **Explicit Results**: No void returns, always return DTO
- **Authorization**: Check `ICurrentUserService` in handler
- **Validation**: Declarative via FluentValidation
- **No Business Logic in Validators**: Only structural/format validation
- **Entity Construction**: New entities created in handler, not in domain

#### Application Queries (20 files)

**Pattern**: `IRequest<TResult>` records, read-only operations

**Examples by Category**:

1. **Nutrition**:
   - `GetDailyNutritionQuery` - Daily macro summary
   - `GetFoodDiaryQuery` - Food log for date range

2. **Recipes**:
   - `GetRecipesQuery` - List with filters (user/household/public)
   - `GetRecipeByIdQuery` - Single recipe detail

3. **Meal Planning**:
   - `GetMealPlansQuery` - User's meal plans
   - `GetMealPlanByIdQuery` - Plan detail with recipes

4. **Shopping**:
   - `GetShoppingListsQuery` - User's shopping lists
   - `GetShoppingListByIdQuery` - List detail with items
   - `GetShoppingListQuery` - Alias/alternate query

5. **Fitness**:
   - `GetExercisesQuery` - Exercise library
   - `GetBodyMeasurementsQuery` - Measurement history

6. **Goals**:
   - `GetUserGoalQuery` - Active goal
   - `GetStreakQuery` - Streak status

7. **Trainer/Client**:
   - `GetChatConversationQuery` - Message history

8. **Households**:
   - `GetHouseholdQuery` - Household members and permissions

9. **Gamification**:
   - `GetAchievementsQuery` - Available/earned achievements

10. **User**:
    - `GetUserQuery` - User profile

**Query Structure** (example: `GetDailyNutritionQuery.cs`):
```csharp
// Request
public record GetDailyNutritionQuery : IRequest<DailyNutritionResult>
{
    public DateOnly Date { get; init; }
}

// Result DTO (projection from entities)
public record DailyNutritionResult
{
    public DateOnly Date { get; init; }
    public int TotalCalories { get; init; }
    public decimal TotalProtein { get; init; }
    public decimal TotalCarbs { get; init; }
    public decimal TotalFat { get; init; }
    public int? TargetCalories { get; init; }
    public decimal? TargetProtein { get; init; }
    public decimal? TargetCarbs { get; init; }
    public decimal? TargetFat { get; init; }
    public List<MealSummary> MealBreakdown { get; init; } = new();
}

// Handler
public class GetDailyNutritionQueryHandler : IRequestHandler<GetDailyNutritionQuery, DailyNutritionResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public async Task<DailyNutritionResult> Handle(GetDailyNutritionQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var entries = await _context.FoodDiaryEntries
            .Where(e => e.UserId == _currentUser.UserId && e.EntryDate == request.Date)
            .ToListAsync(cancellationToken);

        var goal = await _context.UserGoals
            .Where(g => g.UserId == _currentUser.UserId && g.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        var mealBreakdown = entries
            .GroupBy(e => e.MealType)
            .Select(g => new MealSummary { /* ... */ })
            .ToList();

        return new DailyNutritionResult { /* aggregate data */ };
    }
}
```

**Query Patterns**:
- **No Validators**: Queries have looser validation (optional)
- **Projections**: Return DTOs, not entities (prevent over-fetching)
- **Authorization**: User-scoped queries (filter by `_currentUser.UserId`)
- **LINQ Queries**: Entity Framework LINQ, materialized with `ToListAsync()`
- **Aggregations**: In-memory LINQ after fetching (small datasets)

#### Application Interfaces (4 files)

**Purpose**: Abstractions for cross-cutting concerns

1. **`IMizanDbContext`** - Database abstraction
   ```csharp
   public interface IMizanDbContext
   {
       DbSet<User> Users { get; }
       DbSet<Food> Foods { get; }
       // ... all 38 DbSets
       Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
   }
   ```
   - **Why**: Testability (mock DB), layer separation
   - **Implementation**: `MizanDbContext` in Infrastructure

2. **`ICurrentUserService`** - Request-scoped user context
   ```csharp
   public interface ICurrentUserService
   {
       Guid? UserId { get; }
       string? Email { get; }
       bool IsAuthenticated { get; }
   }
   ```
   - **Why**: Authorization without coupling to HttpContext
   - **Implementation**: `CurrentUserService` (extracts JWT claims)

3. **`INutritionAiService`** - AI-powered nutrition features
   ```csharp
   public interface INutritionAiService
   {
       Task<string> GetNutritionAdviceAsync(Guid userId, string userMessage, CancellationToken cancellationToken = default);
       Task<FoodAnalysisResult> AnalyzeFoodImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
   }
   ```
   - **Why**: AI as external concern, swappable providers
   - **Implementation**: `NutritionAiService` (Semantic Kernel + OpenAI)

4. **`IRedisCacheService`** - Generic cache abstraction
   ```csharp
   public interface IRedisCacheService
   {
       Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
       Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
       Task RemoveAsync(string key, CancellationToken cancellationToken = default);
       Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
   }
   ```
   - **Why**: Cache invalidation patterns, testability
   - **Implementation**: `RedisCacheService` (graceful Redis fallback)

#### Controllers (16 files)

**Pattern**: Thin controllers, delegate to MediatR

**Structure**:
```csharp
[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpGet]
    public async Task<ActionResult<GetRecipesResult>> GetRecipes([FromQuery] GetRecipesQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CreateRecipeResult>> CreateRecipe([FromBody] CreateRecipeCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetRecipeById), new { id = result.Id }, result);
    }
}
```

**Controllers List**:
1. `RecipesController` - Recipe CRUD
2. `MealsController` - Meal plan CRUD
3. `MealPlansController` - Meal plan management
4. `NutritionController` - Food logging, diary, goals
5. `WorkoutsController` - Workout logging
6. `ExercisesController` - Exercise library
7. `BodyMeasurementsController` - Body tracking
8. `ShoppingListsController` - Shopping list CRUD
9. `AchievementsController` - Gamification
10. `TrainersController` - Trainer/client relationships
11. `HouseholdsController` - Household management
12. `ChatController` - Chat (HTTP fallback to SignalR)
13. `HealthController` - Health checks
14. (2-3 more inferred from Commands/Queries)

**Controller Patterns**:
- **Authorize Attribute**: Applied per-action (not controller-level for mixed public/auth endpoints)
- **Model Binding**: `[FromQuery]`, `[FromBody]`, route parameters
- **Action Results**: `ActionResult<T>` for typed responses
- **HTTP Verbs**: `HttpGet`, `HttpPost`, `HttpPut`, `HttpDelete`
- **Created Response**: `CreatedAtAction()` for POST operations
- **No Business Logic**: All logic in handlers

#### SignalR Hub (ChatHub.cs)

**Purpose**: Real-time trainer-client chat and workout sync

**Features**:
1. **Connection Management**:
   - Auto-join user group on connect (`user_{userId}`)
   - Manual join/leave conversation rooms
   - Connection ID tracking

2. **Chat Messaging**:
   - `SendMessage()` - Sends via MediatR Command, broadcasts to conversation
   - `ReceiveMessage` - Client event for incoming messages
   - `NewMessageNotification` - Push notification to recipient

3. **Typing Indicators**:
   - `TypingIndicator()` - Broadcast typing status to conversation

4. **Workout Sync**:
   - `SyncWorkoutProgress()` - Multi-device workout state sync
   - `WorkoutProgressUpdated` - Client event for updates

**Hub Pattern**:
```csharp
[Authorize]
public class ChatHub : Hub
{
    private readonly IMediator _mediator;

    public async Task SendMessage(ChatMessageDto message)
    {
        var userId = GetUserId();

        // Persist via CQRS
        var command = new SendChatMessageCommand(message.ConversationId, userId.Value, message.Content, message.MessageType);
        var result = await _mediator.Send(command);

        // Broadcast to conversation group
        await Clients.Group($"conversation_{message.ConversationId}")
            .SendAsync("ReceiveMessage", outgoingMessage);

        // Notify recipient
        await Clients.Group($"user_{result.RecipientId}")
            .SendAsync("NewMessageNotification", notification);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
```

**SignalR + Redis Backplane**:
- Horizontal scaling support
- Channel prefix: `Mizan`
- Message routing across server instances
- Connection affinity not required

---

## Database Schema (EF Core Configuration)

### Configuration Approach

**All configuration in `OnModelCreating()`** - No data annotations on entities

**Conventions**:
- **Table Names**: Snake_case (e.g., `users`, `food_diary_entries`)
- **Column Names**: Snake_case (e.g., `created_at`, `user_id`)
- **Primary Keys**: `id` (Guid) with `gen_random_uuid()`
- **Timestamps**: `created_at`, `updated_at` with `NOW()` default
- **Foreign Keys**: `{entity}_id` (e.g., `user_id`, `recipe_id`)
- **Precision**: `HasPrecision(8, 2)` for decimal macros

### Key Relationships

**User-Centric Relationships**:
```csharp
User
  ├── Accounts (1:N, cascade delete)
  ├── Sessions (1:N, cascade delete)
  ├── HouseholdMemberships (N:M via HouseholdMember)
  ├── Recipes (1:N, set null on delete)
  ├── FoodDiaryEntries (1:N, cascade delete)
  ├── Workouts (1:N, cascade delete)
  ├── CurrentGoal (1:1, cascade delete)
  ├── Achievements (N:M via UserAchievement)
  ├── Streaks (1:N, cascade delete)
  ├── TrainerRelationships (1:N as trainer, cascade delete)
  └── ClientRelationships (1:N as client, cascade delete)
```

**Recipe Composition**:
```csharp
Recipe
  ├── Ingredients (1:N RecipeIngredient, cascade delete)
  ├── Instructions (1:N RecipeInstruction, cascade delete)
  ├── Nutrition (1:1 RecipeNutrition, cascade delete)
  ├── Tags (1:N RecipeTag, cascade delete)
  ├── MealPlanRecipes (1:N, cascade delete)
  └── DiaryEntries (1:N FoodDiaryEntry, set null on delete)
```

**Household Sharing**:
```csharp
Household
  ├── Members (N:M via HouseholdMember, cascade delete)
  ├── Recipes (1:N, set null on delete)
  ├── MealPlans (1:N, set null on delete)
  └── ShoppingLists (1:N, set null on delete)
```

**Workout Structure**:
```csharp
Workout
  ├── Exercises (1:N WorkoutExercise, cascade delete)
      └── Sets (1:N ExerciseSet, cascade delete)
```

**Delete Behaviors**:
- **Cascade**: Child records deleted with parent (owned data)
- **SetNull**: Child survives, foreign key nulled (shared data)
- **Restrict**: Not used (would block deletion)

### Indexes

**Performance Indexes** (defined in `OnModelCreating`):

1. **Unique Indexes**:
   - `users.email` (login lookup)
   - `sessions.token` (session validation)
   - `trainer_client_relationships.{trainer_id, client_id}` (prevent duplicates)
   - `goal_progress.{user_id, date}` (one progress per day)

2. **Composite Indexes**:
   - `food_diary_entries.{user_id, entry_date}` (daily nutrition queries)
   - `goal_progress.{user_id, date}` (goal tracking)

3. **Foreign Key Indexes** (auto-created by PostgreSQL):
   - All `{entity}_id` columns indexed
   - Join performance optimization

### Data Types

**Special PostgreSQL Types**:
- `jsonb` for `AiChatThread.ThreadData` (flexible AI context storage)
- `decimal(P, S)` for monetary/precise values
- `timestamp` for `DateTime` (UTC assumed)
- `uuid` for `Guid`

**Precision Patterns**:
- **Macros**: `decimal(8, 2)` (e.g., 99999.99g protein)
- **Measurements**: `decimal(6, 2)` (e.g., 9999.99kg)
- **Large decimals**: `decimal(10, 2)` (e.g., ingredient amounts)

---

## Dataflow Analysis

### Request Lifecycle (Typical Flow)

**Command Example: Creating a Recipe**

```
1. HTTP POST /api/recipes
   Body: { title: "Doro Wat", ingredients: [...], instructions: [...] }
   Headers: Authorization: Bearer {JWT}

2. ASP.NET Core Middleware Stack:
   - Serilog logs request start
   - CORS check (AllowFrontend policy)
   - JWT validation (JwtBearerOptionsSetup)
     → Fetch JWKS from cache/BetterAuth endpoint
     → Validate signature, issuer, audience, expiration
     → Populate ClaimsPrincipal (UserId, Email)
   - Authorization (check [Authorize] attribute)
   - Model binding (deserialize to CreateRecipeCommand)

3. RecipesController.CreateRecipe():
   - Inject IMediator
   - Send command to MediatR pipeline

4. MediatR Pipeline:
   a. LoggingBehavior (start timer, log request name)
   b. ValidationBehavior:
      - Resolve CreateRecipeCommandValidator
      - Run validation rules
      - Throw ValidationException if invalid (400 response)
   c. CreateRecipeCommandHandler:
      - Resolve IMizanDbContext (scoped DbContext)
      - Resolve ICurrentUserService (extract UserId from claims)
      - Check authorization (UserId not null)
      - Create Recipe entity
      - Populate child collections (Ingredients, Instructions, Tags, Nutrition)
      - Add to DbSet: _context.Recipes.Add(recipe)
      - SaveChangesAsync (EF Core change tracking)
        → Generate SQL INSERT statements
        → Execute in transaction
        → PostgreSQL returns generated IDs
      - Return CreateRecipeResult DTO
   d. LoggingBehavior (stop timer, log duration)

5. Controller:
   - Receive result from handler
   - Return CreatedAtAction (201 status, Location header)

6. Middleware Stack (response):
   - Serilog logs response status + duration
   - Serialize result to JSON (System.Text.Json)
   - Send HTTP response

7. Client receives:
   Status: 201 Created
   Location: /api/recipes/{guid}
   Body: { id: "{guid}", title: "Doro Wat" }
```

**Query Example: Getting Daily Nutrition**

```
1. HTTP GET /api/nutrition/daily?date=2025-12-25
   Headers: Authorization: Bearer {JWT}

2. Middleware (same as above, no body parsing)

3. NutritionController.GetDailyNutrition():
   - Model bind query string to GetDailyNutritionQuery
   - Send to MediatR

4. MediatR Pipeline:
   a. LoggingBehavior
   b. GetDailyNutritionQueryHandler:
      - Get UserId from ICurrentUserService
      - Query FoodDiaryEntries (filtered by UserId and Date)
        → EF Core generates SQL SELECT with WHERE clause
        → PostgreSQL returns rows
        → Materialize as List<FoodDiaryEntry>
      - Query UserGoals (filtered by UserId and IsActive)
        → SQL SELECT with WHERE clause
        → FirstOrDefaultAsync (nullable goal)
      - LINQ aggregation (in-memory):
        → GroupBy MealType
        → Sum calories/macros per meal
        → Calculate totals
      - Construct DailyNutritionResult DTO
   c. LoggingBehavior (log duration)

5. Controller returns Ok(result) (200 status)

6. Client receives:
   Status: 200 OK
   Body: {
     date: "2025-12-25",
     totalCalories: 2150,
     totalProtein: 165.5,
     targetCalories: 2200,
     mealBreakdown: [...]
   }
```

### State Management

**Request-Scoped State**:
- `HttpContext.User` (ClaimsPrincipal from JWT)
- `ICurrentUserService` (scoped, extracts from ClaimsPrincipal)
- `MizanDbContext` (scoped, per-request DB connection)

**Application State**:
- **No In-Memory State** - Stateless API
- **Session State**: PostgreSQL (`sessions` table)
- **User State**: PostgreSQL (`users` table)
- **Cache State**: Redis (ephemeral, optional)

**Transaction Management**:
- **Implicit Transactions**: `SaveChangesAsync()` wraps in transaction
- **No Manual Transactions**: EF Core change tracker handles all writes
- **Idempotency**: Not enforced (client responsibility for retry logic)

### External Service Integrations

**BetterAuth (Frontend)**:
- **Endpoint**: `{BETTERAUTH_JWKS_URL}` (default: `http://localhost:3000/api/auth/jwks`)
- **Protocol**: HTTP GET for JWKS
- **Cache**: 1-minute Redis cache + in-memory fallback
- **Failure Mode**: Return stale cache if available, else fail auth

**OpenAI API** (Nutrition AI):
- **Model**: GPT-4o (via Semantic Kernel)
- **Endpoints**:
  - Chat completions (nutrition advice)
  - Vision API (food image analysis)
- **Authentication**: API key (`OpenAI:ApiKey`)
- **Timeout**: Default HttpClient timeout (100 seconds)
- **Error Handling**: Graceful fallback (return generic message)

**Redis**:
- **Connection**: `ConnectionStrings__Redis` (default: `localhost:6379`)
- **Multiplexer**: Singleton `IConnectionMultiplexer`
- **Failure Mode**: Silent fail, cache operations return null/noop
- **Reconnection**: Automatic via StackExchange.Redis

**PostgreSQL**:
- **Connection**: `ConnectionStrings__PostgreSQL`
- **Pooling**: Default ADO.NET connection pooling
- **Health Check**: `/health` endpoint checks connectivity
- **Migration**: Auto-apply in dev via `db.Database.Migrate()`

---

## Environment & Configuration

### Development Environment

**Required Services**:
- PostgreSQL 18 (database)
- Redis 7 (cache + SignalR backplane)
- OpenAI API key (optional, for AI features)

**Environment Variables** (backend):
```env
ConnectionStrings__PostgreSQL=Host=localhost;Database=mizan;Username=mizan;Password=mizan_dev_password
ConnectionStrings__Redis=localhost:6379
BetterAuth__JwksUrl=http://localhost:3000/api/auth/jwks
BetterAuth__Issuer=http://localhost:3000
BetterAuth__Audience=mizan-api
OpenAI__ApiKey=sk-...
OpenAI__ModelId=gpt-4o
Cors__Origins__0=http://localhost:3000
```

**Docker Compose** (recommended workflow):
```yaml
services:
  backend:
    build: ./backend
    environment:
      ConnectionStrings__PostgreSQL: Host=postgres;Database=mizan;Username=mizan;Password=mizan_dev_password
      ConnectionStrings__Redis: redis:6379
      BetterAuth__JwksUrl: http://frontend:3000/api/auth/jwks
    depends_on:
      - postgres
      - redis
  postgres:
    image: postgres:18-alpine
  redis:
    image: redis:7-alpine
```

**Local Development** (without Docker):
```bash
# Start services
docker run -d -p 5432:5432 -e POSTGRES_DB=mizan -e POSTGRES_USER=mizan -e POSTGRES_PASSWORD=mizan_dev_password postgres:18
docker run -d -p 6379:6379 redis:7-alpine

# Run backend
cd backend
dotnet run --project Mizan.Api

# Backend available at http://localhost:5000
# Swagger at http://localhost:5000/swagger
```

### Production Environment

**Docker Image** (`Dockerfile`):
- Multi-stage build (SDK for build, ASP.NET runtime for final image)
- Non-root user
- Health check endpoint

**Configuration Overrides**:
- Environment variables override `appsettings.json`
- Secrets via Docker secrets or Kubernetes secrets
- Connection strings from environment
- CORS origins from `Cors__Origins` array

**Infrastructure Requirements**:
- PostgreSQL 18+ (managed or self-hosted)
- Redis 7+ (for horizontal scaling)
- HTTPS termination (reverse proxy or load balancer)
- JWKS endpoint accessible from backend

### Build Process

**Build Command**:
```bash
dotnet build
```

**Test Command**:
```bash
# Unit/integration tests
dotnet test

# With PostgreSQL in Docker (recommended)
docker-compose --profile test up test
```

**Migration Workflow**:
```bash
# Create migration
dotnet ef migrations add MigrationName --project Mizan.Infrastructure --startup-project Mizan.Api

# Apply migrations (dev auto-applies)
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api

# Generate SQL script (for production)
dotnet ef migrations script --project Mizan.Infrastructure --startup-project Mizan.Api -o migration.sql
```

---

## Testing Strategy

### Test Pyramid

**Test Distribution**:
- **Integration Tests**: 10-15 (API endpoints with real DB)
- **Unit Tests**: 50+ (Command/Query handlers with in-memory DB)
- **E2E Tests**: 0 (frontend responsibility)

### Unit Tests (xUnit + FluentAssertions + Moq)

**Pattern**: In-memory EF Core + mocked services

**Example** (`CreateRecipeCommandTests.cs`):
```csharp
public class CreateRecipeCommandTests : IDisposable
{
    private readonly MizanDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly CreateRecipeCommandHandler _handler;

    public CreateRecipeCommandTests()
    {
        // In-memory database (isolated per test)
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new MizanDbContext(options);

        // Mock current user
        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        _handler = new CreateRecipeCommandHandler(_context, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateRecipe_WithAllDetails()
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Title = "Ethiopian Doro Wat",
            Ingredients = new List<CreateRecipeIngredientDto> { /* ... */ },
            Instructions = new List<string> { /* ... */ },
            Nutrition = new CreateRecipeNutritionDto { /* ... */ }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Instructions)
            .FirstAsync(r => r.Id == result.Id);

        recipe.Ingredients.Should().HaveCount(3);
        recipe.Instructions.Should().HaveCount(4);
    }

    public void Dispose() => _context.Dispose();
}
```

**Test Patterns**:
- **Arrange-Act-Assert**: Clear test structure
- **Isolation**: Unique database per test (Guid name)
- **FluentAssertions**: Readable assertions (`.Should().Be()`)
- **IDisposable**: Clean up DbContext after test
- **No Mocking DbContext**: Use real EF Core in-memory provider
- **Mock External Services**: ICurrentUserService, IAiService, etc.

### Integration Tests (WebApplicationFactory)

**Pattern**: Real HTTP requests, real database (PostgreSQL via Docker or Testcontainers)

**Example** (`ApiIntegrationTests.cs`):
```csharp
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();

        // Apply migrations and seed data
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.Database.Migrate();
        SeedTestData(db);
    }

    [Fact]
    public async Task SearchFoods_ShouldReturnResults_WhenFoodsExist()
    {
        // Act
        var response = await _client.GetAsync("/api/foods/search?searchTerm=chicken");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchFoodsResponse>();
        result.Should().NotBeNull();
        result!.Foods.Should().NotBeEmpty();
    }
}
```

**Integration Test Patterns**:
- **Real Database**: PostgreSQL (configurable via env var)
- **No Mocking**: Full stack testing (middleware, auth, DB)
- **Test Isolation**: Clean database before each test class
- **Seed Data**: Consistent test fixtures
- **HTTP Client**: System.Net.Http for requests
- **JSON Deserialization**: System.Text.Json

**Test Database** (Docker Compose):
```yaml
services:
  test:
    build: ./backend
    command: dotnet test
    environment:
      ConnectionStrings__PostgreSQL: Host=postgres;Database=mizan_test;Username=mizan;Password=mizan_dev_password
    depends_on:
      - postgres
```

### Test Coverage

**Well-Covered Areas**:
- Command handlers (CRUD operations)
- Query handlers (data retrieval)
- Validation rules (FluentValidation)
- API endpoints (integration tests)

**Limited Coverage**:
- SignalR hubs (requires specialized testing)
- AI services (external API, hard to test)
- Pipeline behaviors (logging, validation orchestration)

**Not Tested**:
- Controllers (thin wrappers, tested via integration)
- Entities (pure data, no logic)
- Infrastructure services (Redis, tested via integration)

---

## Security Considerations

### Authentication Flow

**JWT Lifecycle**:
1. User logs in via BetterAuth (frontend)
2. BetterAuth creates session, generates JWT (ES256)
3. JWT stored in httpOnly cookie
4. Frontend includes JWT in `Authorization: Bearer {token}` header
5. Backend validates JWT using JWKS from BetterAuth endpoint
6. Claims extracted: `sub` (UserId), `email`
7. `ICurrentUserService` provides user context to handlers

**JWT Validation** (`JwtBearerOptionsSetup.cs`):
- **Algorithm**: ES256 (ECDSA P-256) - EdDSA compatible
- **Issuer**: Must match `BetterAuth:Issuer`
- **Audience**: Must match `BetterAuth:Audience` (prevents token replay)
- **Expiration**: 15 minutes (short-lived)
- **Clock Skew**: 5 minutes (time sync tolerance)
- **Key Rotation**: JWKS fetched from endpoint (supports multiple keys)

**JWKS Caching**:
- **TTL**: 1 minute (balance security vs performance)
- **Refresh**: Automatic on cache miss
- **Invalidation**: Manual via `IJwksCache.InvalidateCacheAsync()`
- **Stale Cache**: Used on fetch failure (degraded security, but availability)

### Authorization Approach

**Role-Based** (limited):
- `User.Role` field (values: `user`, `admin`, `trainer`)
- Not enforced in backend (future feature)

**Resource-Based** (current):
- User-scoped queries: `WHERE user_id = @userId`
- Household-scoped: Check `HouseholdMember` permission
- Trainer-client: Check `TrainerClientRelationship` permission
- Authorization logic in handlers (not declarative)

**Example Authorization** (CreateRecipeCommand):
```csharp
if (!_currentUser.UserId.HasValue)
    throw new UnauthorizedAccessException("User must be authenticated");

var recipe = new Recipe
{
    UserId = _currentUser.UserId.Value, // Enforce ownership
    // ...
};
```

**Household Permissions** (HouseholdMember):
- `Role` (owner, admin, member)
- `CanEditRecipes` (bool)
- `CanEditShoppingList` (bool)
- `CanViewNutrition` (bool)

**Trainer Permissions** (TrainerClientRelationship):
- `Status` (pending, active, ended)
- `CanViewNutrition` (bool)
- `CanViewWorkouts` (bool)
- `CanViewMeasurements` (bool)
- `CanMessage` (bool)

### Security Vulnerabilities & Mitigations

**Addressed**:
- **Injection Attacks**: EF Core parameterized queries (SQL injection safe)
- **XSS**: JSON serialization escapes HTML (System.Text.Json default)
- **CSRF**: Not applicable (stateless API, token-based auth)
- **CORS**: Configured origins only (`Cors:Origins`)
- **Authentication Bypass**: JWT validation enforced via `[Authorize]`

**Potential Risks**:
- **Weak Password Policy**: Handled by BetterAuth (frontend)
- **Rate Limiting**: Not implemented (future: ASP.NET Core rate limiting)
- **JWKS Poisoning**: Trusts BetterAuth endpoint (mitigation: HTTPS, network isolation)
- **Stale JWKS Cache**: 1-minute window for compromised keys (trade-off for performance)
- **No MFA**: Handled by BetterAuth
- **Session Hijacking**: HttpOnly cookies (frontend), short JWT lifetime

**Secrets Management**:
- **API Keys**: Environment variables (not in source)
- **Connection Strings**: Environment variables
- **JWT Secret**: Not stored in backend (uses JWKS public keys)

---

## Recommendations

### High Priority

1. **Rate Limiting**:
   - Add ASP.NET Core rate limiting middleware
   - Per-IP and per-user limits
   - Prevent abuse of expensive endpoints (AI, search)

2. **Distributed Tracing**:
   - Add OpenTelemetry for request tracing
   - Correlate logs across services (frontend → backend → database)
   - Performance bottleneck identification

3. **Health Check Enhancements**:
   - Add BetterAuth JWKS endpoint health check
   - Add OpenAI API health check (if configured)
   - Liveness vs readiness separation

4. **Authorization Refactor**:
   - Extract authorization logic from handlers
   - Policy-based authorization (`IAuthorizationService`)
   - Declarative attributes (`[Authorize(Policy = "CanEditRecipe")]`)

5. **API Versioning**:
   - Add versioning (header or URL-based)
   - Prepare for breaking changes
   - Gradual migration strategy

### Medium Priority

1. **Domain Events**:
   - Introduce domain events (e.g., `RecipeCreatedEvent`)
   - Decouple side effects (e.g., achievement unlocking)
   - Event sourcing preparation

2. **Cache Invalidation**:
   - Implement cache-aside pattern for recipes, meal plans
   - Use Redis pub/sub for cache invalidation across instances
   - Optimize read-heavy queries

3. **Query Optimization**:
   - Add database indexes for common queries
   - Use compiled queries for hot paths
   - Consider read replicas for scaling

4. **Logging Enrichment**:
   - Add structured logging (user ID, request ID, correlation ID)
   - Log aggregation (Seq, ELK, Azure Application Insights)
   - Query performance logging

5. **Error Handling**:
   - Global exception handler middleware
   - Consistent error response format (Problem Details RFC 7807)
   - Client-friendly error messages

### Low Priority

1. **Code Quality**:
   - Extract validation logic into domain (e.g., Recipe.Validate())
   - Reduce handler complexity (extract methods)
   - Add XML documentation comments

2. **Testing**:
   - Add SignalR hub tests (specialized framework)
   - Increase integration test coverage
   - Performance benchmarks (BenchmarkDotNet)

3. **Documentation**:
   - Generate OpenAPI spec from Swagger
   - Add endpoint examples to Swagger
   - Developer onboarding guide

---

## Architecture Decision Records (ADRs)

### ADR-001: Clean Architecture with CQRS

**Decision**: Use Clean Architecture layers + CQRS pattern via MediatR

**Rationale**:
- **Separation of Concerns**: Domain, Application, Infrastructure, API
- **Testability**: Application layer has no EF Core coupling (interfaces)
- **CQRS**: Clear read/write separation, future read replicas
- **Flexibility**: Swap infrastructure (e.g., Redis → Memcached)

**Trade-offs**:
- Increased complexity (more files, indirection)
- Learning curve for junior developers
- Boilerplate (Command + Validator + Handler for each operation)

**Status**: Accepted, enforced

---

### ADR-002: Anemic Domain Model

**Decision**: Entities are data containers, business logic in handlers

**Rationale**:
- **Simplicity**: Avoid rich domain model complexity for CRUD-heavy app
- **EF Core Compatibility**: Cleaner with public setters
- **Validation**: FluentValidation separate from entities
- **Testing**: Easy to construct entities in tests

**Trade-offs**:
- **Not DDD**: Violates rich domain model principle
- **Logic Scattered**: Business rules in handlers, not encapsulated
- **Future Refactor**: Hard to move to DDD later

**Alternative Considered**: Rich domain model (rejected for project scope)

**Status**: Accepted

---

### ADR-003: JWKS Caching Strategy

**Decision**: 1-minute Redis cache + in-memory fallback + stale cache on failure

**Rationale**:
- **Performance**: JWT validation is hot path (every authenticated request)
- **Resilience**: Fallback to in-memory if Redis down
- **Security**: 1-minute TTL balances performance vs key rotation
- **Availability**: Stale cache preferred over authentication failures

**Trade-offs**:
- **Security Window**: Compromised keys valid for up to 1 minute after revocation
- **Complexity**: Two-tier cache adds code complexity
- **False Security**: Stale cache could serve invalid keys (rare)

**Status**: Accepted

---

### ADR-004: SignalR + Redis Backplane

**Decision**: Use SignalR for real-time features with Redis backplane

**Rationale**:
- **Real-Time**: Trainer-client chat requires bi-directional communication
- **Horizontal Scaling**: Redis backplane enables multi-instance deployment
- **Message Delivery**: At-least-once delivery via Redis pub/sub
- **Connection Management**: Auto-reconnect via SignalR client

**Alternative Considered**: WebSockets (rejected - no framework support for scaling)

**Status**: Accepted

---

### ADR-005: Semantic Kernel for AI

**Decision**: Use Microsoft Semantic Kernel for AI features (nutrition advice, image analysis)

**Rationale**:
- **Function Calling**: Native support for AI calling application methods
- **Multi-Modal**: Text + image inputs (GPT-4o vision)
- **Testability**: Mock kernel for tests
- **Integration**: Works with OpenAI, Azure OpenAI, local models

**Trade-offs**:
- **Vendor Lock-In**: Microsoft-specific (mitigated by abstraction)
- **Version Churn**: Rapid updates (1.29.0 already outdated)
- **Complexity**: Learning curve for semantic functions

**Alternative Considered**: Direct OpenAI SDK (rejected - no function calling abstraction)

**Status**: Accepted

---

## References

**Official Documentation**:
- [ASP.NET Core 10 Documentation](https://learn.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core 10 Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [MediatR GitHub](https://github.com/jbogard/MediatR)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)

**Architecture Patterns**:
- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Functional Core, Imperative Shell](https://www.destroyallsoftware.com/screencasts/catalog/functional-core-imperative-shell)

**Project-Specific**:
- [CLAUDE.md](G:/_Projects/macro_chef/CLAUDE.md) - Project conventions
- [ARCHITECTURE.md](G:/_Projects/macro_chef/ARCHITECTURE.md) - Full-stack architecture
- [SIGNALR_IMPLEMENTATION.md](G:/_Projects/macro_chef/SIGNALR_IMPLEMENTATION.md) - SignalR guide

---

## Conclusion

The MacroChef backend is a well-architected ASP.NET Core 10 API implementing Clean Architecture with CQRS. The system demonstrates:

- **Separation of Concerns**: Clear boundaries between Domain, Application, Infrastructure, API
- **Testability**: Interface-based design, in-memory tests, integration tests
- **Scalability**: Redis backplane for SignalR, stateless API design
- **Security**: JWT validation with JWKS caching, resource-based authorization
- **Maintainability**: Consistent patterns (Command/Query handlers), declarative validation
- **Modern Stack**: .NET 10, EF Core 10, PostgreSQL 18, Redis 7, Semantic Kernel

**Architectural Strengths**:
- MediatR pipeline behaviors (logging, validation) reduce boilerplate
- DbContext abstraction enables testing without database
- CQRS separation prepares for future read/write scaling
- SignalR + Redis backplane enables horizontal scaling

**Areas for Improvement**:
- Authorization logic embedded in handlers (not declarative)
- Anemic domain model (business logic not encapsulated)
- No domain events (side effects tightly coupled)
- Limited observability (no distributed tracing)

**Critical Issue Identified**:
The current architecture has the backend managing auth-related tables (users, accounts, sessions, jwks, verification) via EF Core migrations, while the frontend (BetterAuth) is the actual source of truth for these tables. This creates a dual-write problem and potential schema drift. A Backend-for-Frontend (BFF) architecture is needed to resolve this.

**Next Steps**:
1. **CRITICAL**: Implement BFF architecture to separate concerns between auth (frontend-owned) and business logic (backend-owned)
2. Implement rate limiting middleware
3. Add distributed tracing (OpenTelemetry)
4. Refactor authorization to policy-based system
5. Optimize common queries with indexes and caching
6. Add domain events for decoupled side effects

This architecture provides a solid foundation for a production meal planning and fitness tracking application, with clear paths for future enhancements and scaling.
