# MacroChef Architecture

## Overview

MacroChef is a full-stack meal planning and nutrition tracking application built with a modern, scalable architecture.

**Tech Stack:**
- **Frontend:** Next.js 16 (App Router) + React 19 + TypeScript + Tailwind CSS
- **Backend:** ASP.NET Core 10 (Web API) + Clean Architecture + C#
- **Database:** PostgreSQL 18
- **Cache:** Redis 7 (SignalR backplane + application caching)
- **Authentication:** BetterAuth (JWT-based)
- **Deployment:** Docker + Docker Compose (self-hosted)

---

## Architecture Principles

### 1. Hybrid Pragmatic Approach

We accept **intentional schema separation** while automating the critical validation layer:

- **Backend owns business logic schema** (Recipes, Foods, Meals, etc.) via EF Core
- **Frontend owns auth schema** (Users, Sessions, JWKS) via Drizzle (BetterAuth requirement)
- **Validation is synchronized** via OpenAPI → Zod schema generation
- **Types are synchronized** via OpenAPI → TypeScript type generation
- **Case conversion** automatic (PascalCase ↔ camelCase) in API client

### 2. Schema Boundaries

#### Frontend Schema Zone (Drizzle ORM)
**Owner:** BetterAuth (authentication system)
**Tables:**
- `users` - User accounts
- `accounts` - OAuth provider accounts
- `sessions` - Active user sessions
- `jwks` - (removed Jan 2026) formerly stored JWT signing keys
- `verification` - Email verification tokens

**Why separate?**
BetterAuth requires ORM control for auth schema management. Drizzle provides the flexibility needed for auth flows while maintaining type safety.

#### Backend Schema Zone (EF Core)
**Owner:** Business logic (Clean Architecture)
**Tables:**
- `foods` - Ingredient database
- `recipes` - User recipes
- `recipe_ingredients` - Recipe-food relationships
- `meal_plans` - Weekly meal plans
- `food_diary_entries` - Daily nutrition logs
- `workouts` - Workout logs
- `body_measurements` - Body composition tracking
- `achievements` - Gamification system
- `households` - Multi-user groups
- `trainers` - Trainer-client relationships

**Why backend?**
Complex business logic, validation rules, and domain-driven design are best expressed in C# with EF Core. Backend serves as the source of truth for business data.

#### Shared Boundary
**Table:** `households` (linked by both frontend and backend)
**Constraint:** Backend is the source of truth. Frontend references via user associations.

---

## Data Flow Architecture

### Request Flow

```
Browser → Next.js (proxy) → Backend API → PostgreSQL
   ↓          ↓                    ↓           ↓
  JWT    Validation           EF Core    Business Data
         (Zod)                  ↓
                           Redis Cache
```

### Authentication Flow

```
1. User submits login → BetterAuth (Next.js)
2. BetterAuth validates → Creates session + JWT
3. JWT sent to frontend → Stored in httpOnly cookie
4. API requests → JWT in Authorization header
5. Backend validates JWT → Uses JWKS from BetterAuth endpoint
6. JWKS cached in Redis → 1-minute TTL
```

### Type Safety Flow

```
Backend (C# DTOs)
    ↓
OpenAPI spec (with FluentValidation metadata)
    ↓
┌─────────────────┬──────────────────┐
│                 │                  │
TypeScript Types  Zod Schemas     (Code generation)
    ↓                 ↓
Frontend Components  Form Validation
```

---

## API Routing

### Next.js Proxy Configuration

**Handled by Next.js:**
- `/api/auth/*` - BetterAuth endpoints
- `/api/health` - Frontend health check
- `/api/csrf` - CSRF token management

**Proxied to Backend:**
- `/api/Users/*` → `http://backend:8080/api/Users/*`
- `/api/Foods/*` → `http://backend:8080/api/Foods/*`
- `/api/Recipes/*` → `http://backend:8080/api/Recipes/*`
- `/api/MealPlans/*` → `http://backend:8080/api/MealPlans/*`
- `/api/Workouts/*` → `http://backend:8080/api/Workouts/*`
- `/api/Goals/*` → `http://backend:8080/api/Goals/*`
- `/hubs/*` → `http://backend:8080/hubs/*` (SignalR)

