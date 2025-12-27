# MacroChef BFF Refactoring Plan

**Version**: 1.0
**Date**: 2025-12-21
**Status**: Draft

## Executive Summary

**Current State:**
- Backend validates JWT tokens via JWKS endpoint
- Backend has FULL write access to auth tables (violates BetterAuth ownership)
- JWT validation adds latency (JWKS cache, Redis dependency)
- Not a true BFF pattern

**Target State:**
- Next.js acts as Backend-for-Frontend (BFF) layer
- Backend trusts Next.js via shared secret
- Backend only READS from user tables (BetterAuth owns writes)
- Data Access Layer (DAL) for session validation
- Simpler, faster, more secure

## Current Architecture Analysis

### What's Working ✅
1. BetterAuth handles authentication on frontend
2. Shared PostgreSQL database
3. Proxying already configured via `next.config.ts` rewrites
4. Docker network isolation (backend not exposed to internet)

### Critical Issues ❌

1. **Schema Ownership Violation**: Backend EF Core has WRITE access to `users`, `sessions`, `accounts`, `jwks`, `verification` tables
   - `MizanDbContext.cs:14-18` defines DbSets with full write access
   - Migrations can modify auth tables (dangerous!)
   - BetterAuth expects to be sole owner

2. **JWT Validation Overhead**:
   - Backend fetches JWKS from Next.js (`JwtBearerOptionsSetup.cs:54`)
   - Redis caching layer (`JwksCache`)
   - Extra network hop for every request
   - Unnecessary complexity for internal service

3. **Not a True BFF**:
   - Client can potentially call backend directly (if exposed)
   - No trusted header pattern
   - Backend doesn't trust Next.js implicitly

4. **Security Vulnerability**:
   - Middleware-based auth is deprecated (CVE-2025-29927)
   - Need Data Access Layer pattern instead

---

## Phase 1: Frontend - Data Access Layer (DAL)

**Goal**: Create centralized session verification layer

### 1.1 Create DAL for Session Verification

**File**: `frontend/lib/dal.ts` (new)

```typescript
import 'server-only'; // Ensures this only runs on server
import { cache } from 'react';
import { cookies } from 'next/headers';
import { auth } from '@/lib/auth';

export type SessionUser = {
  id: string;
  email: string;
  name: string | null;
  role: string;
  emailVerified: boolean;
};

/**
 * Data Access Layer: Verify session and return user
 * Uses React cache() for request deduplication
 */
export const verifySession = cache(async (): Promise<SessionUser | null> => {
  const session = await auth.api.getSession({
    headers: await cookies()
  });

  if (!session?.user) {
    return null;
  }

  return {
    id: session.user.id,
    email: session.user.email,
    name: session.user.name,
    role: session.user.role || 'user',
    emailVerified: session.user.emailVerified
  };
});

/**
 * Verify session and throw if not authenticated
 * Use this for protected routes/actions
 */
export async function requireAuth(): Promise<SessionUser> {
  const user = await verifySession();

  if (!user) {
    throw new Error('Unauthorized');
  }

  return user;
}

/**
 * Verify session and check for specific role
 */
export async function requireRole(role: string): Promise<SessionUser> {
  const user = await requireAuth();

  if (user.role !== role) {
    throw new Error('Forbidden: Insufficient permissions');
  }

  return user;
}
```

### 1.2 Create Internal API Proxy

**File**: `frontend/lib/api-proxy.ts` (new)

```typescript
import 'server-only';
import { verifySession } from '@/lib/dal';

const BACKEND_URL = process.env.API_URL || 'http://mizan-backend:8080';
const INTERNAL_SECRET = process.env.INTERNAL_API_SECRET;

if (!INTERNAL_SECRET) {
  throw new Error('INTERNAL_API_SECRET is required');
}

/**
 * Internal API proxy: calls backend with trusted headers
 * ONLY use this from Server Components or Server Actions
 */
export async function callBackend<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  // 1. Verify session
  const user = await verifySession();

  if (!user) {
    throw new Error('Unauthorized: No active session');
  }

  // 2. Prepare headers with trusted user ID
  const headers = new Headers(options.headers);
  headers.set('Content-Type', 'application/json');
  headers.set('X-User-Id', user.id);
  headers.set('X-Internal-Secret', INTERNAL_SECRET);

  // 3. Call backend
  const response = await fetch(`${BACKEND_URL}${path}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || `API error: ${response.status}`);
  }

  return convertKeysToCamelCase<T>(await response.json());
}

