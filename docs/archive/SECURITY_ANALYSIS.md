# Backend API Security & Authorization Analysis

**Project:** MacroChef (Mizan) - ASP.NET Core 10 API
**Date:** 2025-12-26
**Scope:** Backend API endpoints, authentication flow, authorization patterns, and data access controls

---

## Executive Summary

The backend API uses a **Backend-for-Frontend (BFF) authentication pattern** where the Next.js frontend validates JWTs and forwards user context via trusted headers. The authorization implementation is **partially complete** with several critical gaps that could lead to unauthorized data access.

### Key Findings

- ✅ **Strong BFF Authentication**: Constant-time secret comparison, proper claims extraction
- ✅ **Good Authorization in Many Handlers**: Most commands/queries validate user ownership
- ❌ **Missing Authorization in Critical Endpoints**: Several query/command handlers lack ownership checks
- ❌ **Public Data Exposure**: Some endpoints return data without any authentication
- ❌ **No Role Validation in Handlers**: Only admin food endpoints check roles; no other role-based logic
- ⚠️ **Inconsistent Patterns**: Some controllers check auth, some delegate to handlers

---

## 1. Authentication Architecture

### BFF Authentication Flow

```
┌─────────────┐         ┌──────────────┐         ┌──────────────┐
│   Browser   │────────▶│  Next.js BFF │────────▶│ Backend API  │
│             │   JWT   │              │ Headers │              │
└─────────────┘         └──────────────┘         └──────────────┘
                              │
                              ├─ Validates JWT (BetterAuth)
                              ├─ Extracts user claims
                              └─ Forwards via headers:
                                   • X-BFF-Secret (trusted)
                                   • X-User-Id
                                   • X-User-Email
                                   • X-User-Role
```

### BffAuthenticationHandler (`Mizan.Api/Authentication/BffAuthenticationHandler.cs`)

**Security Strengths:**

1. **Constant-time secret comparison** (`CryptographicOperations.FixedTimeEquals`)
   - Prevents timing side-channel attacks
   - Proper mitigation against secret guessing

2. **Claims propagation:**
   - User ID → `ClaimTypes.NameIdentifier` + `"sub"`
   - Email → `ClaimTypes.Email` + `"email"`
   - Role → `ClaimTypes.Role` + `"role"`

3. **Required validation:**
   - `X-BFF-Secret` header (constant-time check)
   - `X-User-Id` header (must be valid GUID)

**Implementation:**

```csharp
// Lines 44-53: Constant-time comparison
var providedSecretBytes = Encoding.UTF8.GetBytes(secretValue.ToString());
var trustedSecretBytes = Encoding.UTF8.GetBytes(Options.TrustedSecret);

if (providedSecretBytes.Length != trustedSecretBytes.Length ||
    !CryptographicOperations.FixedTimeEquals(providedSecretBytes, trustedSecretBytes))
{
    return AuthenticateResult.Fail("Invalid trusted secret");
}
```

**Risks:**

- **Trust boundary**: Backend fully trusts frontend headers if secret matches
- **Single point of failure**: If `Bff:TrustedSecret` leaks, entire system is compromised
- **No additional validation**: Backend doesn't verify JWT signatures itself (delegates to BFF)

---

## 2. API Endpoints Authorization Analysis

### 2.1 FoodsController (`Mizan.Api/Controllers/FoodsController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /api/foods/{id}` | GetFood | None | ❌ **PUBLIC** |
| `GET /api/foods/search` | SearchFoods | None | ❌ **PUBLIC** |
| `POST /api/foods` | CreateFood | `[Authorize(Roles = "admin")]` | ✅ Admin-only |
| `PUT /api/foods/{id}` | UpdateFood | `[Authorize(Roles = "admin")]` | ✅ Admin-only |
| `DELETE /api/foods/{id}` | DeleteFood | `[Authorize(Roles = "admin")]` | ✅ Admin-only |

**Issues:**