**Network Topology:**
- **Client → Frontend (browser):** `http://localhost:3000`
- **Frontend → Backend (server-side):** `http://mizan-backend:8080` (Docker network)
- **Frontend → Backend (client-side):** `http://localhost:5000` (direct API)

---

## Security Architecture

### JWT (JWKS) Authentication Pattern

MacroChef uses **direct JWT validation**: the backend validates BetterAuth JWTs using JWKS and checks user status in the database.

**Flow:**
```
Browser → Next.js → Backend API
   ↓          ↓         ↓
 Session   JWT Token   JWT + DB check
 (cookie)  (/api/auth/token)
```

**Implementation:**
1. Browser signs in and receives a BetterAuth session cookie (httpOnly)
2. Frontend fetches `/api/auth/token` and sends `Authorization: Bearer <jwt>` to the backend
3. Backend validates JWT signature via JWKS and enforces issuer/audience
4. Backend checks user status (exists, verified, not banned) with a short cache

**Security Features:**
- **JWKS validation** - Backend verifies signatures against BetterAuth JWKS
- **Issuer/Audience enforcement** - Prevents token replay across environments
- **Cached user-status lookups** - Minimizes DB reads while enforcing bans/verification

**Configuration:**
```
Frontend: BETTER_AUTH_SECRET, BETTER_AUTH_URL, BETTER_AUTH_ISSUER, BETTER_AUTH_AUDIENCE
Backend: BetterAuth:Issuer, BetterAuth:Audience, BetterAuth:JwksUrl
```

### Authentication

**JWT Configuration:**
- **Algorithm:** ES256 (ECDSA P-256) for .NET compatibility (configurable)
- **Token Expiry:** 15 minutes (JWT), 7 days (session)
- **Cookie Security:**
  - `httpOnly: true` (no JavaScript access)
  - `sameSite: "lax"` (CSRF protection)
  - `secure: true` (production only, HTTPS)

**BetterAuth Features:**
- User/password authentication
- Session management
- Admin plugin (user management, impersonation, ban/unban)
- Organization plugin (household management)
- Access control plugin (role-based permissions)

### Authorization

**Three-Tier Role System:**

| Role | Access Level | Features |
|------|-------------|----------|
| `user` | Personal data only | Meal plans, recipes, workouts, nutrition tracking, household membership |
| `trainer` | User features + client management | Client list, workout assignment, messaging, client data viewing (with consent) |
| `admin` | Full system access | User management, role assignment, impersonation, ban/unban, system configuration |

**Permission Model:**

Trainer-client relationships include granular permissions:
- `canViewNutrition` - Trainer can view client's food diary
- `canViewWorkouts` - Trainer can view client's workout logs
- `canViewMeasurements` - Trainer can view client's body measurements
- `canMessage` - Trainer can chat with client via SignalR

**Authorization Patterns:**

All query/command handlers follow this pattern:
```csharp
// 1. Check authentication
if (!_currentUser.UserId.HasValue)
    throw new UnauthorizedAccessException();

// 2. Filter by ownership
var entity = await _context.Entities
    .FirstOrDefaultAsync(e => e.Id == id && e.UserId == _currentUser.UserId);

// 3. Validate permissions (for relationship-based resources)
if (!relationship.CanViewNutrition)
    throw new UnauthorizedAccessException();
```

**Access Control (Optional):**

Better Auth Access Control plugin provides fine-grained permissions:
```typescript
// Define resources and actions
const statement = {
  user: ["create", "read", "update", "delete", "ban"],
  recipe: ["create", "read", "update", "delete"],
  mealPlan: ["create", "read", "update", "delete"],
  workout: ["create", "read", "update", "delete"],
  household: ["create", "read", "update", "delete", "invite"],
  trainerClient: ["create", "read", "update", "delete", "message"],
};

// Check permissions
const canDelete = authClient.admin.checkRolePermission({
  role: "trainer",
  permissions: { workout: ["delete"] }
});
```