// Keep the camelCase converter
function convertKeysToCamelCase<T>(obj: any): T {
  if (obj === null || obj === undefined) return obj;
  if (Array.isArray(obj)) return obj.map(item => convertKeysToCamelCase(item)) as T;
  if (typeof obj === 'object' && obj.constructor === Object) {
    const converted: any = {};
    for (const key in obj) {
      if (Object.prototype.hasOwnProperty.call(obj, key)) {
        const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
        converted[camelKey] = convertKeysToCamelCase(obj[key]);
      }
    }
    return converted as T;
  }
  return obj;
}
```

### 1.3 Update Environment Variables

**File**: `frontend/.env.local` (add)

```bash
# Internal API secret (must match backend)
INTERNAL_API_SECRET="your-strong-secret-here-min-32-chars"
```

### 1.4 Update Client-Side API Client

**File**: `frontend/lib/api-client.ts` (new - for client components only)

```typescript
'use client';

/**
 * Client-side API client
 * Routes through Next.js API routes (which use the DAL)
 */
export async function apiClient<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const response = await fetch(`/api/proxy${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
    credentials: 'include', // Send cookies
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || `API error: ${response.status}`);
  }

  return response.json();
}
```

### 1.5 Create API Route Handler (Generic Proxy)

**File**: `frontend/app/api/proxy/[...path]/route.ts` (new)

```typescript
import { NextRequest } from 'next/server';
import { callBackend } from '@/lib/api-proxy';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  const endpoint = `/${path.join('/')}`;
  const searchParams = request.nextUrl.searchParams.toString();
  const fullPath = searchParams ? `${endpoint}?${searchParams}` : endpoint;

  try {
    const data = await callBackend(fullPath, { method: 'GET' });
    return Response.json(data);
  } catch (error) {
    return Response.json(
      { error: error instanceof Error ? error.message : 'Unknown error' },
      { status: 500 }
    );
  }
}

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  const endpoint = `/${path.join('/')}`;
  const body = await request.text();

  try {
    const data = await callBackend(endpoint, {
      method: 'POST',
      body,
    });
    return Response.json(data);
  } catch (error) {
    return Response.json(
      { error: error instanceof Error ? error.message : 'Unknown error' },
      { status: 500 }
    );
  }
}

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  const endpoint = `/${path.join('/')}`;
  const body = await request.text();

  try {
    const data = await callBackend(endpoint, {
      method: 'PUT',
      body,
    });
    return Response.json(data);
  } catch (error) {
    return Response.json(
      { error: error instanceof Error ? error.message : 'Unknown error' },
      { status: 500 }
    );
  }
}

export async function PATCH(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  const endpoint = `/${path.join('/')}`;
  const body = await request.text();

  try {
    const data = await callBackend(endpoint, {
      method: 'PATCH',
      body,
    });
    return Response.json(data);
  } catch (error) {
    return Response.json(
      { error: error instanceof Error ? error.message : 'Unknown error' },
      { status: 500 }
    );
  }
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  const endpoint = `/${path.join('/')}`;

  try {
    const data = await callBackend(endpoint, { method: 'DELETE' });
    return Response.json(data);
  } catch (error) {
    return Response.json(
      { error: error instanceof Error ? error.message : 'Unknown error' },
      { status: 500 }
    );
  }
}
```

---

## Phase 2: Backend - Trust Next.js

**Goal**: Remove JWT validation, trust internal headers

### 2.1 Create Internal API Middleware

**File**: `backend/Mizan.Api/Middleware/InternalApiMiddleware.cs` (new)

```csharp
using System.Security.Claims;

namespace Mizan.Api.Middleware;

/// <summary>
/// Validates requests from Next.js BFF and sets user context
/// </summary>
public class InternalApiMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InternalApiMiddleware> _logger;
    private readonly string _internalSecret;

    public InternalApiMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<InternalApiMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _internalSecret = configuration["InternalApiSecret"]
            ?? throw new InvalidOperationException("InternalApiSecret is required");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Allow health check and Swagger without auth
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // SignalR Hub negotiation - needs different handling
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            // For SignalR, the session verification happens in Hub OnConnectedAsync
            // Just verify internal secret here
            var providedSecret = context.Request.Headers["X-Internal-Secret"].FirstOrDefault();
            if (providedSecret != _internalSecret)
            {
                _logger.LogWarning("Forbidden: Invalid internal secret for SignalR from {IP}",
                    context.Connection.RemoteIpAddress);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Forbidden" });
                return;
            }

            await _next(context);
            return;
        }

        // 1. Verify internal secret
        var providedSecret = context.Request.Headers["X-Internal-Secret"].FirstOrDefault();

        if (providedSecret != _internalSecret)
        {
            _logger.LogWarning("Forbidden: Invalid or missing internal secret from {IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Forbidden: Internal access only" });
            return;
        }

        // 2. Extract user ID and set claims
        var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthorized: Missing X-User-Id header");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        // 3. Set user principal
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("user_id", userId) // Custom claim for easier access
        };

        var identity = new ClaimsIdentity(claims, "InternalProxy");
        context.User = new ClaimsPrincipal(identity);

        _logger.LogDebug("Authenticated request for user {UserId}", userId);

        await _next(context);
    }
}
```

### 2.2 Update Program.cs

**File**: `backend/Mizan.Api/Program.cs`

**REMOVE these lines:**
```csharp
// Lines 58-71 (JWT-related services)
builder.Services.AddHttpClient("JwksClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<IJwksCache, JwksCache>();
builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
```

**REMOVE from pipeline:**
```csharp
app.UseAuthentication(); // Line 132
```

**ADD Internal API middleware BEFORE UseAuthorization:**
```csharp
// After app.UseCors("AllowFrontend");
app.UseMiddleware<InternalApiMiddleware>();

// Keep this for [Authorize] attribute support (optional)
app.UseAuthorization();
```

Full updated section:

```csharp
var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");

// ADD: Internal API middleware
app.UseMiddleware<InternalApiMiddleware>();

// REMOVE: app.UseAuthentication();
app.UseAuthorization(); // Keep for [Authorize] attribute (optional)

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health");
```

### 2.3 Update Environment Variables

**File**: `backend/Mizan.Api/appsettings.Development.json`

```json
{
  "InternalApiSecret": "your-strong-secret-here-min-32-chars",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**File**: `docker-compose.yml`

```yaml
services:
  mizan-backend:
    environment:
      - InternalApiSecret=${INTERNAL_API_SECRET}
    # REMOVE:
    # - BetterAuth__JwksUrl=${BETTERAUTH_JWKS_URL}
    # - BetterAuth__Issuer=${BETTER_AUTH_URL}
    # - BetterAuth__Audience=mizan-api
```

**File**: `.env` (create in project root)

```bash
# Internal API secret (shared between frontend and backend)
INTERNAL_API_SECRET=your-strong-secret-here-min-32-chars-CHANGE-THIS-IN-PRODUCTION
```

### 2.4 Make User Entity READ-ONLY

**File**: `backend/Mizan.Infrastructure/Data/MizanDbContext.cs`

Update auth tables to be read-only:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // User configuration - READ ONLY (BetterAuth owns writes)
    modelBuilder.Entity<User>(entity =>
    {
        entity.ToTable("users");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        entity.Property(e => e.EmailVerified).HasColumnName("email_verified").HasDefaultValue(false);
        entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
        entity.Property(e => e.Image).HasColumnName("image");
        entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(50).HasDefaultValue("user");
        entity.Property(e => e.Banned).HasColumnName("banned").HasDefaultValue(false);
        entity.Property(e => e.BanReason).HasColumnName("ban_reason");
        entity.Property(e => e.BanExpires).HasColumnName("ban_expires");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        entity.HasIndex(e => e.Email).IsUnique();

        // MARK AS READ-ONLY (no inserts, updates, deletes in migrations)
        entity.ToTable(tb => tb.ExcludeFromMigrations());
    });

    // Account configuration - READ ONLY
    modelBuilder.Entity<Account>(entity =>
    {
        // ... existing configuration ...
        entity.ToTable(tb => tb.ExcludeFromMigrations());
    });

    // Session configuration - READ ONLY
    modelBuilder.Entity<Session>(entity =>
    {
        // ... existing configuration ...
        entity.ToTable(tb => tb.ExcludeFromMigrations());
    });

    // Jwk configuration - READ ONLY
    modelBuilder.Entity<Jwk>(entity =>
    {
        // ... existing configuration ...
        entity.ToTable(tb => tb.ExcludeFromMigrations());
    });

    // Verification configuration - READ ONLY
    modelBuilder.Entity<Verification>(entity =>
    {
        // ... existing configuration ...
        entity.ToTable(tb => tb.ExcludeFromMigrations());
    });

    // Rest of configurations remain unchanged...
}
```

**Important**: Add `.ToTable(tb => tb.ExcludeFromMigrations())` to:
- User
- Account
- Session
- Jwk
- Verification

### 2.5 Update Controllers to Use ClaimsPrincipal

**File**: `backend/Mizan.Api/Controllers/BaseController.cs` (create)

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Mizan.Api.Controllers;

public class BaseController : ControllerBase
{
    protected Guid GetUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in claims");
        }

        return userId;
    }
}
```

**Update all controllers** to inherit from `BaseController`:

```csharp
// Before:
public class FoodsController : ControllerBase

