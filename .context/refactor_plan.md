## MacroChef BFF Refactoring Plan

  Based on the guide you provided, Next.js 16 deprecation of middleware for auth, and the CVE-2025-29927 security vulnerability, here's your   
  comprehensive refactoring plan.

  Executive Summary

  Current State:
  - Backend validates JWT tokens via JWKS endpoint
  - Backend has FULL write access to auth tables (violates BetterAuth ownership)
  - JWT validation adds latency (JWKS cache, Redis dependency)
  - Not a true BFF pattern

  Target State:
  - Next.js acts as Backend-for-Frontend (BFF) layer
  - Backend trusts Next.js via shared secret
  - Backend only READS from user tables (BetterAuth owns writes)
  - Data Access Layer (DAL) for session validation
  - Simpler, faster, more secure

  Current Architecture Analysis

  What's Working ✅

  1. BetterAuth handles authentication on frontend
  2. Shared PostgreSQL database
  3. Proxying already configured via next.config.ts rewrites
  4. Docker network isolation (backend not exposed to internet)

  Critical Issues ❌

  1. Schema Ownership Violation: Backend EF Core has WRITE access to users, sessions, accounts, jwks, verification tables
    - MizanDbContext.cs:14-18 defines DbSets with full write access
    - Migrations can modify auth tables (dangerous!)
    - BetterAuth expects to be sole owner
  2. JWT Validation Overhead:
    - Backend fetches JWKS from Next.js (JwtBearerOptionsSetup.cs:54)
    - Redis caching layer (JwksCache)
    - Extra network hop for every request
    - Unnecessary complexity for internal service
  3. Not a True BFF:
    - Client can potentially call backend directly (if exposed)
    - No trusted header pattern
    - Backend doesn't trust Next.js implicitly
  4. Security Vulnerability:
    - Middleware-based auth is deprecated (CVE-2025-29927)
    - Need Data Access Layer pattern instead

  Refactoring Plan

  Phase 1: Frontend - Data Access Layer (DAL)

  Goal: Create centralized session verification layer

  1.1 Create DAL for Session Verification

  File: frontend/lib/dal.ts (new)

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

  1.2 Create Internal API Proxy

  File: frontend/lib/api-proxy.ts (new)

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

  1.3 Update Environment Variables

  File: frontend/.env.local (add)

  # Internal API secret (must match backend)
  INTERNAL_API_SECRET="your-strong-secret-here-min-32-chars"

  1.4 Update Client-Side API Client

  File: frontend/lib/api-client.ts (new - for client components only)

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

  1.5 Create API Route Handler (Generic Proxy)

  File: frontend/app/api/proxy/[...path]/route.ts (new)

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

  // Add PUT, PATCH, DELETE similarly

  Phase 2: Backend - Trust Next.js

  Goal: Remove JWT validation, trust internal headers

  2.1 Create Internal API Middleware

  File: backend/Mizan.Api/Middleware/InternalApiMiddleware.cs (new)

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

  2.2 Update Program.cs

  File: backend/Mizan.Api/Program.cs

  Remove JWT authentication, add internal middleware:

  // REMOVE these lines:
  // - builder.Services.AddHttpClient("JwksClient", ...);
  // - builder.Services.AddSingleton<IJwksCache, JwksCache>();
  // - builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();
  // - builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
  // - Redis connection for JWKS caching (if only used for JWKS)

  // REMOVE this line from pipeline:
  // - app.UseAuthentication();

  // ADD Internal API middleware BEFORE UseAuthorization:
  app.UseMiddleware<InternalApiMiddleware>();
  app.UseAuthorization(); // Keep this for [Authorize] attribute support

  Full updated section:

  // REMOVE JWT-related services (lines 58-71 in current Program.cs)

  // Keep SignalR, CORS, HealthChecks as-is

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
  app.UseAuthorization(); // Keep for [Authorize] attribute

  app.MapControllers();
  app.MapHub<ChatHub>("/hubs/chat");
  app.MapHealthChecks("/health");

  2.3 Update Environment Variables

  File: backend/Mizan.Api/appsettings.Development.json

  {
    "InternalApiSecret": "your-strong-secret-here-min-32-chars",
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }

  File: docker-compose.yml

  services:
    mizan-backend:
      environment:
        - InternalApiSecret=${INTERNAL_API_SECRET}
      # REMOVE: BetterAuth__JwksUrl, BetterAuth__Issuer, BetterAuth__Audience

  2.4 Make User Entity READ-ONLY

  Option A: Keep entity, mark as read-only

  File: backend/Mizan.Infrastructure/Data/MizanDbContext.cs

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
      base.OnModelCreating(modelBuilder);

      // User configuration - READ ONLY (BetterAuth owns writes)
      modelBuilder.Entity<User>(entity =>
      {
          entity.ToTable("users");
          entity.HasKey(e => e.Id);
          // ... all Property mappings stay the same ...

          // MARK AS READ-ONLY (no inserts, updates, deletes)
          entity.ToTable(tb => tb.ExcludeFromMigrations());
      });

      // Same for Account, Session, Jwk, Verification
      modelBuilder.Entity<Account>(entity => {
          // ... existing config ...
          entity.ToTable(tb => tb.ExcludeFromMigrations());
      });

      modelBuilder.Entity<Session>(entity => {
          // ... existing config ...
          entity.ToTable(tb => tb.ExcludeFromMigrations());
      });

      modelBuilder.Entity<Jwk>(entity => {
          // ... existing config ...
          entity.ToTable(tb => tb.ExcludeFromMigrations());
      });

      modelBuilder.Entity<Verification>(entity => {
          // ... existing config ...
          entity.ToTable(tb => tb.ExcludeFromMigrations());
      });
  }

  Option B: Remove entities entirely (if backend doesn't need to read user data)

  Remove User, Account, Session, Jwk, Verification from DbContext entirely. Use Guid UserId directly in your entities and join via raw SQL if  
  needed.

  Recommendation: Keep entities as READ-ONLY (Option A) for foreign key relationships.

  2.5 Update Controllers to Use ClaimsPrincipal

  File: backend/Mizan.Api/Controllers/BaseController.cs (create if doesn't exist)

  using Microsoft.AspNetCore.Mvc;

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

  Update all controllers:

  public class FoodsController : BaseController  // Inherit from BaseController
  {
      [HttpGet]
      public async Task<IActionResult> GetMyFoods()
      {
          var userId = GetUserId(); // From BaseController
          // ... rest of logic
      }
  }

  2.6 Remove JWKS-Related Code

  Delete these files:
  - backend/Mizan.Api/Services/JwtBearerOptionsSetup.cs
  - backend/Mizan.Api/Services/IJwksCache.cs (if exists)
  - backend/Mizan.Api/Services/JwksCache.cs (if exists)

  File: backend/Mizan.Api/Mizan.Api.csproj

  Remove:
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />

  Phase 3: Migration Strategy

  3.1 Feature Flag Approach

  Create a feature flag to switch between old JWT auth and new internal auth:

  File: backend/Mizan.Api/appsettings.Development.json

  {
    "FeatureFlags": {
      "UseInternalAuth": false  // Set to true when ready to switch
    }
  }

  Conditional middleware registration in Program.cs:

  var useInternalAuth = builder.Configuration.GetValue<bool>("FeatureFlags:UseInternalAuth");

  if (useInternalAuth)
  {
      app.UseMiddleware<InternalApiMiddleware>();
  }
  else
  {
      app.UseAuthentication(); // Old JWT auth
  }

  app.UseAuthorization();

  3.2 Testing Plan

  Before cutover:
  1. Test DAL session verification with existing auth
  2. Test internal API proxy with feature flag OFF (should fail)
  3. Add integration tests for internal middleware

  During cutover:
  1. Set UseInternalAuth=true
  2. Restart backend
  3. Test critical user flows (login, create recipe, etc.)
  4. Monitor logs for auth failures

  After cutover:
  1. Remove feature flag
  2. Remove JWT-related code
  3. Update documentation

  3.3 Rollback Plan

  If issues arise:
  1. Set UseInternalAuth=false in config
  2. Restart backend
  3. Old JWT auth resumes immediately

  Phase 4: SignalR Updates

  SignalR currently extracts JWT from query string. Update to use session cookie instead.

  File: backend/Mizan.Api/Middleware/InternalApiMiddleware.cs

  Add special handling for SignalR:

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
              context.Response.StatusCode = 403;
              await context.Response.WriteAsJsonAsync(new { error = "Forbidden" });
              return;
          }

          await _next(context);
          return;
      }

      // ... rest of existing code
  }

  File: backend/Mizan.Api/Hubs/ChatHub.cs

  public override async Task OnConnectedAsync()
  {
      var userId = Context.User?.FindFirst("user_id")?.Value;

      if (string.IsNullOrEmpty(userId))
      {
          Context.Abort();
          return;
      }

      // Add to group by user ID
      await Groups.AddToGroupAsync(Context.ConnectionId, userId);
      await base.OnConnectedAsync();
  }

  Frontend SignalR client: Update to not send access_token in query string (session cookie will be sent automatically).

  Phase 5: Documentation Updates

  5.1 Update auth-architecture.md

  File: .context/auth-architecture.md

  Replace authentication flow section with:

  ## Authentication Flow (BFF Pattern)

  ### User Login
  User → Frontend (BetterAuth) → Validate credentials
                                ↓
                       Create session in PostgreSQL
                                ↓
                       Set httpOnly session cookie

  ### API Request (Server Component)
  Server Component → verifySession() (DAL)
                                ↓
                       Check session cookie
                                ↓
                       Validate with BetterAuth
                                ↓
                       callBackend() with X-User-Id header
                                ↓
  Backend validates X-Internal-Secret → Trusts X-User-Id → Sets ClaimsPrincipal

  ### API Request (Client Component)
  Client Component → apiClient() → /api/proxy/*
                                         ↓
                                Next.js API Route → verifySession()
                                         ↓
                                callBackend() with trusted headers
                                         ↓
                                Backend trusts Next.js

  ### Key Security Features
  - Backend NOT exposed to internet (Docker network only)
  - Backend validates INTERNAL_API_SECRET (shared secret)
  - Next.js validates session, backend trusts Next.js
  - No JWT validation overhead
  - BetterAuth owns auth tables (backend read-only)

  5.2 Update CLAUDE.md

  Update authentication section to reflect new BFF pattern.

  Security Implications & Trade-offs

  ✅ Security Improvements

  1. CVE-2025-29927 Mitigation: No more middleware-based auth
  2. Principle of Least Privilege: Backend can only READ user data
  3. Defense in Depth: Multiple layers (DAL, internal secret, ClaimsPrincipal)
  4. Attack Surface Reduction: Backend not exposed to internet
  5. Session-Based Auth: More secure than JWT for server-to-server

  ⚠️ Trade-offs

  1. Shared Secret Dependency:
    - Risk: If INTERNAL_API_SECRET leaks, attacker can impersonate any user
    - Mitigation: Use strong secret (min 32 chars), rotate regularly, env var only
  2. Single Point of Failure:
    - Risk: Next.js goes down = entire app down
    - Mitigation: Already the case (Next.js serves frontend), add health checks
  3. Stateful Sessions:
    - Risk: Session table growth, DB dependency
    - Mitigation: BetterAuth handles cleanup, PostgreSQL can handle scale
  4. Trust Boundary:
    - Risk: Backend implicitly trusts Next.js (no double-check)
    - Mitigation: Docker network isolation, internal secret validation

  🔒 Best Practices

  1. Rotate Internal Secret every 90 days
  2. Monitor Failed Auth Attempts (log X-Internal-Secret mismatches)
  3. Rate Limit Next.js → Backend requests (if needed)
  4. Use HTTPS in production (even within Docker network)
  5. Audit User Access via PostgreSQL logs

  Migration Checklist

  - Phase 1: Frontend DAL
    - Create frontend/lib/dal.ts
    - Create frontend/lib/api-proxy.ts
    - Create frontend/lib/api-client.ts
    - Create frontend/app/api/proxy/[...path]/route.ts
    - Add INTERNAL_API_SECRET to .env.local
    - Test session verification
  - Phase 2: Backend Trust
    - Create backend/Mizan.Api/Middleware/InternalApiMiddleware.cs
    - Update Program.cs (add middleware, remove JWT auth)
    - Add InternalApiSecret to appsettings.Development.json
    - Update docker-compose.yml env vars
    - Mark auth tables as read-only in MizanDbContext.cs
    - Create BaseController.cs with GetUserId()
    - Update all controllers to inherit from BaseController
    - Remove JWKS-related files
  - Phase 3: Migration
    - Add feature flag UseInternalAuth
    - Test with flag OFF (should work with old JWT auth)
    - Test with flag ON (should work with new internal auth)
    - Flip flag in production
    - Monitor for issues
    - Remove feature flag after 7 days
  - Phase 4: SignalR
    - Update InternalApiMiddleware for /hubs path
    - Update ChatHub.OnConnectedAsync()
    - Remove access_token query param from frontend
  - Phase 5: Documentation
    - Update .context/auth-architecture.md
    - Update CLAUDE.md
    - Update README.md (if needed)
    - Document rollback procedure
  - Phase 6: Cleanup
    - Remove JWT packages from Mizan.Api.csproj
    - Remove JWKS cache code
    - Remove BetterAuth env vars from backend
    - Remove Redis if only used for JWKS (keep if SignalR uses it)
    - Archive old auth code in git history

  Estimated Effort

  - Phase 1 (Frontend DAL): 4-6 hours
  - Phase 2 (Backend Trust): 3-4 hours
  - Phase 3 (Migration): 2-3 hours (+ monitoring)
  - Phase 4 (SignalR): 1-2 hours
  - Phase 5 (Documentation): 1-2 hours
  - Phase 6 (Cleanup): 1 hour

  Total: 12-18 hours

  Sources

  Research based on:
  - https://nextjs.org/blog/next-16
  - https://u11d.com/blog/nextjs-16-proxy-vs-middleware-bff-guide/
  - https://dev.to/aakash_shrivas_0806333bbe/nextjs-middleware-is-changing-what-you-need-to-know-about-the-move-toward-proxies-3ndk
  - https://www.franciscomoretti.com/blog/modern-nextjs-authentication-best-practices
  - https://projectdiscovery.io/blog/nextjs-middleware-authorization-bypass

  ---
  Ready to proceed with implementation? I can help you execute any phase step-by-step.