**Next.js 16 Proxy Protection:**

`frontend/proxy.ts` (replaces deprecated middleware.ts):
- Optimistic cookie checks (no database queries)
- Route protection for `/admin/*` and `/trainer/*`
- Redirects unauthenticated users to `/login`
- Actual session validation happens in Server Components

### CSRF Protection
- **Package:** `csrf-csrf`
- **Token Generation:** `/api/csrf` endpoint
- **Validation:** Double-submit cookie pattern
- **Ignored Methods:** GET, HEAD, OPTIONS

### Data Validation
- **Backend:** FluentValidation (command pipeline)
- **Frontend:** Zod schemas (generated from OpenAPI)
- **Sync Mechanism:** OpenAPI spec includes validation metadata

### Case Conversion
- **Automatic:** Backend DTOs (PascalCase) → Frontend types (camelCase)
- **Implementation:** `convertKeysToCamelCase()` in `apiClient()`
- **Prevents:** Property mismatch errors between C# and TypeScript

---

## Trainer Features Architecture

### Trainer-Client Relationship Model

**Database Schema:**
```sql
CREATE TABLE trainer_client_relationships (
  id UUID PRIMARY KEY,
  trainer_id UUID REFERENCES users(id),
  client_id UUID REFERENCES users(id),
  status VARCHAR(20), -- pending, active, paused, ended
  can_view_nutrition BOOLEAN DEFAULT false,
  can_view_workouts BOOLEAN DEFAULT false,
  can_view_measurements BOOLEAN DEFAULT false,
  can_message BOOLEAN DEFAULT true,
  created_at TIMESTAMPTZ,
  updated_at TIMESTAMPTZ
);
```

**Relationship States:**
- `pending` - Client invitation sent, awaiting acceptance
- `active` - Active trainer-client relationship
- `paused` - Temporarily suspended (client can resume)
- `ended` - Permanently terminated

### Trainer Endpoints

**Client Management:**
- `GET /api/Trainers/clients` - List trainer's clients
- `GET /api/Trainers/requests` - List pending client requests
- `POST /api/Trainers/invitations` - Send client invitation
- `PUT /api/Trainers/clients/{clientId}/permissions` - Update permissions

**Client Data Access (permission-based):**
- `GET /api/Trainers/clients/{clientId}/nutrition?date={date}` - View client nutrition diary
- `GET /api/Trainers/clients/{clientId}/workouts` - View client workout logs
- `GET /api/Trainers/clients/{clientId}/measurements` - View client body measurements
- `POST /api/Trainers/clients/{clientId}/workouts` - Assign workout program

**Authorization:**
All trainer endpoints validate:
1. User has `trainer` or `admin` role
2. Active trainer-client relationship exists
3. Specific permission flag is enabled (e.g., `canViewNutrition`)

Example handler:
```csharp
public async Task<ClientNutritionDto> Handle(GetClientNutritionQuery request)
{
    // 1. Verify trainer authentication
    if (!_currentUser.UserId.HasValue)
        throw new UnauthorizedAccessException();

    // 2. Validate active relationship
    var relationship = await _context.TrainerClientRelationships
        .FirstOrDefaultAsync(r =>
            r.TrainerId == _currentUser.UserId &&
            r.ClientId == request.ClientId &&
            r.Status == "active");

    if (relationship == null)
        throw new UnauthorizedAccessException("No active relationship");

    // 3. Check specific permission
    if (!relationship.CanViewNutrition)
        throw new UnauthorizedAccessException("No nutrition viewing permission");

    // 4. Return data
    return await _context.FoodDiaryEntries
        .Where(e => e.UserId == request.ClientId && e.Date == request.Date)
        .ProjectToDto()
        .ToListAsync();
}
```

### Real-Time Communication (SignalR)