// After:
public class FoodsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetMyFoods()
    {
        var userId = GetUserId(); // From BaseController
        // ... rest of logic
    }
}
```

### 2.6 Remove JWKS-Related Code

**Delete these files:**
- `backend/Mizan.Api/Services/JwtBearerOptionsSetup.cs`
- `backend/Mizan.Api/Services/IJwksCache.cs` (if exists)
- `backend/Mizan.Api/Services/JwksCache.cs` (if exists)

**File**: `backend/Mizan.Api/Mizan.Api.csproj`

Remove:
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
```

---

## Phase 3: Migration Strategy

### 3.1 Feature Flag Approach

Create a feature flag to switch between old JWT auth and new internal auth.

**File**: `backend/Mizan.Api/appsettings.Development.json`

```json
{
  "FeatureFlags": {
    "UseInternalAuth": false  // Set to true when ready to switch
  },
  "InternalApiSecret": "your-strong-secret-here-min-32-chars",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**File**: `backend/Mizan.Api/Program.cs`

Conditional middleware registration:

```csharp
var useInternalAuth = builder.Configuration.GetValue<bool>("FeatureFlags:UseInternalAuth");

// ... rest of app setup

if (useInternalAuth)
{
    app.UseMiddleware<InternalApiMiddleware>();
}
else
{
    app.UseAuthentication(); // Old JWT auth
}

