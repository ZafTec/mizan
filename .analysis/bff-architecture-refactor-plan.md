# Backend-for-Frontend (BFF) Architecture Refactor Plan

## Executive Summary

**Objective**: Refactor the MacroChef architecture to implement a proper Backend-for-Frontend (BFF) pattern, resolving the dual-write problem where both frontend (BetterAuth/Drizzle) and backend (EF Core) manage auth-related tables.

**Current Problem**:
- Frontend BetterAuth is the source of truth for `users`, `accounts`, `sessions`, `jwks`, `verification` tables
- Backend EF Core includes these tables in migrations and can potentially write to them
- Schema drift risk: Backend migrations could alter tables that BetterAuth owns
- Coupling: Backend depends on frontend auth implementation details
- Security: Backend is publicly exposed and validates JWTs directly from frontend

**Desired Architecture**:
- Backend accesses User table (read-only) without managing it via migrations
- Frontend → Backend communication secured via shared secret (trusted internal network)
- Backend hidden from internet, accessible only via Docker network
- Clear ownership boundaries: Frontend owns auth, Backend owns business logic
- Shared database, but isolated schema management

**Implementation Timeline**: 3-4 days

---

## Architectural Analysis

### Current Architecture (Problematic)

```
┌──────────────────────────────────────────────────────┐
│                    Internet                           │
└───────────────────┬──────────────────┬───────────────┘
                    │                  │
        ┌───────────▼──────────┐   ┌───▼────────────────┐
        │  Next.js Frontend    │   │  .NET Backend API  │
        │  (Port 3000)         │   │  (Port 5000)       │
        │                      │   │                    │
        │  - BetterAuth        │   │  - JWT Validation  │
        │  - Drizzle ORM       │   │  - EF Core         │
        │  - Auth Tables       │   │  - Business Logic  │
        └─────────┬────────────┘   └────────┬───────────┘
                  │                         │
                  │  BOTH write to auth     │
                  │  tables (CONFLICT!)     │
                  │                         │
                  └────────┬────────────────┘
                           ▼
                  ┌────────────────┐
                  │   PostgreSQL   │
                  │                │
                  │  users         │  ← Dual ownership
                  │  accounts      │  ← Dual ownership
                  │  sessions      │  ← Dual ownership
                  │  jwks          │  ← Dual ownership
                  │  verification  │  ← Dual ownership
                  │                │
                  │  foods         │  ← Backend only
                  │  recipes       │  ← Backend only
                  │  workouts      │  ← Backend only
                  └────────────────┘
```

**Problems**:
1. **Dual Write**: Both ORMs can write to auth tables → schema drift, data corruption
2. **Migration Conflicts**: Backend migrations could alter auth schema → breaks BetterAuth
3. **Tight Coupling**: Backend depends on frontend auth implementation (JWKS endpoint)
4. **Public Exposure**: Backend is internet-facing → larger attack surface
5. **Auth Complexity**: Backend validates JWTs directly → duplicated auth logic

### Target Architecture (BFF Pattern)

```
┌──────────────────────────────────────────────────────┐
│                    Internet                           │
└───────────────────────────┬──────────────────────────┘
                            │
                            │ HTTPS (Public)
                            │
                ┌───────────▼──────────────────────────┐
                │  Next.js Frontend (BFF Layer)        │
                │  Port 3000                           │
                │                                      │
                │  ┌─────────────────────────────┐    │
                │  │  Client-Side (Public)       │    │
                │  │  - React Components         │    │
                │  │  - BetterAuth Client        │    │
                │  └─────────────────────────────┘    │
                │                                      │
                │  ┌─────────────────────────────┐    │
                │  │  Server-Side (API Routes)   │    │
                │  │  - BetterAuth Server        │    │
                │  │  - Drizzle ORM              │    │
                │  │  - Auth Management          │    │
                │  │  - Backend API Proxy        │    │
                │  └─────────────────────────────┘    │
                └───────────────┬──────────────────────┘
                                │
                                │ Trusted Secret
                                │ (Docker Network Only)
                                │
                    ┌───────────▼──────────────┐
                    │  .NET Backend API        │
                    │  (Docker Network Only)   │
                    │  NOT exposed to internet │
                    │                          │
                    │  - Business Logic        │
                    │  - EF Core (business     │
                    │    tables only)          │
                    │  - Read-only User entity │
                    │    (mapped, not migrated)│
                    └───────────┬──────────────┘
                                │
                                ▼
                    ┌──────────────────────┐
                    │   PostgreSQL         │
                    │                      │
                    │  Auth Schema         │
                    │  ---------------     │
                    │  users         ←─────┼─── Frontend (Drizzle)
                    │  accounts            │    manages via migrations
                    │  sessions            │
                    │  jwks                │
                    │  verification        │
                    │                      │
                    │  Business Schema     │
                    │  ---------------     │
                    │  foods         ←─────┼─── Backend (EF Core)
                    │  recipes             │    manages via migrations
                    │  workouts            │
                    │  households    ←─────┼─── Shared (Backend migrates,
                    │  ...                 │    Frontend references)
                    └──────────────────────┘
```