**Hubs:**
- `/hubs/chat` - ChatHub (trainer-client messaging)
- `/hubs/goals` - GoalHub (goal assignment notifications)
- `/hubs/notifications` - NotificationHub (system notifications)

**ChatHub Features:**
- One-to-one messaging between trainer and client
- Real-time message delivery
- Conversation history persistence
- Connection-based authorization

**Frontend Usage:**
```typescript
import { chatService } from "@/lib/services/signalr-chat";

// Connect to hub
await chatService.connect();

// Join conversation
await chatService.joinConversation(conversationId);

// Send message
await chatService.sendMessage(recipientId, "Hello!");

// Listen for messages
chatService.onMessageReceived((message) => {
  console.log(message);
});
```

**Backend Configuration:**
- SignalR uses Redis backplane for horizontal scaling
- Configured in `Program.cs` with `AddStackExchangeRedis`
- Authorization via `[Authorize]` attribute on hubs

**Security:**
- Hub methods validate user participation in conversation
- Only trainer or client can join their own conversation
- Messages are persisted to `chat_messages` table

---

## Caching Strategy

### Redis Cache Layers

**1. JWKS Cache**
- **TTL:** 1 minute
- **Fallback:** In-memory cache
- **Purpose:** Reduce calls to BetterAuth JWKS endpoint

**2. Ingredient Search Cache**
- **TTL:** 1 hour
- **Key Pattern:** `foods:search:{term}:{barcode}:{limit}`
- **Invalidation:** On food creation/update (via prefix removal)

**3. Recipe Cache (planned)**
- **TTL:** 5 minutes
- **Key Pattern:** `recipes:{id}` or `recipes:user:{userId}`
- **Invalidation:** On recipe update/delete

**4. Meal Plan Cache (planned)**
- **TTL:** 5 minutes
- **Key Pattern:** `mealplans:week:{userId}:{weekStart}`
- **Invalidation:** On meal plan changes

### Cache-Aside Pattern
1. Check Redis cache first
2. If miss, query PostgreSQL
3. Store result in Redis with TTL
4. Return data

---

## Backend Architecture (Clean Architecture)

### Layers

```
┌─────────────────────────────────────────┐
│          API Layer (Controllers)        │
│  - HTTP endpoints                       │
│  - Swagger/OpenAPI                      │
│  - JWT authentication                   │
│  - CORS configuration                   │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│      Application Layer (CQRS)           │
│  - Commands (write operations)          │
│  - Queries (read operations)            │
│  - DTOs                                 │
│  - FluentValidation (ValidationBehavior)│
│  - MediatR pipeline                     │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│        Domain Layer (Core)              │
│  - Entities (Food, Recipe, User, etc.)  │
│  - Value Objects                        │
│  - Domain Events                        │
│  - Business Rules                       │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│     Infrastructure Layer (I/O)          │
│  - EF Core (PostgreSQL)                 │
│  - Redis cache service                  │
│  - External APIs (OpenAI)               │
│  - Email service                        │
└─────────────────────────────────────────┘
```

### Key Patterns
- **CQRS:** Commands (CUD) and Queries (R) separated
- **MediatR Pipeline:** Validation → Logging → Handler execution
- **Repository Pattern:** Abstracted via `IMizanDbContext`
- **Dependency Injection:** Constructor injection throughout

---

## Frontend Architecture

### Directory Structure