- `GetFoodByIdQuery` has **no authentication check** (lines 18-41 in `GetFoodByIdQuery.cs`)
- `SearchFoodsQuery` has **no authentication check** (lines 46-101 in `SearchFoodsQuery.cs`)
- Food data is **intentionally public** (nutritional database), but this should be documented

**Recommendation:** If foods should be public, add comment confirming intent. If not, add `[Authorize]` attribute.

---

### 2.2 RecipesController (`Mizan.Api/Controllers/RecipesController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /api/recipes` | GetRecipes | None | ⚠️ **Handler checks** |
| `GET /api/recipes/{id}` | GetRecipeById | None | ⚠️ **Handler checks** |
| `POST /api/recipes` | CreateRecipe | `[Authorize]` | ✅ Requires auth |

**Handler-Level Authorization:**

**GetRecipesQuery** (lines 59-76):
```csharp
if (_currentUser.UserId.HasValue)
{
    query = query.Where(r =>
        r.UserId == _currentUser.UserId ||
        (request.IncludePublic && r.IsPublic));
}
else
{
    query = query.Where(r => r.IsPublic);
}
```
✅ **Correct**: Authenticated users see their own + public recipes; anonymous see only public

**GetRecipeByIdQuery** (lines 67-68):
```csharp
if (!recipe.IsPublic && recipe.UserId != _currentUser.UserId)
    return null;
```
✅ **Correct**: Private recipes require ownership

**CreateRecipeCommand** (lines 74-77):
```csharp
if (!_currentUser.UserId.HasValue)
{
    throw new UnauthorizedAccessException("User must be authenticated");
}
```
✅ **Correct**: Ownership enforced

**Status:** ✅ Properly secured

---

### 2.3 MealPlansController (`Mizan.Api/Controllers/MealPlansController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /api/mealplans` | GetMealPlans | `[Authorize]` | ✅ Handler filters by user |
| `GET /api/mealplans/{id}` | GetMealPlanById | `[Authorize]` | ✅ Handler filters by user |
| `POST /api/mealplans` | CreateMealPlan | `[Authorize]` | ✅ Handler sets userId |
| `POST /api/mealplans/{id}/recipes` | AddRecipeToMealPlan | `[Authorize]` | ✅ Handler validates ownership |
| `DELETE /api/mealplans/{id}` | DeleteMealPlan | `[Authorize]` | ✅ Handler validates ownership |

**Handler Validation Examples:**

**GetMealPlansQuery** (lines 52-54):
```csharp
var query = _context.MealPlans
    .Include(mp => mp.MealPlanRecipes)
    .Where(mp => mp.UserId == _currentUser.UserId);
```

**GetMealPlanByIdQuery** (line 65):
```csharp
.FirstOrDefaultAsync(mp => mp.Id == request.Id && mp.UserId == _currentUser.UserId, cancellationToken);
```

**DeleteMealPlanCommand** (line 35):
```csharp
.FirstOrDefaultAsync(mp => mp.Id == request.Id && mp.UserId == _currentUser.UserId, cancellationToken);
```

**Status:** ✅ Properly secured

---

### 2.4 ShoppingListsController (`Mizan.Api/Controllers/ShoppingListsController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /api/shoppinglists/{id}` | GetShoppingList | `[Authorize]` | ❌ **NO OWNERSHIP CHECK** |
| `GET /api/shoppinglists` | GetShoppingLists | `[Authorize]` | ✅ Handler filters by user |
| `POST /api/shoppinglists` | CreateShoppingList | `[Authorize]` | ✅ Sets userId in controller |
| `POST /api/shoppinglists/{id}/items` | AddItem | `[Authorize]` | ❌ **NO OWNERSHIP CHECK** |
| `PATCH /api/shoppinglists/items/{itemId}/toggle` | ToggleItem | `[Authorize]` | ❌ **NO OWNERSHIP CHECK** |

**CRITICAL VULNERABILITIES:**