**Benefits**:
1. **Clear Ownership**: Frontend owns auth schema, Backend owns business schema
2. **No Migration Conflicts**: EF Core doesn't track auth tables → can't alter them
3. **Security**: Backend hidden behind frontend → reduced attack surface
4. **Simplified Auth**: Frontend validates JWTs, passes trusted userId to backend
5. **Scalability**: Can deploy frontend/backend independently
6. **Future-Proof**: Can swap backend without affecting auth

---

## Detailed Implementation Plan

### Phase 1: Backend Refactor (Authentication Layer)

**Goal**: Make backend agnostic to how users are authenticated. Backend should trust the frontend's authentication decisions.

#### Step 1.1: Remove JWT Validation from Backend

**Current State**:
```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();
```

**Target State**:
```csharp
// Program.cs
builder.Services.AddAuthentication("BffTrustedSource")
    .AddScheme<BffAuthenticationSchemeOptions, BffAuthenticationHandler>("BffTrustedSource", options => { });
```

**Files to Create**:
1. `Mizan.Api/Authentication/BffAuthenticationHandler.cs`:
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Mizan.Api.Authentication;

public class BffAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string TrustedSecret { get; set; } = string.Empty;
}

public class BffAuthenticationHandler : AuthenticationHandler<BffAuthenticationSchemeOptions>
{
    private readonly ILogger<BffAuthenticationHandler> _logger;

    public BffAuthenticationHandler(
        IOptionsMonitor<BffAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<BffAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for trusted secret header
        if (!Request.Headers.TryGetValue("X-BFF-Secret", out var secretValue))
        {
            _logger.LogWarning("Missing X-BFF-Secret header from {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Missing trusted secret header");
        }

        // Validate secret
        if (secretValue != Options.TrustedSecret)
        {
            _logger.LogError("Invalid X-BFF-Secret from {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid trusted secret");
        }

        // Extract user claims from BFF headers
        if (!Request.Headers.TryGetValue("X-User-Id", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            _logger.LogWarning("Missing or invalid X-User-Id header");
            return AuthenticateResult.Fail("Missing or invalid user ID");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString())
        };

        // Optional: Email claim
        if (Request.Headers.TryGetValue("X-User-Email", out var emailValue))
        {
            claims.Add(new Claim(ClaimTypes.Email, emailValue!));
            claims.Add(new Claim("email", emailValue!));
        }

        // Optional: Role claim
        if (Request.Headers.TryGetValue("X-User-Role", out var roleValue))
        {
            claims.Add(new Claim(ClaimTypes.Role, roleValue!));
            claims.Add(new Claim("role", roleValue!));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogInformation("Authenticated user {UserId} via BFF", userId);

        return AuthenticateResult.Success(ticket);
    }
}
```

**Files to Delete**:
1. `Mizan.Api/Services/JwtBearerOptionsSetup.cs`
2. `Mizan.Api/Services/JwksCache.cs`
3. `Mizan.Api/Services/IJwksCache.cs`

**Files to Modify**:
1. `Mizan.Api/Program.cs`:
   - Remove JWT Bearer services
   - Remove JWKS cache services
   - Add BFF authentication
   - Configure trusted secret from environment

**Configuration**:
```json
// appsettings.json
{
  "Bff": {
    "TrustedSecret": "CHANGE_ME_IN_PRODUCTION",
    "AllowedOrigins": ["http://localhost:3000"]
  }
}
```

**Environment Variables**:
```env
BFF__TrustedSecret=<random-256-bit-secret>
```

**Generate Secret** (for deployment):
```bash
# Generate secure random secret
openssl rand -base64 32
```

#### Step 1.2: Mark Auth Tables as NotMapped

**Goal**: Prevent EF Core from generating migrations for auth tables while still allowing read access.

**Current State**:
```csharp
// MizanDbContext.cs
public DbSet<User> Users => Set<User>();
public DbSet<Account> Accounts => Set<Account>();
public DbSet<Session> Sessions => Set<Session>();
public DbSet<Jwk> Jwks => Set<Jwk>();
public DbSet<Verification> Verifications => Set<Verification>();

// OnModelCreating - configures all tables
```

**Target State**:
```csharp
// MizanDbContext.cs
public DbSet<User> Users => Set<User>();
// Remove Account, Session, Jwk, Verification DbSets

// OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Mark auth tables as not managed by EF Core migrations
    // BUT still map them for read access
    ConfigureAuthTablesReadOnly(modelBuilder);

