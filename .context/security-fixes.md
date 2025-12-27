# Security Fixes - Authorization Implementation Plan

**Date**: 2025-12-26
**Status**: Implementation Required
**Priority**: CRITICAL

## Executive Summary

Project-architecture-indexer agents identified 5 critical authorization vulnerabilities in the backend API where resource ownership and membership checks are missing. These vulnerabilities allow any authenticated user to access or modify resources they shouldn't have access to.

## Critical Vulnerabilities

### 1. Shopping Lists - No Ownership Validation

**Severity**: CRITICAL
**Impact**: Any user can access/modify any shopping list by guessing IDs

**Affected Files**:
- `backend/Mizan.Api/Controllers/ShoppingListsController.cs` (contains TODO comment)
- `backend/Mizan.Application/Queries/GetShoppingListQuery.cs`
- `backend/Mizan.Application/Commands/AddShoppingListItemCommand.cs`
- `backend/Mizan.Application/Commands/ToggleShoppingListItemCommand.cs`

**Current Code Pattern** (ShoppingListsController.cs:78-85):
```csharp
[HttpGet("{id}")]
public async Task<ActionResult<ShoppingListDto>> GetShoppingList(Guid id)
{
    // TODO: Validate user access
    var query = new GetShoppingListQuery { ShoppingListId = id };
    var shoppingList = await _mediator.Send(query);
    return Ok(shoppingList);
}
```

**Fix Required**: Implement resource-based authorization to verify user owns the list OR is member of household that owns the list.

### 2. Meal Plans - No Ownership Validation

**Severity**: CRITICAL
**Impact**: Any user can access/modify any meal plan

**Affected Files**:
- `backend/Mizan.Application/Queries/GetMealPlanQuery.cs`
- `backend/Mizan.Application/Commands/AddRecipeToMealPlanCommand.cs`
- `backend/Mizan.Application/Commands/DeleteMealPlanCommand.cs`

**Current Code Pattern** (GetMealPlanQuery.cs):
```csharp
public async Task<MealPlanDto> Handle(GetMealPlanQuery request, CancellationToken cancellationToken)
{
    var mealPlan = await _context.MealPlans
        .Include(mp => mp.MealPlanRecipes)
        .FirstOrDefaultAsync(mp => mp.Id == request.MealPlanId, cancellationToken);
    // No ownership check - returns any meal plan by ID
}
```

**Fix Required**: Add ownership validation in handlers before returning data.

### 3. Households - No Membership Validation

**Severity**: CRITICAL
**Impact**: Any user can view any household's data

**Affected Files**:
- `backend/Mizan.Application/Queries/GetHouseholdQuery.cs`

**Current Code Pattern**:
```csharp
public async Task<HouseholdDto> Handle(GetHouseholdQuery request, CancellationToken cancellationToken)
{
    var household = await _context.Households
        .Include(h => h.Members)
        .FirstOrDefaultAsync(h => h.Id == request.HouseholdId, cancellationToken);
    // No membership check - returns any household
}
```

**Fix Required**: Verify user is member of household before returning data.

### 4. Chat/SignalR - No Conversation Access Validation

**Severity**: CRITICAL
**Impact**: Any user can join any trainer-client conversation and read messages

**Affected Files**:
- `backend/Mizan.Api/Hubs/ChatHub.cs`
- `backend/Mizan.Application/Queries/GetChatConversationQuery.cs`

**Current Code Pattern** (ChatHub.cs:38-46):
```csharp
public async Task JoinConversation(Guid conversationId)
{
    var userId = GetUserId();
    // No validation that user is participant in this conversation
    await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
    _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
}
```

**Fix Required**: Validate user is participant in trainer-client relationship before allowing access.

### 5. Controller-Level Authorization Inconsistency

**Severity**: MEDIUM
**Impact**: Unclear if GET endpoints are intentionally public or missing authorization