1. **GetShoppingListQuery** (`GetShoppingListQuery.cs`, lines 35-60):
```csharp
public async Task<ShoppingListDto?> Handle(GetShoppingListQuery request, CancellationToken cancellationToken)
{
    var list = await _context.ShoppingLists
        .Include(l => l.Items)
        .FirstOrDefaultAsync(l => l.Id == request.ShoppingListId, cancellationToken);
    // ❌ NO USER ID CHECK - returns ANY shopping list
```
**Impact:** Any authenticated user can read ANY shopping list by guessing GUIDs.

2. **AddShoppingListItemCommand** (`AddShoppingListItemCommand.cs`, lines 19-46):
```csharp
public async Task<Guid?> Handle(AddShoppingListItemCommand request, CancellationToken cancellationToken)
{
    var shoppingList = await _context.ShoppingLists.FindAsync(...);
    // ❌ NO OWNERSHIP CHECK - can add items to anyone's list
```
**Impact:** Any authenticated user can modify ANY shopping list.

3. **ToggleShoppingListItemCommand** (`ToggleShoppingListItemCommand.cs`, lines 17-30):
```csharp
public async Task<bool> Handle(ToggleShoppingListItemCommand request, CancellationToken cancellationToken)
{
    var item = await _context.ShoppingListItems.FindAsync(...);
    // ❌ NO OWNERSHIP CHECK - can toggle items in any list
```
**Impact:** Any authenticated user can check/uncheck items in ANY shopping list.

**TODO Comment in Controller** (line 27):
```csharp
// TODO: Validate user access
```

**Status:** ❌ **CRITICAL - Horizontal privilege escalation vulnerability**

---

### 2.5 HouseholdsController (`Mizan.Api/Controllers/HouseholdsController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /api/households/{id}` | GetHousehold | `[Authorize]` | ❌ **NO MEMBERSHIP CHECK** |
| `POST /api/households` | CreateHousehold | `[Authorize]` | ✅ Sets userId in controller |
| `POST /api/households/{id}/members` | AddMember | `[Authorize]` | ✅ Handler checks admin role |

**VULNERABILITY:**

**GetHouseholdQuery** (`GetHouseholdQuery.cs`, lines 34-59):
```csharp
public async Task<HouseholdDto?> Handle(GetHouseholdQuery request, CancellationToken cancellationToken)
{
    var household = await _context.Households
        .Include(h => h.Members)
        .ThenInclude(m => m.User)
        .FirstOrDefaultAsync(h => h.Id == request.HouseholdId, cancellationToken);
    // ❌ NO MEMBERSHIP CHECK - returns household even if user is not a member
```

**Impact:** Any authenticated user can view ANY household's details including:
- Member list
- Member emails
- Member names
- Household structure

**Correct Authorization** in `AddHouseholdMemberCommand` (lines 22-30):
```csharp
var requester = await _context.HouseholdMembers
    .FirstOrDefaultAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == request.RequestingUserId, cancellationToken);

if (requester == null || requester.Role != "admin")
{
    return false;
}
```

**Status:** ❌ **CRITICAL - Information disclosure vulnerability**

---

### 2.6 ChatController (`Mizan.Api/Controllers/ChatController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /api/chat/{relationshipId}` | GetConversation | `[Authorize]` | ❌ **NO OWNERSHIP CHECK** |
| `POST /api/chat/send` | SendMessage | `[Authorize]` | ✅ Handler validates participation |

**VULNERABILITY:**

**GetChatConversationQuery** (`GetChatConversationQuery.cs`, lines 34-53):
```csharp
public async Task<ChatConversationDto?> Handle(GetChatConversationQuery request, CancellationToken cancellationToken)
{
    var conversation = await _context.ChatConversations
        .Include(c => c.Messages)
        .FirstOrDefaultAsync(c => c.TrainerClientRelationshipId == request.RelationshipId, cancellationToken);
    // ❌ NO CHECK if current user is part of this conversation
```