    // Configure business tables (migrated by EF Core)
    ConfigureBusinessTables(modelBuilder);
}

private void ConfigureAuthTablesReadOnly(ModelBuilder modelBuilder)
{
    // User - Read-only, managed by Drizzle
    modelBuilder.Entity<User>(entity =>
    {
        entity.ToTable("users");
        entity.HasKey(e => e.Id);

        // Property mappings (read-only)
        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Email).HasColumnName("email");
        entity.Property(e => e.EmailVerified).HasColumnName("email_verified");
        entity.Property(e => e.Name).HasColumnName("name");
        entity.Property(e => e.Image).HasColumnName("image");
        entity.Property(e => e.Role).HasColumnName("role");
        entity.Property(e => e.Banned).HasColumnName("banned");
        entity.Property(e => e.BanReason).HasColumnName("ban_reason");
        entity.Property(e => e.BanExpires).HasColumnName("ban_expires");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // CRITICAL: Exclude from migrations
        entity.Metadata.SetIsTableExcludedFromMigrations(true);

        // Read-only: Ignore navigation properties that reference auth tables
        entity.Ignore(e => e.Accounts);
        entity.Ignore(e => e.Sessions);
    });

    // Account, Session, Jwk, Verification - REMOVE from DbContext entirely
    // No longer needed by backend
}
```

**Files to Modify**:
1. `Mizan.Infrastructure/Data/MizanDbContext.cs`:
   - Remove `Accounts`, `Sessions`, `Jwks`, `Verifications` DbSets
   - Add `ConfigureAuthTablesReadOnly()` method
   - Mark User table as `SetIsTableExcludedFromMigrations(true)`
   - Remove auth table configurations from `OnModelCreating`

2. `Mizan.Domain/Entities/User.cs`:
   - Remove or mark as `[NotMapped]` auth-related navigation properties
   ```csharp
   public class User
   {
       // Properties remain same

       // Business navigation properties (kept)
       public virtual ICollection<HouseholdMember> HouseholdMemberships { get; set; } = new List<HouseholdMember>();
       public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
       // ... other business relationships

       // Auth navigation properties (removed - managed by frontend)
       // [NotMapped] or completely removed:
       // public virtual ICollection<Account> Accounts { get; set; }
       // public virtual ICollection<Session> Sessions { get; set; }
   }
   ```

3. Delete auth entity files:
   - `Mizan.Domain/Entities/Account.cs`
   - `Mizan.Domain/Entities/Session.cs`
   - `Mizan.Domain/Entities/Jwk.cs`
   - `Mizan.Domain/Entities/Verification.cs`

**Testing**:
```bash
# Generate migration - should NOT include auth tables
dotnet ef migrations add RemoveAuthTablesFromMigrations --project Mizan.Infrastructure --startup-project Mizan.Api

# Verify migration contains only:
# - DROP for Account, Session, Jwk, Verification tables (if they were created by EF)
# - NO ALTER for users table
```

#### Step 1.3: Update Application Interfaces

**Files to Modify**:
1. `Mizan.Application/Interfaces/IMizanDbContext.cs`:
```csharp
public interface IMizanDbContext
{
    // Auth - Read-only access
    DbSet<User> Users { get; }
    // REMOVE: DbSet<Account>, DbSet<Session>, DbSet<Jwk>, DbSet<Verification>

