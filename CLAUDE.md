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

**Something broken?** Check `docs/TROUBLESHOOTING.md` first (90% of issues covered).

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

MacroChef (also referred to as "Mizan" internally) is a full-stack meal planning, nutrition tracking, and fitness application. The codebase uses a hybrid architecture with intentional schema separation between frontend authentication (BetterAuth + Drizzle) and backend business logic (Clean Architecture + EF Core).

**Tech Stack:**
- **Frontend:** Next.js 16 (App Router) + React 19 + TypeScript + Tailwind CSS + Bun
- **Backend:** ASP.NET Core 10 (Web API) + Clean Architecture + C#
- **Database:** PostgreSQL 18
- **Cache:** Redis 7 (SignalR backplane + application caching)
- **Authentication:** BetterAuth (JWT-based, ES256)
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

# Database operations (Drizzle - auth schema only)
bun run db:generate   # Generate migrations
bun run db:migrate    # Apply migrations
bun run db:push       # Push schema without migrations
bun run db:studio     # Open Drizzle Studio

# Code generation from OpenAPI
bun run codegen              # Generate both types and Zod schemas
bun run codegen:types        # Generate TypeScript types only
bun run codegen:zod          # Generate Zod validation schemas only
```

**CRITICAL:** Always run `bun run codegen` after backend API/DTO changes to sync types and validation schemas.

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

## Task Execution Workflow

### 1. Understand the Problem Deeply
- Read carefully, think critically, break into manageable parts
- Consider: expected behavior, edge cases, pitfalls, larger context, dependencies
- For URLs provided: fetch immediately and follow relevant links

### 2. Investigate the Codebase
- **Check `.fpf/context.md` first** — Project context, constraints, and tech stack
- **Check `.fpf/knowledge/`** — Project knowledge base with verified claims
- **Check `docs/` directory** — Comprehensive documentation (architecture, API, deployment, testing)
- **Check `docs/context/` directory** — Design decisions and access control documentation
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
│       ├── auth/            # BetterAuth endpoints
│       ├── csrf/            # CSRF token management
│       └── health/          # Health check
├── components/              # Reusable UI components (shadcn/ui)
├── db/                      # Drizzle schema (auth tables only)
│   ├── schema.ts
│   └── client.ts
├── lib/                     # Services and utilities
│   ├── auth.ts              # BetterAuth server config
│   ├── auth-client.ts       # Client-side auth + apiClient
│   ├── hooks/               # React hooks
│   ├── services/            # SignalR, etc.
│   └── utils/               # Utility functions
├── types/                   # TypeScript types
│   └── api.generated.ts     # Generated from OpenAPI
└── scripts/                 # Code generation scripts
```

### Schema Boundaries (Critical Concept)

**Frontend Schema (Drizzle ORM):**
- **Owner:** BetterAuth
- **Tables:** `users`, `accounts`, `sessions`, `jwks`, `verification`
- **Why:** BetterAuth requires Drizzle for auth flows
- **Migrations:** `bun run db:generate` → `bun run db:migrate`

**Backend Schema (EF Core):**
- **Owner:** Business logic
- **Tables:** `foods`, `recipes`, `meal_plans`, `workouts`, `achievements`, `trainers`, etc.
- **Why:** Complex domain logic best expressed in C# with EF Core
- **Migrations:** `dotnet ef migrations add` → `dotnet ef database update`

**Shared Table:** `households` - Backend is source of truth, frontend references via user associations.

**CRITICAL:** Changes to shared tables must be coordinated between both ORMs. This separation is intentional - don't try to unify them.

## API Routing and Proxying

### Next.js Handles Directly
- `/api/auth/*` - BetterAuth endpoints
- `/api/health` - Frontend health check
- `/api/csrf` - CSRF token management

### Proxied to Backend (via next.config.ts rewrites)
All other `/api/*` routes are proxied to the backend:
- `/api/Users/*`, `/api/Foods/*`, `/api/Recipes/*`, `/api/MealPlans/*`
- `/api/Workouts/*`, `/api/Exercises/*`, `/api/BodyMeasurements/*`
- `/api/Achievements/*`, `/api/Households/*`, `/api/Trainers/*`, `/api/Chat/*`
- `/hubs/*` - SignalR hubs