**Impact:** Any authenticated user can read ANY trainer-client chat conversation.

**Correct Authorization** in `SendChatMessageCommand` (lines 47-52):
```csharp
var relationship = conversation.Relationship;
if (relationship.TrainerId != request.UserId && relationship.ClientId != request.UserId)
{
    throw new UnauthorizedAccessException("User is not part of this conversation");
}
```

**Status:** ❌ **CRITICAL - Chat message disclosure vulnerability**

---

### 2.7 ExercisesController (`Mizan.Api/Controllers/ExercisesController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /api/exercises` | GetExercises | None | ⚠️ **Depends on handler** |
| `POST /api/exercises` | CreateExercise | `[Authorize]` | ⚠️ **Need to review handler** |

**Status:** ⚠️ Needs further investigation for exercise data model

---

### 2.8 HealthController (`Mizan.Api/Controllers/HealthController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /health` | Get | None | ✅ **Intentionally public** |

**Status:** ✅ Health checks should be public for monitoring

---

### 2.9 UsersController (`Mizan.Api/Controllers/UsersController.cs`)

| Endpoint | Method | Authorization | Status |
|----------|--------|---------------|--------|
| `GET /api/users/me` | GetMe | `[Authorize]` | ✅ Returns current user only |
| `GET /api/users/me/debug` | GetMeDebug | `[Authorize]` | ✅ Returns current user only |
| `PUT /api/users/me` | UpdateMe | `[Authorize]` | ✅ Updates current user only |

**Implementation** (lines 28-40):
```csharp
if (!_currentUser.UserId.HasValue)
{
    return Unauthorized("User not authenticated");
}

var result = await _mediator.Send(new GetUserQuery(_currentUser.UserId.Value));
```

**Status:** ✅ Properly secured (no user ID parameter, uses current user)

---

## 3. SignalR Hubs Authorization

### ChatHub (`Mizan.Api/Hubs/ChatHub.cs`)

**Authorization:**
- Hub-level: `[Authorize]` attribute (line 34) ✅
- Connection-based user groups: `user_{userId}` ✅
- Conversation groups: `conversation_{conversationId}` ⚠️

**SendMessage Method** (lines 82-129):
```csharp
public async Task SendMessage(ChatMessageDto message)
{
    var userId = GetUserId();
    // Delegates to SendChatMessageCommand which validates participation ✅
    var command = new SendChatMessageCommand(message.ConversationId, userId.Value, message.Content, message.MessageType);
    var result = await _mediator.Send(command);
```

**VULNERABILITY:**

**JoinConversation Method** (lines 70-75):
```csharp
public async Task JoinConversation(Guid conversationId)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
    // ❌ NO CHECK if user has access to this conversation
```

**Impact:** User can join ANY conversation group and receive real-time messages even if they shouldn't have access.

**Status:** ❌ **Real-time message disclosure vulnerability**

---

## 4. Data Access Patterns Summary

### ✅ Secure Patterns (Used Consistently)

1. **User-scoped queries with filter:**
```csharp
var query = _context.MealPlans
    .Where(mp => mp.UserId == _currentUser.UserId);
```

2. **Ownership validation in commands:**
```csharp
var entity = await _context.Entities
    .FirstOrDefaultAsync(e => e.Id == id && e.UserId == _currentUser.UserId);

if (entity == null)
    return NotFound(); // or throw UnauthorizedAccessException
```

3. **Public/private data filtering:**
```csharp
query = query.Where(r =>
    r.UserId == _currentUser.UserId ||
    (request.IncludePublic && r.IsPublic));
```

### ❌ Insecure Patterns (Found in Code)

1. **No ownership check:**
```csharp
var entity = await _context.Entities
    .FirstOrDefaultAsync(e => e.Id == id); // Missing: && e.UserId == _currentUser.UserId
```