**Affected Files**:
- `backend/Mizan.Api/Controllers/FoodsController.cs` - GET endpoints lack `[Authorize]`
- `backend/Mizan.Api/Controllers/RecipesController.cs` - GET endpoints lack `[Authorize]`
- `backend/Mizan.Api/Controllers/ExercisesController.cs` - GET endpoints lack `[Authorize]`

**Current Code Pattern**:
```csharp
// POST/PUT/DELETE have [Authorize], but GET methods don't
[HttpGet]
public async Task<ActionResult<PaginatedList<FoodDto>>> GetFoods([FromQuery] GetFoodsQuery query)
{
    var foods = await _mediator.Send(query);
    return Ok(foods);
}
```

**Decision Required**: Determine if these should be public or protected. Add `[Authorize]` for consistency if protected.

## Implementation Approach

### Pattern: Resource-Based Authorization (ASP.NET Core)

Based on Context7 documentation and ASP.NET Core best practices:

#### Step 1: Define Authorization Requirements

Create requirement classes in `backend/Mizan.Application/Authorization/`:

```csharp
public class OwnershipRequirement : IAuthorizationRequirement { }
public class HouseholdMembershipRequirement : IAuthorizationRequirement { }
public class ConversationParticipantRequirement : IAuthorizationRequirement { }
```

#### Step 2: Implement Authorization Handlers

Example for Shopping Lists (`ShoppingListAuthorizationHandler.cs`):

```csharp
public class ShoppingListAuthorizationHandler
    : AuthorizationHandler<OwnershipRequirement, ShoppingList>
{
    private readonly IMizanDbContext _context;

    public ShoppingListAuthorizationHandler(IMizanDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnershipRequirement requirement,
        ShoppingList resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return;
        }

        // Check direct ownership
        if (resource.UserId.ToString() == userId)
        {
            context.Succeed(requirement);
            return;
        }

        // Check household membership
        var userHouseholds = await _context.HouseholdMembers
            .Where(hm => hm.UserId.ToString() == userId)
            .Select(hm => hm.HouseholdId)
            .ToListAsync();

        if (resource.HouseholdId.HasValue &&
            userHouseholds.Contains(resource.HouseholdId.Value))
        {
            context.Succeed(requirement);
        }
    }
}
```

#### Step 3: Register Handlers

In `backend/Mizan.Application/DependencyInjection.cs`:

```csharp
services.AddScoped<IAuthorizationHandler, ShoppingListAuthorizationHandler>();
services.AddScoped<IAuthorizationHandler, MealPlanAuthorizationHandler>();
services.AddScoped<IAuthorizationHandler, HouseholdAuthorizationHandler>();
services.AddScoped<IAuthorizationHandler, ConversationAuthorizationHandler>();
```

#### Step 4: Use in Handlers

Inject `IAuthorizationService` and check authorization before returning data:

```csharp
public class GetShoppingListQueryHandler : IRequestHandler<GetShoppingListQuery, ShoppingListDto>
{
    private readonly IMizanDbContext _context;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task<ShoppingListDto> Handle(GetShoppingListQuery request, CancellationToken cancellationToken)
    {
        var shoppingList = await _context.ShoppingLists
            .FirstOrDefaultAsync(sl => sl.Id == request.ShoppingListId, cancellationToken);

        if (shoppingList == null)
        {
            throw new NotFoundException(nameof(ShoppingList), request.ShoppingListId);
        }

        // Authorization check
        var user = _httpContextAccessor.HttpContext.User;
        var authResult = await _authorizationService.AuthorizeAsync(
            user,
            shoppingList,
            new OwnershipRequirement()
        );

        if (!authResult.Succeeded)
        {
            throw new ForbiddenAccessException();
        }

        return _mapper.Map<ShoppingListDto>(shoppingList);
    }
}
```

### Pattern: SignalR Hub Authorization

For ChatHub, validate in `OnConnectedAsync` and before joining groups:

```csharp
public async Task JoinConversation(Guid conversationId)
{
    var userId = GetUserId();

    // Validate user is participant in this conversation
    var isParticipant = await _context.TrainerClientRelationships
        .AnyAsync(r => r.ConversationId == conversationId &&
                      (r.TrainerId.ToString() == userId || r.ClientId.ToString() == userId));

    if (!isParticipant)
    {
        throw new HubException("You do not have access to this conversation");
    }

    await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
}
```

## Implementation Roadmap

### Phase 1: Infrastructure (2-3 hours)
1. Create authorization requirements classes
2. Create authorization handler base classes
3. Register handlers in DI container
4. Add `IAuthorizationService` and `IHttpContextAccessor` to handlers that need it

### Phase 2: Shopping Lists (1 hour)
1. Implement `ShoppingListAuthorizationHandler`
2. Update `GetShoppingListQuery` handler
3. Update `AddShoppingListItemCommand` handler
4. Update `ToggleShoppingListItemCommand` handler
5. Remove TODO comment from controller

### Phase 3: Meal Plans (1 hour)
1. Implement `MealPlanAuthorizationHandler`
2. Update `GetMealPlanQuery` handler
3. Update `AddRecipeToMealPlanCommand` handler
4. Update `DeleteMealPlanCommand` handler

### Phase 4: Households (30 minutes)
1. Implement `HouseholdAuthorizationHandler`
2. Update `GetHouseholdQuery` handler

### Phase 5: Chat/SignalR (1 hour)
1. Implement `ConversationAuthorizationHandler`
2. Update `ChatHub.JoinConversation` method
3. Update `GetChatConversationQuery` handler
4. Add authorization to `SendMessage` method

### Phase 6: Controller Attributes (15 minutes)
1. Review Foods/Recipes/Exercises controllers
2. Add `[Authorize]` attributes if resources should be protected
3. Document if intentionally public

### Phase 7: Testing (2 hours)
1. Unit tests for authorization handlers
2. Integration tests for protected endpoints
3. Test unauthorized access returns 403
4. Test household membership scenarios

**Total Estimated Effort**: 8-9 hours

## Testing Strategy

### Unit Tests (per handler)
```csharp
[Fact]
public async Task HandleRequirementAsync_OwnerAccess_Succeeds()
{
    // Arrange: User owns the resource
    // Act: Call HandleRequirementAsync
    // Assert: context.Succeed was called
}

[Fact]
public async Task HandleRequirementAsync_NonOwnerAccess_Fails()
{
    // Arrange: User doesn't own the resource
    // Act: Call HandleRequirementAsync
    // Assert: context.Succeed was NOT called
}

[Fact]
public async Task HandleRequirementAsync_HouseholdMemberAccess_Succeeds()
{
    // Arrange: User is household member, resource owned by household
    // Act: Call HandleRequirementAsync
    // Assert: context.Succeed was called
}
```

### Integration Tests (per endpoint)
```csharp
[Fact]
public async Task GetShoppingList_Unauthorized_Returns403()
{
    // Arrange: Create shopping list for User A, authenticate as User B
    // Act: GET /api/ShoppingLists/{id}
    // Assert: 403 Forbidden
}
```

## Risk Assessment

| Vulnerability | Exploitability | Impact | Risk Level |
|---------------|----------------|--------|------------|
| Shopping Lists | High | High | CRITICAL |
| Meal Plans | High | High | CRITICAL |
| Households | High | Medium | CRITICAL |
| Chat/SignalR | Medium | High | CRITICAL |
| Controller Attrs | Low | Low | MEDIUM |

**Immediate Action Required**: All CRITICAL items should be fixed before next deployment.

## References

- ASP.NET Core Resource-Based Authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased
- SignalR Security: https://learn.microsoft.com/en-us/aspnet/core/signalr/security
- Context7 Documentation: Retrieved 2025-12-26

## Approval

- [ ] Technical Lead Review
- [ ] Security Review
- [ ] Implementation Approval
- [ ] Test Plan Approval

## Implementation Log

- 2025-12-26: Plan created based on agent findings and Context7 research
- _Next: Begin Phase 1 implementation_