**Network Topology:**
- **Browser → Frontend:** `http://localhost:3000` (client-side)
- **Frontend → Backend (server-side):** `http://mizan-backend:8080` (Docker network)
- **Frontend → Backend (client-side):** `http://localhost:3000` (proxied via rewrites)

## Authentication Flow

1. User logs in → BetterAuth (Next.js)
2. BetterAuth creates session + JWT (ES256, 15min expiry)
3. JWT stored in httpOnly cookie
4. API requests include JWT in Authorization header
5. Backend validates JWT using JWKS from BetterAuth endpoint
6. JWKS cached in Redis (1-minute TTL) to reduce calls

**Security Features:**
- JWT Algorithm: ES256 (ECDSA P-256)
- Token Expiry: 15 minutes (JWT), 7 days (session)
- Cookie: httpOnly, sameSite: "lax", secure (production)
- CSRF Protection: Double-submit cookie pattern via `csrf-csrf`

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

See `docs/SIGNALR_IMPLEMENTATION.md` for detailed implementation guide.

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
Use for library/framework documentation (Next.js, React, Drizzle, etc.):

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

### Error Handling: Explicit Over Hidden
- Never swallow errors silently (empty catch blocks are bugs)
- Handle exceptions at boundaries, not deep in call stack
- Return error values when codebase uses them (Result, Option, error tuples)
- If codebase uses exceptions — use exceptions consistently, but explicitly
- Fail fast for programmer errors, handle gracefully for expected failures
- Keep execution flow deterministic and linear

### Code Quality
- Self-documenting code for simple logic
- Comments only for complex invariants and business logic (explain WHY not WHAT)
- Keep functions small and focused (<25 lines as guideline)
- Avoid high cyclomatic complexity
- No deeply nested conditions (max 2 levels)
- No loops nested in loops — extract inner loop
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

### Frontend
- **Pages:** `frontend/app/`
- **Components:** `frontend/components/` (shadcn/ui)
- **Auth Schema:** `frontend/db/schema.ts`
- **Auth Client:** `frontend/lib/auth-client.ts`
- **Generated Types:** `frontend/types/api.generated.ts`
- **Generated Schemas:** `frontend/lib/validations/api.generated.ts`
- **SignalR Services:** `frontend/lib/services/`

## Documentation Files

- `README.md` - Getting started and deployment
- `docs/ARCHITECTURE.md` - Comprehensive architecture documentation
- `docs/API_REFERENCE.md` - Complete API endpoint documentation
- `docs/DEPLOYMENT_GUIDE.md` - Production deployment instructions
- `docs/TESTING_GUIDE.md` - Testing strategy and how to run tests
- `docs/SIGNALR_IMPLEMENTATION.md` - SignalR setup and usage guide
- `docs/SECURITY_ANALYSIS.md` - Security audit and authorization patterns
- `docs/SECURITY.md` - Security policies and vulnerability reporting
- `docs/IMPLEMENTATION_2025_12_27.md` - December 2025 feature implementations (recipe visibility, trainer discovery)
- `docs/context/` - Design decisions and access control documentation
- `.fpf/` - First Principles Framework knowledge base (if initialized)

## Environment Variables

See `.env.example` for complete list. Key variables:

**Frontend:**
- `DATABASE_URL` - PostgreSQL (for BetterAuth)
- `BETTER_AUTH_SECRET` - JWT signing secret
- `API_URL` - Backend URL (server-side, use Docker network name)
- `NEXT_PUBLIC_API_URL` - Backend URL (client-side, use localhost)

**Backend:**
- `ConnectionStrings__PostgreSQL` - PostgreSQL connection
- `ConnectionStrings__Redis` - Redis connection
- `BetterAuth__JwksUrl` - JWKS endpoint from frontend
- `BetterAuth__Issuer` - JWT issuer
- `BetterAuth__Audience` - JWT audience

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
10. **Schema separation is intentional** - don't try to unify Drizzle and EF Core
11. **Case conversion is automatic** - don't manually convert PascalCase/camelCase
12. **Use MCP tools** - Microsoft Docs for .NET, Context7 for npm packages, Next.js DevTools for debugging