2. **No membership validation:**
```csharp
var household = await _context.Households
    .FirstOrDefaultAsync(h => h.Id == householdId);
// Missing: Check if current user is a member
```

3. **No relationship validation:**
```csharp
var conversation = await _context.ChatConversations
    .FirstOrDefaultAsync(c => c.TrainerClientRelationshipId == relationshipId);
// Missing: Check if current user is trainer or client
```

---

## 5. CurrentUserService (`Mizan.Infrastructure/Services/CurrentUserService.cs`)

**Implementation:**

```csharp
public Guid? UserId
{
    get
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
```

**Status:** ✅ Properly extracts user context from claims

**Usage Pattern:**
- ✅ Most handlers check `if (!_currentUser.UserId.HasValue)`
- ❌ Some handlers assume authentication and skip check
- ⚠️ `UserId` is nullable - handlers must explicitly validate

---

## 6. Role-Based Access Control (RBAC)

### Current Implementation

**Only Admin Role Check:**
```csharp
// FoodsController.cs, lines 39, 47, 62
[Authorize(Roles = "admin")]
```

**No Other Role Checks:**
- No trainer-specific endpoints with role validation
- No client-specific endpoints with role validation
- Household roles (`admin`, `member`) checked in handler, not attribute

### Missing RBAC Patterns

1. **Trainer role enforcement:**
```csharp
[Authorize(Roles = "trainer")]
public async Task<ActionResult> AssignGoal(...) { }
```

2. **Household role-based permissions:**
- `CanEditRecipes`, `CanEditShoppingList`, `CanViewNutrition` flags exist in `HouseholdMember`
- Not enforced anywhere in application layer

---

## 7. Vulnerabilities Summary

### Critical (Immediate Fix Required)

| ID | Component | Vulnerability | Impact | Exploitability |
|----|-----------|---------------|--------|----------------|
| VULN-01 | GetShoppingListQuery | No ownership check | Any user can read any shopping list | High |
| VULN-02 | AddShoppingListItemCommand | No ownership check | Any user can modify any shopping list | High |
| VULN-03 | ToggleShoppingListItemCommand | No ownership check | Any user can toggle items in any list | High |
| VULN-04 | GetHouseholdQuery | No membership check | Any user can view household member info | High |
| VULN-05 | GetChatConversationQuery | No participation check | Any user can read trainer-client chats | Critical |
| VULN-06 | ChatHub.JoinConversation | No conversation access check | Real-time message interception | Critical |

### High (Fix Soon)

| ID | Component | Issue | Impact |
|----|-----------|-------|--------|
| INFO-01 | GetFoodByIdQuery | Public access (no auth) | If intentional, document it |
| INFO-02 | SearchFoodsQuery | Public access (no auth) | If intentional, document it |

### Medium (Review and Consider)

| ID | Component | Issue | Impact |
|----|-----------|-------|--------|
| ARCH-01 | Household permissions | Flags exist but not enforced | Permission model not used |
| ARCH-02 | Role-based authorization | Only admin role used | Incomplete RBAC |

---

## 8. Recommendations

### Immediate Actions (P0)

1. **Fix ShoppingLists authorization:**
```csharp
// GetShoppingListQuery.cs
public async Task<ShoppingListDto?> Handle(GetShoppingListQuery request, CancellationToken cancellationToken)
{
    if (!_currentUser.UserId.HasValue)
        throw new UnauthorizedAccessException("User must be authenticated");

    var list = await _context.ShoppingLists
        .Include(l => l.Items)
        .FirstOrDefaultAsync(l => l.Id == request.ShoppingListId && l.UserId == _currentUser.UserId, cancellationToken);

    // For household lists, add:
    // || (l.HouseholdId.HasValue && _context.HouseholdMembers.Any(m => m.HouseholdId == l.HouseholdId && m.UserId == _currentUser.UserId))
```

