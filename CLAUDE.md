# CLAUDE.md - MacroChef Developer Guidance

This file provides guidance to Claude Code and other LLM tools when working with the MacroChef codebase.

**Last Updated:** 2025-12-27
**Project:** MacroChef (Mizan) - Full-stack meal planning + nutrition tracking application

---

## Quick Reference

**New to the project?** Start here in order:
1. Read this file (5 min)
2. Read `docs/DEVELOPER_ONBOARDING.md` (15 min)
3. Run `docker-compose up -d` and verify services are healthy
4. Review `docs/ARCHITECTURE.md` to understand structure

---

## Communication Style

**Be a peer engineer, not a cheerleader:**

- Skip validation theater ("you're absolutely right", "excellent point")
- Be direct and technical - if something's wrong, say it
- Use dry, technical humor when appropriate
- Talk like you're pairing with a staff engineer, not pitching to a VP
- Challenge bad ideas respectfully - disagreement is valuable
- No emoji unless the user uses them first
- Precision over politeness - technical accuracy is respect

**Calibration phrases (use these, avoid alternatives):**

| USE | AVOID |
|-----|-------|
| "This won't work because..." | "Great idea, but..." |
| "The issue is..." | "I think maybe..." |
| "No." | "That's an interesting approach, however..." |
| "You're wrong about X, here's why..." | "I see your point, but..." |
| "I don't know" | "I'm not entirely sure but perhaps..." |
| "This is overengineered" | "This is quite comprehensive" |
| "Simpler approach:" | "One alternative might be..." |

## Project Overview

MacroChef (also referred to as "Mizan" internally) is a full-stack meal planning, nutrition tracking, and fitness application. **ASP.NET Core owns the entire database schema, identity included.** Next.js is a pure client: no ORM, no tables, no auth library. See `docs/REFOCUS.md` §6 for why the old Drizzle/BetterAuth split was removed.

**Tech Stack:**
- **Frontend:** Next.js 16 (App Router) + React 19 + TypeScript + Tailwind CSS + Bun
- **Backend:** ASP.NET Core 10 (Web API) + Clean Architecture + C#
- **Database:** PostgreSQL 18
- **Cache:** Redis 7 (SignalR backplane + application caching)
- **Authentication:** backend-issued opaque session cookies (`mizan_session`), password hashing via `PasswordHasher<T>`, Google + GitHub OAuth
- **Real-time:** SignalR (for trainer-client chat and notifications)
- **Deployment:** Docker Compose (self-hosted)

## Essential Commands

### Docker Compose (Recommended Workflow)

```bash
# Start all services (frontend, backend, postgres, redis)
docker-compose up -d

# View logs
docker-compose logs -f [frontend|backend|postgres|redis]

# Stop all services
docker-compose down

# Rebuild after dependency changes
docker-compose up -d --build [frontend|backend]

# Access running services:
# - Frontend: http://localhost:3000
# - Backend API: http://localhost:5000
# - Swagger UI: http://localhost:5000/swagger
# - PostgreSQL: localhost:5432
# - Redis: localhost:6379
```

### Backend (.NET)

```bash
cd backend

# Build
dotnet build

# Run locally (requires PostgreSQL + Redis)
dotnet run --project Mizan.Api

# Run tests (use Docker for proper test isolation)
docker-compose --profile test up test

# Run tests locally (fallback - use docker-compose preferably)
ConnectionStrings__PostgreSQL="Host=localhost;Database=mizan_test;Username=mizan;Password=mizan_dev_password" dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"

# Database migrations (EF Core)
dotnet ef migrations add MigrationName --project Mizan.Infrastructure --startup-project Mizan.Api
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api

# Format code
dotnet format
```

### Frontend (Next.js + Bun)

```bash
cd frontend

# Install dependencies
bun install

# Run dev server (requires backend running)
bun run dev

# Build for production
bun run build

# Start production build
bun run start

# Lint
bun run lint

# Run unit/integration tests (Vitest)
bun run test

# Run E2E tests (Playwright)
bun run test:e2e

# Code generation from OpenAPI
bun run codegen              # Generate TypeScript API types
```