```
frontend/
├── app/                    # Next.js App Router pages
│   ├── api/               # Next.js API routes
│   │   ├── auth/          # BetterAuth handlers
│   │   ├── csrf/          # CSRF token management
│   │   └── health/        # Health check
│   ├── profile/           # Profile page
│   ├── recipes/           # Recipe management
│   └── meals/             # Meal planning
├── components/            # Reusable React components
├── db/                    # Drizzle schema + client
│   ├── schema.ts          # BetterAuth tables
│   └── client.ts          # PostgreSQL connection
├── lib/                   # Utilities and services
│   ├── auth.ts            # BetterAuth configuration
│   ├── auth-client.ts     # Client-side auth + apiClient
│   ├── hooks/             # Custom React hooks
│   │   ├── useFormValidation.ts  # Zod validation hook
│   │   └── useCsrfToken.ts       # CSRF hook
│   ├── utils/             # Utility functions
│   │   └── case-converter.ts     # PascalCase ↔ camelCase
│   └── validations/       # Zod schemas
│       └── api.generated.ts   # Generated from OpenAPI
├── types/                 # TypeScript types
│   ├── api.generated.ts   # Generated from OpenAPI
│   └── ...                # Custom types
└── scripts/               # Code generation
    └── generate-zod-schemas.mjs
```

### Key Patterns
- **Server Components:** Default for pages (SSR)
- **Client Components:** Forms, interactive UI
- **Custom Hooks:** Validation, CSRF, session management
- **API Client:** Centralized fetch wrapper with JWT injection

---

## Code Generation Workflow

### Setup
```bash
npm run codegen
```

### Process
1. **Fetch OpenAPI Spec:** From `http://localhost:5000/swagger/v1/swagger.json`
2. **Generate Types:** `openapi-typescript` → `types/api.generated.ts`
3. **Generate Zod Schemas:** `openapi-zod-client` → `lib/validations/api.generated.ts`

### Usage

**TypeScript Types:**
```typescript
import type { FoodDto } from "@/types/api.generated";
```

**Zod Validation:**
```typescript
import { FoodDtoSchema } from "@/lib/validations/api.generated";
import { useFormValidation } from "@/lib/hooks/useFormValidation";

const { errors, validate } = useFormValidation(FoodDtoSchema);
```

---

## Testing Strategy

### Preference Order
**E2E (Playwright) > Integration (Vitest) > Unit (Vitest)**

### Coverage
- **E2E Tests:** User flows (login, recipe creation, meal planning)
- **Integration Tests:** API endpoints, database queries
- **Unit Tests:** Pure functions, complex domain logic

### Test Database
- **Name:** `mizan_test`
- **Isolation:** Separate from dev database
- **Reset:** Before each test run

---

## Deployment

### Docker Compose Services

```yaml
services:
  postgres:    # PostgreSQL 18
  redis:       # Redis 7
  frontend:    # Next.js (port 3000)
  backend:     # ASP.NET Core (port 5000 → 8080 internal)
  test:        # Backend tests (profile: test)
```

### Environment Variables

**Frontend:**
- `DATABASE_URL` - PostgreSQL connection (for BetterAuth)
- `BETTER_AUTH_SECRET` - JWT signing secret (ES256 private key)
- `BETTER_AUTH_URL` - Auth base URL
- `BETTER_AUTH_ISSUER` - JWT issuer identifier
- `BETTER_AUTH_AUDIENCE` - JWT audience identifier
- `API_URL` - Backend URL (server-side, e.g., `http://mizan-backend:8080`)
- `NEXT_PUBLIC_API_URL` - Backend URL (client-side, e.g., `http://localhost:5000`)
- `REDIS_URL` - Redis connection (optional, for session secondary storage)

**Backend:**
- `ConnectionStrings__PostgreSQL` - PostgreSQL connection string
- `ConnectionStrings__Redis` - Redis connection string
- `BetterAuth__JwksUrl` - JWKS endpoint from BetterAuth
- `BetterAuth__Issuer` - JWT issuer (must match frontend)
- `BetterAuth__Audience` - JWT audience (must match frontend)
- `ASPNETCORE_ENVIRONMENT` - Environment: Development, Staging, Production

---

## Production Deployment

### Docker Compose Deployment

**1. Pre-deployment Checklist**

```bash
# Verify all services are configured
✓ PostgreSQL 18 running with backups enabled
✓ Redis 7 configured for persistence
✓ Frontend environment variables set
✓ Backend environment variables set
✓ SSL certificates provisioned (Nginx)
✓ DNS records configured
```

**2. Database Migration**