app.UseAuthorization();
```

### 3.2 Testing Plan

**Before cutover:**
1. ✅ Test DAL session verification with existing auth
2. ✅ Test internal API proxy with feature flag OFF (should fail gracefully)
3. ✅ Add integration tests for internal middleware
4. ✅ Test all critical user flows (login, create recipe, workout, etc.)

**During cutover:**
1. Set `UseInternalAuth=true` in `appsettings.Development.json`
2. Restart backend container: `docker-compose restart mizan-backend`
3. Test critical user flows again
4. Monitor logs for auth failures
5. Check for any 401/403 errors in browser console

**After cutover:**
1. Monitor for 24-48 hours
2. If stable, update production config
3. Remove feature flag code
4. Remove JWT-related dependencies

### 3.3 Rollback Plan

If issues arise:
1. Set `UseInternalAuth=false` in config
2. Restart backend: `docker-compose restart mizan-backend`
3. Old JWT auth resumes immediately
4. No data loss, no migration needed

---

## Phase 4: SignalR Updates

**Goal**: Update SignalR to work with new auth pattern

### 4.1 Update Hub Authorization

**File**: `backend/Mizan.Api/Hubs/ChatHub.cs`

```csharp
using Microsoft.AspNetCore.SignalR;

namespace Mizan.Api.Hubs;

public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("user_id")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("SignalR connection rejected: No user_id claim");
            Context.Abort();
            return;
        }

        _logger.LogInformation("User {UserId} connected to SignalR", userId);

        // Add to group by user ID for direct messaging
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst("user_id")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            _logger.LogInformation("User {UserId} disconnected from SignalR", userId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ... rest of hub methods
}
```

### 4.2 Update Frontend SignalR Client

**File**: `frontend/lib/services/signalr-chat.ts`

Remove JWT token from query string (session cookie will be sent automatically):

```typescript
// Before:
const connection = new HubConnectionBuilder()
  .withUrl(`${apiUrl}/hubs/chat`, {
    accessTokenFactory: async () => {
      const token = await getApiToken();
      return token || '';
    }
  })
  .build();

// After (session cookie sent automatically):
const connection = new HubConnectionBuilder()
  .withUrl(`${apiUrl}/hubs/chat`, {
    // No accessTokenFactory needed - session cookie is sent automatically
    withCredentials: true  // Ensure cookies are sent
  })
  .build();
```

---

## Phase 5: Documentation Updates

### 5.1 Update auth-architecture.md

**File**: `.context/auth-architecture.md`

Replace authentication flow section:

```markdown
## Authentication Flow (BFF Pattern)

### User Login
```
User → Frontend (BetterAuth) → Validate credentials
                              ↓
                     Create session in PostgreSQL
                              ↓
                     Set httpOnly session cookie
```