**CRITICAL:** Always run `bun run codegen` after backend API/DTO changes to sync frontend API types.

## Thinking Principles

When reasoning through problems, apply these principles:

**Separation of Concerns:**
- What's Core (pure logic, calculations, transformations)?
- What's Shell (I/O, external services, side effects)?
- Are these mixed? They shouldn't be.

**Weakest Link Analysis:**
- What will break first in this design?
- What's the least reliable component?
- System reliability ≤ min(component reliabilities)

**Explicit Over Hidden:**
- Are failure modes visible or buried?
- Can this be tested without mocking half the world?
- Would a new team member understand the flow?

**Reversibility Check:**
- Can we undo this decision in 2 weeks?
- What's the cost of being wrong?
- Are we painting ourselves into a corner?

## Decision Framework

For a single, reversible decision that doesn't need a persistent evidence
trail, work through this with the user rather than picking silently:

```
DECISION:      what we're deciding
CONTEXT:       why now, what triggered it

OPTIONS:
  1. [A]   + pros   - cons
  2. [B]   + pros   - cons

WEAKEST LINK:  what breaks first in each option
REVERSIBILITY: can we undo in 2 weeks? 2 months? never?
RECOMMENDATION: which + why, or "need your input on X"
```

## Task Execution Workflow

### 1. Understand the Problem Deeply
- Read carefully, think critically, break into manageable parts
- Consider: expected behavior, edge cases, pitfalls, larger context, dependencies
- For URLs provided: fetch immediately and follow relevant links

### 2. Investigate the Codebase
- **Check `docs/REFOCUS.md` first**, the product thesis and what is being cut
- **Check `docs/ARCHITECTURE.md`**, structure and layer boundaries
- Use Task tool for broader/multi-file exploration (preferred for context efficiency)
- Explore relevant files and directories
- Search for key functions, classes, variables
- Identify root cause
- Continuously validate and update understanding

### 3. Research (When Needed)
- Knowledge may be outdated (cutoff: January 2025)
- When using third-party packages/libraries/frameworks, verify current usage patterns
- **Use available MCP tools** for up-to-date documentation (see MCP Tools section)
- Don't rely on summaries - fetch actual content