```bash
# Run EF Core migrations on startup (automatic)
# MizanDbContext.Database.Migrate() runs in Program.cs

# Manual migration (if needed)
docker-compose exec backend dotnet ef database update \
  --project Mizan.Infrastructure --startup-project Mizan.Api
```

**3. Health Checks**

```bash
# Frontend health check
curl http://localhost:3000/api/health

# Backend health check
curl http://localhost:5000/health
```

**4. Monitoring & Logs**

```bash
# View logs
docker-compose logs -f [service-name]

# Check service status
docker-compose ps

# Inspect container
docker exec -it mizan-backend /bin/sh
```

### Scaling Considerations

**Horizontal Scaling (Multiple Instances):**
- Redis backplane required for SignalR (already configured)
- Load balancer (nginx, HAProxy) needed
- Session storage via Redis (not in-memory)

**Database:**
- Connection pooling configured in EF Core
- Read replicas for scaling queries
- Backup strategy: Daily snapshots to S3

**Caching:**
- Redis provides distributed cache
- JWKS cached for 1 minute
- Ingredient search cached for 1 hour

---

## Rate Limiting

Rate limiting is implemented using BetterAuth's rate limit plugin with custom per-path rules.

**Configuration (lib/auth.ts):**

```typescript
const rateLimit = {
  enabled: true,
  storage: hasRedis ? new RedisRateLimitStorage(client) : new DefaultRateLimitStorage(),
  customRules: [
    {
      pathPattern: "/api/auth/sign-up|/api/auth/sign-in|/api/auth/forgot-password/verify",
      window: 15 * 60 * 1000, // 15 minutes
      max: 5 // 5 attempts
    },
    {
      pathPattern: "/api/auth/send-magic-link|/api/auth/send-verification-email",
      window: 60 * 60 * 1000, // 1 hour
      max: 3 // 3 attempts
    },
    {
      pathPattern: "/api/auth/verify-email",
      window: 5 * 60 * 1000, // 5 minutes
      max: 10 // 10 attempts
    }
  ]
};
```

**Rate Limit Headers:**
All responses include:
- `X-RateLimit-Limit` - Maximum requests allowed
- `X-RateLimit-Remaining` - Requests remaining in current window
- `X-RateLimit-Reset` - Unix timestamp of window reset

**HTTP 429 Response:**
```json
{
  "error": "Too many requests",
  "retryAfter": 60
}
```

---

## Authentication Features

### BetterAuth Plugins

**1. Session Plugin**
- Session-based authentication with JWT tokens
- 7-day session expiry with 15-minute JWT expiry

**2. Magic Link Plugin**
- Email-based passwordless authentication
- 15-minute link expiry
- Automatic email notification on account access

**3. Email Notifications**
- Sign-in notifications with IP address and user agent
- Magic link emails with verification link
- Account verification emails

**4. Rate Limiting Plugin**
- Per-path custom rules
- Credential endpoint protection (5 attempts/15min)
- Magic link endpoint protection (3 attempts/hour)

**5. Admin Plugin**
- User impersonation
- Ban/unban functionality
- Role management

**6. Secondary Storage (Redis)**
- Persistent session storage in Redis
- Fallback to in-memory if Redis unavailable
- Cache-aside pattern with TTL

---

## Recent Feature Implementations (Feb 2026)

### Body Measurements Refactoring

**Schema Changes:**
- Split single `ArmsCm` and `ThighsCm` into left/right pairs
- New columns: `LeftArmCm`, `RightArmCm`, `LeftThighCm`, `RightThighCm`
- Migration strategy: Renamed existing columns, added left variants as NULL

**Visualization:**
- Tab-based interface: Body Composition | Circumference
- Body Composition: Dual Y-axis (kg on left, % on right)
- Circumference: Single Y-axis (cm) with grouped toggles (Core/Limbs)
- Real-time data filtering by time range (1M/3M/6M/1Y/ALL)

### Recipe Visibility & SEO