### API Request (Server Component)
```
Server Component → verifySession() (DAL)
                              ↓
                     Check session cookie
                              ↓
                     Validate with BetterAuth
                              ↓
                     callBackend() with X-User-Id header
                              ↓
Backend validates X-Internal-Secret → Trusts X-User-Id → Sets ClaimsPrincipal
```

### API Request (Client Component)
```
Client Component → apiClient() → /api/proxy/*
                                       ↓
                              Next.js API Route → verifySession()
                                       ↓
                              callBackend() with trusted headers
                                       ↓
                              Backend trusts Next.js
```

### Key Security Features
- Backend NOT exposed to internet (Docker network only)
- Backend validates INTERNAL_API_SECRET (shared secret)
- Next.js validates session, backend trusts Next.js
- No JWT validation overhead
- BetterAuth owns auth tables (backend read-only)
```

### 5.2 Update CLAUDE.md

Add BFF architecture section:

```markdown
## BFF Architecture (Backend for Frontend)

MacroChef uses a BFF pattern where Next.js acts as the trusted middleware between the client and .NET backend.

### Authentication Flow
1. User authenticates via BetterAuth (Next.js)
2. Session stored in PostgreSQL
3. Session cookie set (httpOnly, sameSite: lax)
4. Next.js validates session via DAL (`lib/dal.ts`)
5. Next.js forwards requests to backend with `X-User-Id` and `X-Internal-Secret` headers
6. Backend trusts Next.js (validates secret) and sets user claims

### API Calls

**From Server Components:**
```typescript
import { callBackend } from '@/lib/api-proxy';

const data = await callBackend<FoodDto[]>('/api/Foods');
```

**From Client Components:**
```typescript
import { apiClient } from '@/lib/api-client';

const data = await apiClient<FoodDto[]>('/api/Foods');
```