2. **Fix Households authorization:**
```csharp
// GetHouseholdQuery.cs
public async Task<HouseholdDto?> Handle(GetHouseholdQuery request, CancellationToken cancellationToken)
{
    if (!_currentUser.UserId.HasValue)
        throw new UnauthorizedAccessException("User must be authenticated");

    var household = await _context.Households
        .Include(h => h.Members)
        .ThenInclude(m => m.User)
        .FirstOrDefaultAsync(h => h.Id == request.HouseholdId, cancellationToken);

    if (household == null)
        return null;

    // Check membership
    if (!household.Members.Any(m => m.UserId == _currentUser.UserId))
        throw new UnauthorizedAccessException("User is not a member of this household");

    return new HouseholdDto(...);
}
```

3. **Fix Chat authorization:**
```csharp
// GetChatConversationQuery.cs
public async Task<ChatConversationDto?> Handle(GetChatConversationQuery request, CancellationToken cancellationToken)
{
    if (!_currentUser.UserId.HasValue)
        throw new UnauthorizedAccessException("User must be authenticated");

    var conversation = await _context.ChatConversations
        .Include(c => c.Messages)
        .Include(c => c.Relationship) // Add this
        .FirstOrDefaultAsync(c => c.TrainerClientRelationshipId == request.RelationshipId, cancellationToken);

    if (conversation == null)
        return null;

    // Validate participation
    var relationship = conversation.Relationship;
    if (relationship.TrainerId != _currentUser.UserId && relationship.ClientId != _currentUser.UserId)
        throw new UnauthorizedAccessException("User is not part of this conversation");

    return new ChatConversationDto(...);
}
```

4. **Fix ChatHub.JoinConversation:**
```csharp
// ChatHub.cs
public async Task JoinConversation(Guid conversationId)
{
    var userId = GetUserId();
    if (!userId.HasValue)
        throw new HubException("User not authenticated");

    // Validate access
    var conversation = await _context.ChatConversations
        .Include(c => c.Relationship)
        .FirstOrDefaultAsync(c => c.Id == conversationId);

    if (conversation == null)
        throw new HubException("Conversation not found");

    var relationship = conversation.Relationship;
    if (relationship.TrainerId != userId && relationship.ClientId != userId)
        throw new HubException("Access denied to this conversation");

    await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
    _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
}
```

### Short-term Actions (P1)

5. **Implement authorization base classes:**
```csharp
// Create IOwnable interface for user-owned entities
public interface IOwnable
{
    Guid UserId { get; }
}

// Create authorization helper
public static class AuthorizationHelpers
{
    public static async Task<T?> GetOwnedEntityAsync<T>(
        this DbSet<T> dbSet,
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
        where T : class, IOwnable
    {
        return await dbSet.FirstOrDefaultAsync(
            e => e.Id == id && e.UserId == currentUserId,
            cancellationToken);
    }
}
```

6. **Add authorization unit tests:**
```csharp
[Fact]
public async Task GetShoppingList_DifferentUser_ReturnsNull()
{
    // Arrange
    var listId = Guid.NewGuid();
    var ownerId = Guid.NewGuid();
    var attackerId = Guid.NewGuid();

    _currentUserService.Setup(x => x.UserId).Returns(attackerId);

    // Act
    var result = await _handler.Handle(new GetShoppingListQuery(listId), CancellationToken.None);

    // Assert
    result.Should().BeNull();
}
```

### Long-term Actions (P2)

7. **Implement policy-based authorization:**
```csharp
// Define policies in Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanEditHouseholdRecipes", policy =>
        policy.RequireAssertion(context =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var householdId = context.Resource as Guid?;
            // Check HouseholdMember.CanEditRecipes
            return true; // Implement logic
        }));
});
```

8. **Add security audit logging:**
```csharp
public class SecurityAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Log authorization attempts
        _logger.LogInformation("User {UserId} executing {CommandType}", _currentUser.UserId, typeof(TRequest).Name);

        try
        {
            return await next();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Authorization failed for user {UserId} on {CommandType}: {Message}",
                _currentUser.UserId, typeof(TRequest).Name, ex.Message);
            throw;
        }
    }
}
```