**Public Recipe Pages:**
- Recipe list page: `/recipes` (public, SEO-indexed)
- Recipe detail pages: `/recipes/[recipeId]` (public with Open Graph metadata)
- Dynamic metadata for Google indexing

**Access Control:**
- Unauthenticated users: View public recipes only
- Authenticated users: View own recipes + public recipes
- Admin: View all recipes

---

## Security Features

### Vulnerability Reporting

**Process:**
1. Report security issues privately: security@yourdomain.com
2. Do NOT create public issues for security vulnerabilities
3. Allow 48 hours for response before disclosure
4. Responsible disclosure appreciated

**Scope:**
- Authentication flaws
- Authorization bypasses
- Data exposure vulnerabilities
- Injection attacks
- Cross-site scripting (XSS)

**Out of Scope:**
- Email enumeration (feature design)
- Rate limiting (by design)
- Denial of service (infrastructure concern)

### Authentication Security

**Passwords:**
- Minimum 12 characters recommended
- No complexity requirements enforced (UX tradeoff)
- Hashed with bcrypt (BetterAuth default)

**Sessions:**
- Session ID rotated on each login
- Automatic cleanup of expired sessions
- IP binding optional (disabled by default)

**JWT Token Security:**
- Algorithm: ES256 (ECDSA P-256)
- Signed with private key
- Verified with JWKS
- Expiry: 15 minutes
- Issued by: BetterAuth
- Claims: user_id, email, role, iat, exp, iss, aud

---

## Monitoring & Health Checks

### Endpoints
- `/api/health` (Frontend) - Next.js health + DB connection
- `/health` (Backend) - ASP.NET health + PostgreSQL + Redis

### Health Checks
- **PostgreSQL:** Connection test + simple query
- **Redis:** Ping test
- **SignalR:** Hub connection test

---

## Migration Strategy

### Schema Changes

**Backend (EF Core):**
```bash
cd backend
dotnet ef migrations add MigrationName
dotnet ef database update
```

**Frontend (Drizzle):**
```bash
cd frontend
npm run db:generate
npm run db:migrate
```

**IMPORTANT:** Changes to shared tables (e.g., `households`) must be coordinated between both ORMs.

---

## Future Enhancements

### Short-term
- [ ] Schema drift detection CI script
- [ ] Add Redis caching to more queries (recipes, meal plans)
- [ ] Implement Redis task queue for AI suggestions (BullMQ)

### Long-term
- [ ] Consider GraphQL for flexible frontend queries
- [ ] Add observability (OpenTelemetry, Serilog → Seq)
- [ ] Implement event sourcing for audit trails
- [ ] Add CDC (Change Data Capture) for real-time sync

---

## Troubleshooting

### Common Issues

**Q: Frontend can't connect to backend**
A: Check `API_URL` env var. Server-side should use `http://mizan-backend:8080` (Docker network).

**Q: Authentication fails with 401**
A: Verify JWKS endpoint is accessible. Check Redis cache. Ensure JWT issuer/audience match.

**Q: Type mismatch errors**
A: Run `npm run codegen` to regenerate types/schemas from latest OpenAPI spec.

**Q: Ingredient dropdown not showing**
A: Check `.card` CSS has `overflow-visible` on ingredients card.

**Q: CSRF validation fails**
A: Ensure CSRF token is fetched before form submission. Check cookie configuration.

---

## Contacts & Resources

- **Architecture & Design:** `docs/ARCHITECTURE.md`
- **API Endpoints:** `docs/API_REFERENCE.md`
- **Getting Started:** `docs/DEVELOPER_ONBOARDING.md`
- **Development Philosophy:** `CLAUDE.md`
- **Swagger UI (Live):** `http://localhost:5000/swagger` (when backend running)
- **Database Schema:** `backend/Mizan.Infrastructure/Data/MizanDbContext.cs`
- **Frontend Auth Schema:** `frontend/db/schema.ts`
- **CI/CD Pipeline:** `.github/workflows/deploy.yml`
- **Security Issues:** security@yourdomain.com (private, no public issues)