    // Business tables (unchanged)
    DbSet<Household> Households { get; }
    DbSet<Food> Foods { get; }
    // ... rest of business DbSets

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**Testing**:
- Compile project
- Ensure no handlers reference Account, Session, Jwk, or Verification entities
- Search for references: `rg -i "Account|Session|Jwk|Verification" backend/Mizan.Application/`

#### Step 1.4: Update CurrentUserService

**Goal**: Continue using `ICurrentUserService` but extract from BFF headers instead of JWT.

**Current Implementation** (JWT claims):
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

**No Changes Needed**: BFF authentication handler already populates claims → CurrentUserService works unchanged.

---

### Phase 2: Frontend BFF Integration

**Goal**: Create Next.js API routes that proxy authenticated requests to backend with trusted secret.

#### Step 2.1: Create Backend API Client

**File**: `frontend/lib/backend-api-client.ts`

```typescript
import { auth } from "@/lib/auth";

interface BackendApiOptions {
  method?: string;
  body?: any;
  headers?: Record<string, string>;
}

export class BackendApiError extends Error {
  constructor(
    public status: number,
    public statusText: string,
    public body: any
  ) {
    super(`Backend API error: ${status} ${statusText}`);
  }
}

/**
 * Server-side API client for calling the backend with trusted secret
 * MUST only be used in Next.js API routes or Server Components
 */
export async function callBackendApi<T>(
  path: string,
  options: BackendApiOptions = {}
): Promise<T> {
  // Get authenticated session (server-side only)
  const session = await auth();

  if (!session?.user?.id) {
    throw new BackendApiError(401, "Unauthorized", { error: "Not authenticated" });
  }

  const backendUrl = process.env.BACKEND_API_URL || "http://mizan-backend:8080";
  const trustedSecret = process.env.BFF_TRUSTED_SECRET;

  if (!trustedSecret) {
    throw new Error("BFF_TRUSTED_SECRET not configured");
  }

  const url = `${backendUrl}${path}`;

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    "X-BFF-Secret": trustedSecret,
    "X-User-Id": session.user.id,
    ...options.headers,
  };

  // Optional: Pass email and role if available
  if (session.user.email) {
    headers["X-User-Email"] = session.user.email;
  }

  if (session.user.role) {
    headers["X-User-Role"] = session.user.role;
  }

  const response = await fetch(url, {
    method: options.method || "GET",
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new BackendApiError(response.status, response.statusText, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
}
```

#### Step 2.2: Create BFF Proxy API Routes

**File**: `frontend/app/api/bff/[...path]/route.ts`

```typescript
import { NextRequest, NextResponse } from "next/server";
import { callBackendApi, BackendApiError } from "@/lib/backend-api-client";

/**
 * BFF Proxy Route - Forwards authenticated requests to backend
 *
 * Example:
 * - Frontend: GET /api/bff/recipes
 * - Proxies to: GET http://mizan-backend:8080/api/recipes
 * - With: X-BFF-Secret, X-User-Id headers
 */

export async function GET(
  request: NextRequest,
  { params }: { params: { path: string[] } }
) {
  try {
    const path = `/api/${params.path.join("/")}`;
    const searchParams = request.nextUrl.searchParams.toString();
    const fullPath = searchParams ? `${path}?${searchParams}` : path;

    const data = await callBackendApi(fullPath);
    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}

export async function POST(
  request: NextRequest,
  { params }: { params: { path: string[] } }
) {
  try {
    const path = `/api/${params.path.join("/")}`;
    const body = await request.json();

    const data = await callBackendApi(path, {
      method: "POST",
      body,
    });

    return NextResponse.json(data, { status: 201 });
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}

export async function PUT(
  request: NextRequest,
  { params }: { params: { path: string[] } }
) {
  try {
    const path = `/api/${params.path.join("/")}`;
    const body = await request.json();

    const data = await callBackendApi(path, {
      method: "PUT",
      body,
    });

    return NextResponse.json(data);
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: { path: string[] } }
) {
  try {
    const path = `/api/${params.path.join("/")}`;

    await callBackendApi(path, { method: "DELETE" });

    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (error instanceof BackendApiError) {
      return NextResponse.json(error.body, { status: error.status });
    }
    console.error("BFF proxy error:", error);
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    );
  }
}
```

**Usage from Client**:
```typescript
// Before (direct backend call):
const recipes = await apiClient("/api/recipes");

// After (via BFF):
const recipes = await apiClient("/api/bff/recipes");
```

#### Step 2.3: Update Frontend API Client

**File**: `frontend/lib/auth-client.ts`

```typescript
// Update apiClient to use BFF proxy for backend calls
export const apiClient = async (path: string, options?: RequestInit) => {
  // If path starts with /api/auth, call directly (BetterAuth)
  if (path.startsWith("/api/auth")) {
    return fetch(path, options).then(handleResponse);
  }

  // If path starts with /api, proxy via BFF
  if (path.startsWith("/api/")) {
    const bffPath = path.replace("/api/", "/api/bff/");
    return fetch(bffPath, options).then(handleResponse);
  }

  // Other paths (health, etc.) call directly
  return fetch(path, options).then(handleResponse);
};
```

---

### Phase 3: Docker & Network Configuration

#### Step 3.1: Update Docker Compose

**File**: `docker-compose.yml`

```yaml
version: "3.8"

services:
  frontend:
    build: ./frontend
    ports:
      - "3000:3000"
    environment:
      # Database (for BetterAuth)
      DATABASE_URL: postgresql://mizan:mizan_dev_password@postgres:5432/mizan

      # BetterAuth config
      BETTER_AUTH_SECRET: ${BETTER_AUTH_SECRET}
      BETTER_AUTH_URL: http://localhost:3000

      # Backend API (internal network)
      BACKEND_API_URL: http://backend:8080
      BFF_TRUSTED_SECRET: ${BFF_TRUSTED_SECRET}

      # Public URL (for client-side)
      NEXT_PUBLIC_API_URL: http://localhost:3000
    depends_on:
      - postgres
      - redis
      - backend
    networks:
      - app-network

  backend:
    build: ./backend
    # NO PORT EXPOSURE - internal only
    expose:
      - "8080"
    environment:
      # Database
      ConnectionStrings__PostgreSQL: Host=postgres;Database=mizan;Username=mizan;Password=mizan_dev_password

      # Redis
      ConnectionStrings__Redis: redis:6379

      # BFF Authentication
      Bff__TrustedSecret: ${BFF_TRUSTED_SECRET}
      Bff__AllowedOrigins__0: http://frontend:3000

      # OpenAI (optional)
      OpenAI__ApiKey: ${OPENAI_API_KEY}
      OpenAI__ModelId: gpt-4o

      # ASP.NET Core
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080
    depends_on:
      - postgres
      - redis
    networks:
      - app-network

  postgres:
    image: postgres:18-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: mizan
      POSTGRES_USER: mizan
      POSTGRES_PASSWORD: mizan_dev_password
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - app-network

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    networks:
      - app-network

networks:
  app-network:
    driver: bridge

volumes:
  postgres-data:
```

**Key Changes**:
1. Backend has no `ports` mapping (only `expose: - "8080"`)
2. Backend not accessible from host machine
3. Frontend accesses backend via `http://backend:8080` (Docker network)
4. Both services use `BFF_TRUSTED_SECRET` environment variable

**Environment File** (`.env`):
```env
# BetterAuth
BETTER_AUTH_SECRET=your-better-auth-secret-here

# BFF Trusted Secret (CRITICAL - must be same for frontend and backend)
BFF_TRUSTED_SECRET=your-trusted-secret-here

# OpenAI (optional)
OPENAI_API_KEY=sk-your-openai-key-here
```

**Generate Secrets**:
```bash
# Generate BetterAuth secret
openssl rand -base64 32

# Generate BFF trusted secret
openssl rand -base64 32
```

#### Step 3.2: Production Deployment Notes

**Security Recommendations**:

1. **Secrets Management**:
   - Use Docker secrets in production
   - Rotate `BFF_TRUSTED_SECRET` regularly
   - Never commit secrets to git

2. **Network Isolation**:
   - Backend MUST NOT be exposed to internet
   - Use internal Docker network or private VPC
   - Only frontend should have public ingress

3. **HTTPS**:
   - Terminate HTTPS at reverse proxy (Nginx, Traefik, Caddy)
   - Frontend communicates with backend over internal network (HTTP OK)
   - Use Let's Encrypt for SSL certificates

4. **Health Checks**:
   - Frontend: `/api/health` (public, returns 200 if healthy)
   - Backend: `/health` (internal only, checks DB + Redis)

**Example Nginx Config** (production):
```nginx
# Reverse proxy for frontend
server {
    listen 443 ssl http2;
    server_name macrochef.example.com;

    ssl_certificate /etc/letsencrypt/live/macrochef.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/macrochef.example.com/privkey.pem;

    location / {
        proxy_pass http://frontend:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

# Backend is NOT exposed via Nginx
# Only accessible via Docker network from frontend
```

---

### Phase 4: Testing & Validation

#### Step 4.1: Unit Tests

**Test BFF Authentication Handler**:

```csharp
// Mizan.Tests/Api/BffAuthenticationHandlerTests.cs
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Xunit;
using FluentAssertions;

namespace Mizan.Tests.Api;

public class BffAuthenticationHandlerTests
{
    [Fact]
    public async Task HandleAuthenticate_ShouldFail_WhenSecretMissing()
    {
        // Arrange
        var context = CreateHttpContext(headers: new Dictionary<string, string>());
        var handler = CreateHandler(context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Contain("Missing trusted secret");
    }

    [Fact]
    public async Task HandleAuthenticate_ShouldFail_WhenSecretInvalid()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "X-BFF-Secret", "wrong-secret" },
            { "X-User-Id", Guid.NewGuid().ToString() }
        };
        var context = CreateHttpContext(headers);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid trusted secret");
    }

    [Fact]
    public async Task HandleAuthenticate_ShouldSucceed_WhenValidSecret()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var headers = new Dictionary<string, string>
        {
            { "X-BFF-Secret", "test-secret" },
            { "X-User-Id", userId.ToString() },
            { "X-User-Email", "test@example.com" }
        };
        var context = CreateHttpContext(headers);
        var handler = CreateHandler(context, trustedSecret: "test-secret");

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();

        var userIdClaim = result.Principal!.FindFirst(ClaimTypes.NameIdentifier);
        userIdClaim.Should().NotBeNull();
        userIdClaim!.Value.Should().Be(userId.ToString());
    }
}
```

#### Step 4.2: Integration Tests

**Test BFF Proxy Route**:

```typescript
// frontend/__tests__/api/bff-proxy.test.ts
import { describe, it, expect, beforeEach } from "vitest";
import { callBackendApi } from "@/lib/backend-api-client";

describe("BFF Proxy", () => {
  beforeEach(() => {
    // Mock auth session
    vi.mock("@/lib/auth", () => ({
      auth: vi.fn().mockResolvedValue({
        user: {
          id: "test-user-id",
          email: "test@example.com",
          role: "user",
        },
      }),
    }));
  });

  it("should proxy GET request to backend", async () => {
    const response = await fetch("/api/bff/recipes");
    expect(response.ok).toBe(true);
  });

  it("should proxy POST request with body", async () => {
    const recipe = { title: "Test Recipe", ingredients: [] };
    const response = await fetch("/api/bff/recipes", {
      method: "POST",
      body: JSON.stringify(recipe),
      headers: { "Content-Type": "application/json" },
    });
    expect(response.status).toBe(201);
  });

  it("should return 401 when not authenticated", async () => {
    // Mock unauthenticated session
    vi.mocked(auth).mockResolvedValue(null);

    const response = await fetch("/api/bff/recipes");
    expect(response.status).toBe(401);
  });
});
```

#### Step 4.3: End-to-End Testing

**Test Recipe Creation Flow**:

```typescript
// frontend/e2e/bff-integration.spec.ts
import { test, expect } from "@playwright/test";

test.describe("BFF Integration", () => {
  test.beforeEach(async ({ page }) => {
    // Login via BetterAuth
    await page.goto("/login");
    await page.fill('input[name="email"]', "test@example.com");
    await page.fill('input[name="password"]', "password123");
    await page.click('button[type="submit"]');
    await page.waitForURL("/dashboard");
  });

  test("should create recipe via BFF", async ({ page }) => {
    // Navigate to recipe creation
    await page.goto("/recipes/new");

    // Fill form
    await page.fill('input[name="title"]', "Ethiopian Doro Wat");
    await page.fill('textarea[name="description"]', "Traditional chicken stew");

    // Add ingredient
    await page.click('button:has-text("Add Ingredient")');
    await page.fill('input[name="ingredients.0.text"]', "2 lbs chicken");

    // Submit
    await page.click('button[type="submit"]');

    // Verify redirect
    await page.waitForURL(/\/recipes\/[a-f0-9-]+/);

    // Verify recipe created
    await expect(page.locator("h1")).toContainText("Ethiopian Doro Wat");
  });

  test("should fetch recipes via BFF", async ({ page }) => {
    await page.goto("/recipes");

    // Wait for recipes to load (via BFF proxy)
    await page.waitForSelector('[data-testid="recipe-card"]');

    // Verify recipes displayed
    const recipeCards = page.locator('[data-testid="recipe-card"]');
    await expect(recipeCards).not.toHaveCount(0);
  });
});
```

---

### Phase 5: Migration Strategy

#### Step 5.1: Pre-Migration Checklist

**Before deploying**:
- [ ] Generate `BFF_TRUSTED_SECRET` for production
- [ ] Update environment variables in deployment
- [ ] Test BFF proxy in staging environment
- [ ] Verify backend not exposed to internet
- [ ] Run integration tests against staging
- [ ] Backup database before migration
- [ ] Document rollback procedure

#### Step 5.2: Deployment Steps (Zero-Downtime)

**Step 1: Deploy Backend First** (compatible with old frontend):
```bash
# Deploy backend with BFF auth + legacy JWT support
# Backend should support BOTH:
# 1. BFF authentication (X-BFF-Secret header)
# 2. Legacy JWT authentication (Authorization: Bearer)

# This allows gradual migration
```

**Step 2: Deploy Frontend** (use BFF proxy):
```bash
# Deploy frontend with BFF proxy routes
# Frontend calls /api/bff/* instead of direct backend
```

**Step 3: Verify** (monitor for errors):
```bash
# Check frontend logs
docker-compose logs frontend

# Check backend logs
docker-compose logs backend

# Verify authentication working
curl -H "Cookie: auth-token=..." http://localhost:3000/api/bff/recipes
```

**Step 4: Remove Legacy JWT Support** (after verification):
```bash
# Once all traffic uses BFF, remove JWT authentication from backend
# This is the point of no return
```

#### Step 5.3: Rollback Procedure

**If BFF fails**:
1. Redeploy frontend without BFF proxy (use direct backend calls)
2. Re-enable JWT authentication in backend
3. Investigate BFF failure
4. Fix and retry migration

**Database Rollback**:
- No schema changes needed (auth tables unchanged)
- Only code changes need reverting

---

## Summary of Changes

### Backend Changes

**Files Created**:
1. `Mizan.Api/Authentication/BffAuthenticationHandler.cs` - Custom auth handler
2. `Mizan.Api/Authentication/BffAuthenticationSchemeOptions.cs` - Auth options

**Files Deleted**:
1. `Mizan.Api/Services/JwtBearerOptionsSetup.cs`
2. `Mizan.Api/Services/JwksCache.cs`
3. `Mizan.Api/Services/IJwksCache.cs`
4. `Mizan.Domain/Entities/Account.cs`
5. `Mizan.Domain/Entities/Session.cs`
6. `Mizan.Domain/Entities/Jwk.cs`
7. `Mizan.Domain/Entities/Verification.cs`

**Files Modified**:
1. `Mizan.Api/Program.cs` - Replace JWT auth with BFF auth
2. `Mizan.Api/Mizan.Api.csproj` - Remove JWT packages
3. `Mizan.Infrastructure/Data/MizanDbContext.cs` - Mark auth tables as not migrated
4. `Mizan.Domain/Entities/User.cs` - Remove auth navigation properties
5. `Mizan.Application/Interfaces/IMizanDbContext.cs` - Remove auth DbSets

**NuGet Packages Removed**:
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- (JWT-related packages no longer needed)

### Frontend Changes

**Files Created**:
1. `frontend/lib/backend-api-client.ts` - Server-side backend client
2. `frontend/app/api/bff/[...path]/route.ts` - BFF proxy routes

**Files Modified**:
1. `frontend/lib/auth-client.ts` - Route to BFF proxy for backend calls
2. `frontend/next.config.ts` - Remove backend proxy rewrites (handled by BFF routes)

**Environment Variables Added**:
- `BACKEND_API_URL` (server-side, internal network)
- `BFF_TRUSTED_SECRET` (shared with backend)

### Infrastructure Changes

**Docker Compose**:
- Backend: Remove `ports` mapping, only `expose`
- Frontend: Add `BACKEND_API_URL` and `BFF_TRUSTED_SECRET`
- Both: Connect to `app-network`

**Security Improvements**:
- Backend hidden from internet
- Trusted secret authentication
- Reduced attack surface

---

## Implementation Timeline

### Day 1: Backend Refactor
- [ ] Create BFF authentication handler
- [ ] Mark auth tables as not migrated
- [ ] Remove JWT validation logic
- [ ] Update Program.cs
- [ ] Run unit tests
- [ ] Generate new migration (verify no auth table changes)

### Day 2: Frontend BFF Integration
- [ ] Create backend API client
- [ ] Create BFF proxy routes
- [ ] Update auth-client to use BFF
- [ ] Test locally with Docker Compose
- [ ] Run integration tests

### Day 3: Docker & Testing
- [ ] Update docker-compose.yml
- [ ] Configure environment variables
- [ ] Test end-to-end flows
- [ ] Verify backend not exposed
- [ ] Load testing (optional)

### Day 4: Deployment & Validation
- [ ] Deploy to staging
- [ ] Run smoke tests
- [ ] Monitor logs
- [ ] Deploy to production (gradual rollout)
- [ ] Final verification

---

## Open Questions & Decisions

### Q1: Should backend support both BFF and JWT during transition?

**Recommendation**: Yes, for zero-downtime migration.

**Implementation**:
```csharp
// Program.cs
builder.Services.AddAuthentication()
    .AddScheme<BffAuthenticationSchemeOptions, BffAuthenticationHandler>("BffTrustedSource", options => { })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => { /* legacy config */ });

// Set default to BFF, fallback to JWT
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("BffTrustedSource", JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});
```

### Q2: How to handle SignalR with BFF?

**Current**: SignalR hub uses JWT from query string (`?access_token=...`)

**Options**:
1. **Keep JWT for SignalR**: BFF generates short-lived JWT for SignalR connections
2. **Custom SignalR Auth**: Use same X-BFF-Secret + X-User-Id headers
3. **Separate SignalR Gateway**: Frontend proxy for SignalR

**Recommendation**: Option 1 (BFF generates JWT for SignalR only)

**Implementation**:
```typescript
// frontend/app/api/signalr/token/route.ts
export async function GET() {
  const session = await auth();
  if (!session?.user?.id) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  // Generate short-lived JWT for SignalR (1 hour)
  const token = generateSignalRToken(session.user.id, "1h");

  return NextResponse.json({ token });
}
```

### Q3: Should frontend cache backend responses?

**Recommendation**: Yes, implement caching at BFF layer.

**Implementation**:
```typescript
// frontend/lib/backend-api-client.ts
import { unstable_cache } from "next/cache";

export const callBackendApiCached = unstable_cache(
  async (path: string) => callBackendApi(path),
  ["backend-api"],
  { revalidate: 60 } // 1-minute cache
);
```

---

## Success Criteria

### Technical Metrics

- [ ] Backend not accessible from internet (port scan verification)
- [ ] All requests to backend include `X-BFF-Secret` header
- [ ] Zero schema drift warnings in logs
- [ ] EF Core migrations don't touch auth tables
- [ ] Frontend can create/read/update/delete all resources
- [ ] SignalR real-time features working
- [ ] Response times < 200ms (95th percentile)

### Security Metrics

- [ ] `BFF_TRUSTED_SECRET` is 256-bit random value
- [ ] No secrets in source control
- [ ] Backend rejects requests without valid secret
- [ ] Auth tables only modified by Drizzle migrations
- [ ] User table read-only from backend perspective

### Business Metrics

- [ ] Zero downtime during migration
- [ ] No user-facing errors
- [ ] All features working post-migration
- [ ] Performance same or better than before

---

## References

- [Backend-for-Frontend Pattern](https://samnewman.io/patterns/architectural/bff/)
- [ASP.NET Core Custom Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/custom-scheme)
- [EF Core Table Exclusions](https://learn.microsoft.com/en-us/ef/core/modeling/table-exclusion)
- [Next.js API Routes](https://nextjs.org/docs/app/building-your-application/routing/route-handlers)
- [Docker Networking](https://docs.docker.com/network/)

---

## Conclusion

This BFF refactor resolves the critical dual-write problem while maintaining all existing functionality. The architecture clearly separates concerns:

- **Frontend**: Owns auth schema, validates JWTs, proxies business requests
- **Backend**: Owns business schema, trusts frontend authentication, isolated from internet
- **Shared Database**: No schema conflicts, clear migration ownership

**Benefits**:
1. **Security**: Backend not exposed to internet → reduced attack surface
2. **Maintainability**: Clear ownership → no migration conflicts
3. **Flexibility**: Can swap auth providers without backend changes
4. **Scalability**: Frontend and backend can scale independently

**Trade-offs**:
1. **Latency**: Extra network hop (Frontend → Backend) → mitigated by caching
2. **Complexity**: More moving parts → clear documentation and testing mitigates
3. **Debugging**: Request tracing across services → OpenTelemetry recommended

This architecture is production-ready and follows industry best practices for microservices and BFF patterns.