### 4. Plan the Solution
- Create clear, step-by-step plan using TodoWrite
- **For significant changes: use Decision Framework or FPF Mode**
- Break fix into manageable, incremental steps
- Each step should be specific, simple, and verifiable
- Actually execute each step (don't just say "I will do X" - DO X)

### 5. Implement Changes
- Before editing, read relevant file contents for complete context
- Make small, testable, incremental changes
- Follow existing code conventions (check neighboring files, package.json, etc.)

### 6. Debug
- Make changes only with high confidence
- Determine root cause, not symptoms
- Use print statements, logs, temporary code to inspect state
- Revisit assumptions if unexpected behavior occurs

### 7. Test & Verify
- Test frequently after each change
- Run lint and typecheck commands if available
- Run existing tests
- Verify all edge cases are handled

### 8. Complete & Reflect
- Mark all todos as completed
- After tests pass, think about original intent
- Ensure solution addresses the root cause
- Never commit unless explicitly asked

## Architecture Overview

### Clean Architecture Layers (Backend)

```
Mizan.Api (Presentation)
  ↓ Controllers, SignalR Hubs, Middleware
Mizan.Application (Use Cases)
  ↓ Commands (write), Queries (read), DTOs, Validation
Mizan.Domain (Core Business Logic)
  ↓ Entities, Value Objects, Domain Events
Mizan.Infrastructure (External Concerns)
  ↓ EF Core, Redis, External APIs
```

**Key Patterns:**
- **CQRS:** Commands and Queries separated via MediatR
- **Pipeline Behaviors:** Validation (FluentValidation) → Logging → Handler
- **Repository Pattern:** Abstracted via `IMizanDbContext` interface
- **Dependency Injection:** Constructor injection throughout
- **Functional Core, Imperative Shell:** Pure business logic in Domain/Application, I/O in Infrastructure/API

### Frontend Structure (Next.js App Router)

```
frontend/
├── app/                      # Pages and routes
│   ├── (auth)/              # Auth routes (login, signup)
│   ├── (dashboard)/         # Protected dashboard routes
│   ├── admin/               # Admin-only routes
│   └── api/                 # Next.js API routes
│       ├── csrf/            # CSRF token management
│       └── health/          # Health check
├── components/              # Reusable UI components (shadcn/ui)
├── lib/                     # Services and utilities
│   ├── auth.ts              # Server-side session read (getCurrentUser)
│   ├── auth-client.ts       # Client-side auth calls against /api/Auth
│   ├── hooks/               # React hooks
│   ├── services/            # SignalR, etc.
│   └── utils/               # Utility functions
├── types/                   # TypeScript types
│   └── api.generated.ts     # Generated from OpenAPI
└── scripts/                 # Code generation scripts
```

### Schema Boundaries (Critical Concept)

**One schema, one owner: EF Core.**

`users`, `user_sessions`, `user_tokens` and `external_logins` sit alongside
`foods`, `recipes`, `workouts` and the rest, in a single `InitialCreate`
migration. There is no second ORM and no shared-table coordination problem.

**Migrations:** `dotnet ef migrations add <Name> --project Mizan.Infrastructure --startup-project Mizan.Api`

**CRITICAL:** run `dotnet ef migrations has-pending-model-changes` before any
push that touches an entity or `MizanDbContext` - a divergence between the model
and the migrations fails `MigrateAsync` at startup and takes every integration
test with it.

---

## API Routing and Proxying

### Next.js Handles Directly
- `/api/health` - Frontend health check
- `/api/csrf` - CSRF token management

### Direct Backend Calls (via `api.mizan.euaell.me` subdomain)
Client-side API calls go directly to the backend via a separate API subdomain with CORS:
- `/api/Users/*`, `/api/Foods/*`, `/api/Recipes/*`, `/api/MealPlans/*`
- `/api/Workouts/*`, `/api/Exercises/*`, `/api/BodyMeasurements/*`
- `/api/Achievements/*`, `/api/Households/*`, `/api/Trainers/*`, `/api/Chat/*`
- `/hubs/*` - SignalR hubs

**Network Topology:**
- **Browser → Frontend:** `https://mizan.euaell.me` (pages, auth, SSR)
- **Browser → Backend:** `https://api.mizan.euaell.me` (client-side API calls, CORS-enabled)
- **Frontend → Backend (server-side):** `http://mizan-backend:8080` (Docker network, no CORS needed)
- **Nginx** terminates SSL and routes `mizan.euaell.me` → frontend, `api.mizan.euaell.me` → backend

## Authentication Flow

1. User signs in at `POST /api/Auth/login` (backend).
2. The backend verifies the password, creates a row in `user_sessions`, and sets
   `mizan_session` - httpOnly, SameSite=Lax, `Domain=.euaell.me` in production.
3. Every later request carries the cookie: the browser sends it to both origins
   because they are same-site, and Next.js server components forward it.
4. `SessionCookieAuthenticationHandler` resolves the token against
   `user_sessions` (HybridCache in front) and then checks `IUserStatusService`
   for deleted, unverified and banned.

**Security notes:**
- Sessions: 7-day sliding expiry, revoke = delete, effective on the next request.
- Passwords: `PasswordHasher<T>` (PBKDF2-HMAC-SHA512), 10-character minimum,
  lockout after 5 failures for 15 minutes.
- Mailed links: 32 random bytes, stored as SHA-256, single use. 24h to confirm
  an email, 1h to reset a password.
- The MCP server is unaffected: it authenticates with `X-Api-Key` plus
  `X-Impersonate-User`, and never held a JWT.

---

## Type Safety and Validation

### Code Generation Flow
```
Backend (C# DTOs + FluentValidation)
    ↓
OpenAPI Spec (with validation metadata)
    ↓
┌─────────────────┬──────────────────┐
│                 │                  │
TypeScript Types  Zod Schemas     (bun run codegen)
    ↓                 ↓
Frontend Types    Form Validation
```

**Usage:**
```typescript
// Import generated types
import type { FoodDto } from "@/types/api.generated";

// Import generated Zod schemas
import { FoodDtoSchema } from "@/lib/validations/api.generated";
import { useFormValidation } from "@/lib/hooks/useFormValidation";

const { errors, validate } = useFormValidation(FoodDtoSchema);
```

**Case Conversion:** Backend DTOs use PascalCase, frontend automatically converts to camelCase via `apiClient()`.

## Testing Philosophy

**Preference order:** E2E → Integration → Unit

| Type | When | ROI |
|------|------|-----|
| E2E | Test what users see | Highest value, highest cost |
| Integration | Test module boundaries | Good balance |
| Unit | Complex pure functions with many edge cases | Low cost, limited value |

**Test contracts, not implementation:**
- If function signature is the contract → test the contract
- Public interfaces and use cases only
- Never test internal/private functions directly

**Never test:**
- Private methods
- Implementation details
- Mocks of things you own
- Getters/setters
- Framework code

**The rule:** If refactoring internals breaks your tests but behavior is unchanged, your tests are bad.

### Backend Tests
- **Location:** `backend/Mizan.Tests/`
- **Framework:** xUnit + FluentAssertions + Moq
- **Integration Tests:** Use Testcontainers for PostgreSQL
- **Run:** `docker-compose --profile test up test` (recommended)

### Frontend Tests
- **Unit/Integration:** Vitest + Testing Library → `bun run test`
- **E2E:** Playwright → `bun run test:e2e`

## SignalR Real-Time Features

**Hubs:**
- `/hubs/chat` - ChatHub (trainer-client messaging)
- `/hubs/goals` - GoalHub (goal assignments)
- `/hubs/notifications` - NotificationHub (real-time notifications)

**Frontend Service:**
```typescript
import { chatService } from "@/lib/services/signalr-chat";

await chatService.connect();
chatService.onMessageReceived(callback);
await chatService.sendMessage(recipientId, message);
```

**Backend Configuration:**
- SignalR uses Redis backplane for horizontal scaling
- Configured in `Program.cs` with `AddStackExchangeRedis`

## Caching Strategy

Redis cache layers:
1. **JWKS Cache:** 1-minute TTL (auth validation)
2. **Ingredient Search:** 1-hour TTL, invalidated on food updates
3. **Recipe Cache:** (planned) 5-minute TTL
4. **Meal Plan Cache:** (planned) 5-minute TTL

**Pattern:** Cache-aside (check cache → query DB → store in cache)

## MCP Tools (Available in This Project)

### Microsoft Docs MCP
Use for .NET, ASP.NET Core, Entity Framework Core documentation:

```typescript
// Search for .NET/Azure documentation
mcp__microsoft_docs_mcp__microsoft_docs_search

// Fetch complete documentation page
mcp__microsoft_docs_mcp__microsoft_docs_fetch

// Search for code samples
mcp__microsoft_docs_mcp__microsoft_code_sample_search
```

**When to use:** Researching .NET 10, EF Core 10, or ASP.NET Core 10 features.

### Context7 MCP
Use for library/framework documentation (Next.js, React, etc.):

```typescript
// Resolve library ID
mcp__plugin_context7_context7__resolve-library-id
mcp__io_github_upstash_context7__resolve-library-id

// Get library documentation
mcp__plugin_context7_context7__get-library-docs
mcp__io_github_upstash_context7__get-library-docs
```

**When to use:** API references, usage patterns, migration guides for npm packages.

### Next.js DevTools MCP
Use for Next.js development and debugging:

```typescript
// Initialize Next.js DevTools context
mcp__next-devtools__init

// Search Next.js documentation
mcp__next-devtools__nextjs_docs

// Query running dev server
mcp__next-devtools__nextjs_index
mcp__next-devtools__nextjs_call

// Browser automation for testing
mcp__next-devtools__browser_eval
```

**When to use:**
- Before implementing Next.js features (always call `init` first)
- Debugging Next.js runtime issues
- Testing pages with browser automation (prefer this over curl for Next.js pages)

### shadcn/ui MCP
Use for UI component development:

```typescript
// Get configured registries
mcp__shadcn__get_project_registries

// Search for components
mcp__shadcn__search_items_in_registries

// View component details
mcp__shadcn__view_items_in_registries

// Get usage examples
mcp__shadcn__get_item_examples_from_registries

// Get CLI add command
mcp__shadcn__get_add_command_for_items
```

**When to use:** Adding or modifying shadcn/ui components.

### Docker MCP
Use for Docker Hub research and container management:

```typescript
// Search Docker Hub
mcp__MCP_DOCKER__search

// Get repository info
mcp__MCP_DOCKER__getRepositoryInfo

// List tags
mcp__MCP_DOCKER__listRepositoryTags
```

**When to use:** Researching base images for Dockerfiles.

## Code Generation Guidelines

### Architecture: Functional Core, Imperative Shell
- Pure functions (no side effects) → core business logic
- Side effects (I/O, state, external APIs) → isolated shell modules
- Clear separation: core never calls shell, shell orchestrates core

### Functional Paradigm
- **Immutability**: use immutable types, avoid implicit mutation, return new instances
- **Pure functions**: deterministic, no hidden dependencies
- **No exotic constructs**: stick to language idioms unless monads are native

### Error Handling: Explicit Over Hidden
- Never swallow errors silently (empty catch blocks are bugs)
- Handle exceptions at boundaries, not deep in call stack
- Return error values when codebase uses them (Result, Option, error tuples)
- If codebase uses exceptions, use exceptions consistently, but explicitly
- Fail fast for programmer errors, handle gracefully for expected failures
- Keep execution flow deterministic and linear

### Code Quality
- Self-documenting code for simple logic
- Comments only for complex invariants and business logic (explain WHY not WHAT)
- Keep functions small and focused (<25 lines as guideline)
- Avoid high cyclomatic complexity
- No deeply nested conditions (max 2 levels)
- No loops nested in loops, extract inner loop
- Extract complex conditions into named functions

### Code Style
- DO NOT ADD COMMENTS unless asked
- Follow existing codebase conventions
- Check what libraries/frameworks are already in use
- Mimic existing code style, naming conventions, typing
- Never assume a non-standard library is available
- Never expose or log secrets and keys

## Common Workflows

### Adding a New API Endpoint

1. **Backend:**
   - Add entity to `Mizan.Domain/Entities/`
   - Create Command/Query in `Mizan.Application/Commands/` or `Queries/`
   - Add validator using FluentValidation
   - Create controller in `Mizan.Api/Controllers/`
   - Update `MizanDbContext.cs` if new entity

2. **Database Migration:**
   ```bash
   cd backend
   dotnet ef migrations add AddMyEntity --project Mizan.Infrastructure --startup-project Mizan.Api
   dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api
   ```

3. **Frontend:**
   ```bash
   cd frontend
   bun run codegen  # Generate types and Zod schemas
   ```

4. **Update `next.config.ts`:** Add proxy rewrite if new API namespace

### Adding a shadcn/ui Component

1. **Search for the component:**
   ```typescript
   mcp__shadcn__search_items_in_registries
   ```

2. **Get the add command:**
   ```typescript
   mcp__shadcn__get_add_command_for_items
   ```

3. **Run the command:**
   ```bash
   cd frontend
   bunx shadcn@latest add [component-name]
   ```

### Debugging Common Issues

**Frontend can't connect to backend:**
- Check `API_URL` env var (should be `http://mizan-backend:8080` in Docker)
- Verify backend is running: `docker-compose logs backend`

**Authentication fails with 401:**
- Verify JWKS endpoint accessible: `curl http://localhost:3000/api/auth/jwks`
- Check Redis cache: `docker exec -it mizan-redis redis-cli KEYS "jwks:*"`
- Ensure JWT issuer/audience match in both services

**Type mismatch errors:**
- Run `bun run codegen` to regenerate from latest OpenAPI spec
- Verify backend is running (OpenAPI endpoint must be accessible)

**Tests failing with database errors:**
- Use `docker-compose --profile test up test` for proper isolation
- Test database is `mizan_test`, not `mizan`

**Next.js page verification failing:**
- Use `mcp__next-devtools__browser_eval` instead of curl
- Browser automation actually renders the page and executes JavaScript
- Detects runtime errors, hydration issues, and client-side problems
- Always prefer browser automation for Next.js page testing

## File Locations Reference

### Backend
- **Controllers:** `backend/Mizan.Api/Controllers/`
- **SignalR Hubs:** `backend/Mizan.Api/Hubs/`
- **Commands:** `backend/Mizan.Application/Commands/`
- **Queries:** `backend/Mizan.Application/Queries/`
- **Entities:** `backend/Mizan.Domain/Entities/`
- **DbContext:** `backend/Mizan.Infrastructure/Data/MizanDbContext.cs`
- **Migrations:** `backend/Mizan.Infrastructure/Migrations/`
- **Shared wire types:** `backend/Mizan.Contracts/` - dependency-free records the
  API binds and the MCP server (and later the Telegram bot) constructs. Add a
  request shape here, not in a controller, when more than one service sends it.

### Frontend
- **Pages:** `frontend/app/`
- **Components:** `frontend/components/` (shadcn/ui)
- **Auth Client:** `frontend/lib/auth-client.ts`
- **Generated Types:** `frontend/types/api.generated.ts`
- **Generated Schemas:** `frontend/lib/validations/api.generated.ts`
- **SignalR Services:** `frontend/lib/services/`

## Documentation Files

- `README.md` - Getting started and deployment
- `docs/ARCHITECTURE.md` - Comprehensive architecture documentation
- Swagger UI (`http://localhost:5000/swagger`) - the API reference, generated from the backend
- `docs/DEVELOPER_ONBOARDING.md` - New-contributor setup, workflows, and testing
- `docs/REFOCUS.md` - Product thesis and reorganization roadmap

## Environment Variables

See `.env.example` for complete list. Key variables:

**Frontend:**
- `API_URL` - Backend URL (server-side, use Docker network name)
- `NEXT_PUBLIC_API_URL` - Backend URL (client-side, use localhost)

**Backend:**
- `ConnectionStrings__PostgreSQL` - PostgreSQL connection
- `ConnectionStrings__Redis` - Redis connection
- `App__PublicUrl` - where the web app lives; mailed links point here
- `App__CookieDomain` - parent domain shared by app and API (empty on localhost)
- `Smtp__*` - outbound email
- `Ai__BaseUrl`, `Ai__ApiKey`, `Ai__Model` - any OpenAI-compatible endpoint;
  empty disables the assistant and the API still starts
- `Ai__GlobalDailyTokens`, `Ai__GlobalDailyCostMicros` - the circuit breaker on
  the provider bill. Never raise these without looking at `/api/Ai/usage/global`
- `Storage__*` - S3-compatible object storage (MinIO or Cloudflare R2)
- `Authentication__Google__*`, `Authentication__GitHub__*` - OAuth

## Critical Reminders

1. **Use TodoWrite** - For ANY multi-step task, mark complete IMMEDIATELY
2. **Actually Do Work** - When you say "I will do X", DO X
3. **No Commits Without Permission** - Only commit when explicitly asked
4. **Test Contracts** - Test behavior through public interfaces, not implementation
5. **Follow Architecture** - Functional core (pure), imperative shell (I/O)
6. **No Silent Failures** - Empty catch blocks are bugs
7. **Be Direct** - "No" is a complete sentence. Disagree when you should.
8. **Always run `bun run codegen`** after backend API/DTO changes
9. **Use Docker Compose** for testing to ensure proper isolation
10. **One schema, owned by EF Core** - the frontend has no database access
11. **Case conversion is automatic** - don't manually convert PascalCase/camelCase
12. **Never call an AI provider outside `IAiQuotaService`** - reserve, call,
    settle in a `finally`. An unmetered call is a bill nobody sees coming
13. **Never read personal data for the AI without `IDataAccessPolicy`** - ask it
    which axes you may use and take only those; do not fetch everything and filter
12. **Use MCP tools** - Microsoft Docs for .NET, Context7 for npm packages, Next.js DevTools for debugging