### Security Principles
- Backend is NOT exposed to internet (Docker network only)
- Shared secret (`INTERNAL_API_SECRET`) validates Next.js → Backend communication
- Data Access Layer (DAL) centralizes session validation
- BetterAuth owns auth tables (backend READ-ONLY access)
```

---

## Security Implications & Trade-offs

### ✅ Security Improvements

1. **CVE-2025-29927 Mitigation**: No more middleware-based auth
2. **Principle of Least Privilege**: Backend can only READ user data
3. **Defense in Depth**: Multiple layers (DAL, internal secret, ClaimsPrincipal)
4. **Attack Surface Reduction**: Backend not exposed to internet
5. **Session-Based Auth**: More secure than JWT for server-to-server communication

### ⚠️ Trade-offs

1. **Shared Secret Dependency**:
   - **Risk**: If `INTERNAL_API_SECRET` leaks, attacker can impersonate any user
   - **Mitigation**: Use strong secret (min 32 chars), rotate regularly, env var only, never commit to git

2. **Single Point of Failure**:
   - **Risk**: Next.js goes down = entire app down
   - **Mitigation**: Already the case (Next.js serves frontend), add health checks

3. **Stateful Sessions**:
   - **Risk**: Session table growth, DB dependency
   - **Mitigation**: BetterAuth handles cleanup, PostgreSQL can handle scale

4. **Trust Boundary**:
   - **Risk**: Backend implicitly trusts Next.js (no double-check)
   - **Mitigation**: Docker network isolation, internal secret validation

### 🔒 Best Practices

1. **Rotate Internal Secret** every 90 days
2. **Monitor Failed Auth Attempts** (log `X-Internal-Secret` mismatches)
3. **Rate Limit** Next.js → Backend requests (if abuse detected)
4. **Use HTTPS** in production (even within Docker network for defense in depth)
5. **Audit User Access** via PostgreSQL logs
6. **Strong Secret Generation**: Use `openssl rand -base64 32` or equivalent

---

## Migration Checklist

### Phase 1: Frontend DAL
- [ ] Create `frontend/lib/dal.ts`
- [ ] Create `frontend/lib/api-proxy.ts`
- [ ] Create `frontend/lib/api-client.ts`
- [ ] Create `frontend/app/api/proxy/[...path]/route.ts`
- [ ] Add `INTERNAL_API_SECRET` to `frontend/.env.local`
- [ ] Test session verification (should work with existing backend)

### Phase 2: Backend Trust
- [ ] Create `backend/Mizan.Api/Middleware/InternalApiMiddleware.cs`
- [ ] Add feature flag to `Program.cs`
- [ ] Add `InternalApiSecret` to `appsettings.Development.json`
- [ ] Update `docker-compose.yml` env vars
- [ ] Create `.env` file in project root
- [ ] Mark auth tables as read-only in `MizanDbContext.cs`
- [ ] Create `BaseController.cs` with `GetUserId()`
- [ ] Update all controllers to inherit from `BaseController`

### Phase 3: Testing & Migration
- [ ] Test with feature flag OFF (old JWT auth)
- [ ] Test with feature flag ON (new internal auth)
- [ ] Run integration tests
- [ ] Flip flag in development
- [ ] Monitor for 24-48 hours
- [ ] Document any issues

### Phase 4: SignalR
- [ ] Update `InternalApiMiddleware` for `/hubs` path
- [ ] Update `ChatHub.OnConnectedAsync()`
- [ ] Update frontend SignalR client (remove access_token)
- [ ] Test chat functionality

### Phase 5: Documentation
- [ ] Update `.context/auth-architecture.md`
- [ ] Update `CLAUDE.md`
- [ ] Update `README.md` (if needed)
- [ ] Document rollback procedure

### Phase 6: Cleanup
- [ ] Remove feature flag from code
- [ ] Delete `JwtBearerOptionsSetup.cs`
- [ ] Delete `IJwksCache.cs` and `JwksCache.cs`
- [ ] Remove JWT package from `Mizan.Api.csproj`
- [ ] Remove BetterAuth env vars from backend config
- [ ] Remove Redis if ONLY used for JWKS (keep if SignalR uses it)
- [ ] Archive old auth code in git history

---

## Estimated Effort

- **Phase 1 (Frontend DAL)**: 4-6 hours
- **Phase 2 (Backend Trust)**: 3-4 hours
- **Phase 3 (Migration)**: 2-3 hours (+ monitoring)
- **Phase 4 (SignalR)**: 1-2 hours
- **Phase 5 (Documentation)**: 1-2 hours
- **Phase 6 (Cleanup)**: 1 hour

**Total**: 12-18 hours

---

## References & Research

This plan is based on:

- [Next.js 16 Release Notes](https://nextjs.org/blog/next-16)
- [How to Use Proxy in Next.js 16 (BFF Guide)](https://u11d.com/blog/nextjs-16-proxy-vs-middleware-bff-guide/)
- [Next.js Middleware Is Changing - DEV Community](https://dev.to/aakash_shrivas_0806333bbe/nextjs-middleware-is-changing-what-you-need-to-know-about-the-move-toward-proxies-3ndk)
- [Next.js Authentication Best Practices in 2025](https://www.franciscomoretti.com/blog/modern-nextjs-authentication-best-practices)
- [CVE-2025-29927: Next.js Middleware Authorization Bypass](https://projectdiscovery.io/blog/nextjs-middleware-authorization-bypass)
- BetterAuth Documentation
- Clean Architecture principles (Uncle Bob)

---

## Questions & Considerations

### Q: Should we remove Redis entirely?
A: **No**. Redis is still used for:
- SignalR backplane (horizontal scaling)
- Application caching (ingredient search, etc.)

Only the JWKS cache is removed.

### Q: What about horizontal scaling?
A: **Works fine**. Multiple Next.js instances can share:
- PostgreSQL for sessions (BetterAuth handles session affinity)
- Redis for SignalR backplane
- Backend instances can scale independently (stateless)

### Q: Performance impact?
A: **Faster**. Removes:
- JWKS fetch from Next.js
- Redis cache lookup
- JWT signature verification

New overhead:
- Session validation in Next.js (already happens for SSR)
- Header validation in backend (minimal)

Net result: Lower latency.

### Q: What if INTERNAL_API_SECRET leaks?
A: **Immediate action**:
1. Generate new secret: `openssl rand -base64 32`
2. Update `.env` and `appsettings.json`
3. Restart both services
4. Invalidate all sessions (optional, if compromised)

### Q: Can we still use [Authorize] attribute?
A: **Yes**. ClaimsPrincipal is set by middleware, so `[Authorize]` still works. However, it's redundant since middleware already validates.

---

## Success Criteria

✅ All API requests authenticated via DAL
✅ Backend validates internal secret
✅ Zero JWT validation overhead
✅ Auth tables marked read-only in EF Core
✅ SignalR works with new auth pattern
✅ No 401/403 errors in production
✅ Documentation updated
✅ Old JWT code removed

---

**Status**: Ready for implementation
**Next Step**: Begin Phase 1 (Frontend DAL)