9. **Document authorization patterns in ARCHITECTURE.md:**
- Required checks for all query handlers
- Required checks for all command handlers
- Household permission enforcement
- SignalR authorization requirements

---

## 9. Testing Strategy

### Authorization Test Categories

1. **Ownership tests:**
   - User can access their own resources ✅
   - User cannot access other users' resources ❌
   - Unauthenticated user gets 401 ❌

2. **Household membership tests:**
   - Member can access household resources ✅
   - Non-member cannot access household ❌
   - Admin can add members ✅
   - Non-admin cannot add members ❌

3. **Trainer-client relationship tests:**
   - Trainer can access client data (with permission) ✅
   - Client can access assigned trainer data ✅
   - Unrelated user cannot access relationship ❌

4. **Public/private tests:**
   - Public recipes visible to all ✅
   - Private recipes only to owner ✅
   - Anonymous users see only public ✅

### Test Implementation Pattern

```csharp
public class ShoppingListAuthorizationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetShoppingList_AsOwner_ReturnsData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var listId = await CreateShoppingListAsync(userId);
        AuthenticateAs(userId);

        // Act
        var response = await Client.GetAsync($"/api/shoppinglists/{listId}");

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
        var response = await Client.GetAsync($"/api/shoppinglists/{listId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // Or 403 Forbidden
    }
}
```

---

## 10. Conclusion

The backend API has a **solid authentication foundation** with BFF pattern and constant-time secret validation, but **critical authorization gaps** exist in several endpoints:

**Immediate security risks:**
- Shopping list CRUD operations allow horizontal privilege escalation
- Household data leaks member information to non-members
- Chat conversations expose sensitive trainer-client communications
- SignalR hub allows joining unauthorized conversations

**Recommended priority:**
1. **Week 1:** Fix all CRITICAL vulnerabilities (VULN-01 through VULN-06)
2. **Week 2:** Add authorization unit tests for all handlers
3. **Week 3:** Implement authorization helpers and base patterns
4. **Week 4:** Add integration tests and security audit logging

**Architecture decisions needed:**
- Should food data be public? (Currently is, may be intentional)
- How should household permissions be enforced?
- Should role-based authorization be expanded beyond admin?

---

## Appendix A: Authorization Checklist for New Handlers

When creating new query/command handlers, verify:

- [ ] Handler checks `_currentUser.UserId.HasValue`
- [ ] Database queries filter by `UserId == _currentUser.UserId`
- [ ] OR: Handler validates ownership after querying
- [ ] For household resources: Check membership in `HouseholdMembers`
- [ ] For relationship resources: Validate trainer/client relationship
- [ ] For public/private data: Implement proper visibility filtering
- [ ] Unit tests cover unauthorized access scenarios
- [ ] Integration tests validate authorization with real database
- [ ] Controller has `[Authorize]` attribute or explicit reasoning for omission

---

## Appendix B: Files Requiring Changes

**Immediate fixes:**

1. `backend/Mizan.Application/Queries/GetShoppingListQuery.cs`
2. `backend/Mizan.Application/Commands/AddShoppingListItemCommand.cs`
3. `backend/Mizan.Application/Commands/ToggleShoppingListItemCommand.cs`
4. `backend/Mizan.Application/Queries/GetHouseholdQuery.cs`
5. `backend/Mizan.Application/Queries/GetChatConversationQuery.cs`
6. `backend/Mizan.Api/Hubs/ChatHub.cs`

**Short-term improvements:**

7. `backend/Mizan.Application/Interfaces/IOwnable.cs` (new)
8. `backend/Mizan.Application/Extensions/AuthorizationHelpers.cs` (new)
9. `backend/Mizan.Tests/Authorization/` (new test directory)

**Documentation:**

10. `ARCHITECTURE.md` (add authorization section)
11. `SECURITY.md` (create security guidelines)
