# Project Context (A.2.6 Context Slice)

## Slice: Grounding (Infrastructure)
> The physical/virtual environment where the code runs.
- **Platform:** Self-hosted (Docker Compose)
- **Region:** TBD (local/on-premise)
- **Storage:** PostgreSQL 18 (primary), Redis 7 (cache/backplane)
- **Deployment:** Docker containers with orchestration via docker-compose
- **Scale Target:** 1k-10k users

## Slice: Tech Stack (Software)
> The capabilities available to us.

### Frontend
- **Framework:** Next.js 16.0.8 (App Router)
- **Language:** TypeScript 5
- **UI Library:** React 19.2.3
- **Styling:** Tailwind CSS 3.4.1
- **Authentication:** BetterAuth 1.4.6 (JWT-based)
- **Database ORM:** Drizzle ORM 0.36.0
- **Real-time:** SignalR client (@microsoft/signalr 8.0.7)
- **Testing:** Vitest 2.1.6 + Playwright 1.49.0
- **Image Upload:** Cloudinary integration (next-cloudinary 6.16.0)

### Backend
- **Framework:** ASP.NET Core 10.0 (Web API)
- **Language:** C# (.NET 10)
- **Architecture:** Clean Architecture (Domain → Application → Infrastructure → API)
- **Database ORM:** Entity Framework Core 10.0
- **Authentication:** JWT Bearer tokens (Microsoft.AspNetCore.Authentication.JwtBearer 10.0.0)
- **Real-time:** SignalR with Redis backplane (StackExchange.Redis 2.8.16)
- **Logging:** Serilog 10.0.0
- **API Docs:** Swagger (Swashbuckle.AspNetCore 6.9.0)
- **Health Checks:** AspNetCore.HealthChecks (NpgSql + Redis)

### Infrastructure
- **Database:** PostgreSQL 18-alpine
- **Cache/Backplane:** Redis 7-alpine
- **Containerization:** Docker + Docker Compose
- **Development:** Hot reload enabled for both frontend and backend

## Slice: Constraints (Normative)
> The rules we cannot break.

- **Compliance:** None (no GDPR, HIPAA, or SOC2 requirements)
- **Budget:** < $100/month for infrastructure
- **Team:** Solo developer
- **Timeline:** Exploratory (no hard deadlines)
- **Deployment:** Self-hosted only (no cloud provider dependencies)

## Slice: Architecture Patterns
> How the system is structured.

### Backend Architecture
- **Domain Layer:** Core business entities and logic (pure domain models)
- **Application Layer:** Use cases, DTOs, interfaces
- **Infrastructure Layer:** EF Core, external services, persistence
- **API Layer:** Controllers, middleware, SignalR hubs

### Authentication Flow
- Frontend uses BetterAuth (JWT generation)
- Backend validates JWT via JWKS endpoint (`/api/auth/jwks`)
- Issuer: `http://localhost:3000`
- Audience: `mizan-api`

### Communication Patterns
- **Client → Frontend:** Browser connects to `http://localhost:3000`
- **Frontend → Backend (Server-side):** Next.js server uses `http://mizan-backend:8080` (Docker network)
- **Frontend → Backend (Client-side):** Browser uses `http://localhost:3000` (proxied via Next.js rewrites)
- **Real-time:** SignalR over WebSockets with Redis backplane for scaling

## Slice: Current State
> What exists now.

### Implemented Features
- Recipe management (create, search, filter, nutritional breakdown)
- Meal planning (weekly calendar, adjustable servings)
- Shopping list generation (auto-generated from meal plans)
- Nutrition tracking (daily/weekly summaries)
- Workout tracking (logging, history, analytics)
- Body composition tracking (metrics, trends, charts)
- Gamification (achievements, goals, badges)
- Social features (sharing, commenting, privacy controls)
- Trainer/coaching (role-based access, progress viewing, in-app chat)

### Git Status
- Current branch: `refactor.v2.3`
- Main branch: `master`
- Recent work: Ingredient name change handling, food retrieval by ID, JWT JWKS caching with Redis

## Slice: Development Workflow
> How development happens.

- **Version Control:** Git
- **Branch Strategy:** Feature branches off `master`
- **Local Development:** Docker Compose with hot reload
- **Testing Strategy:** E2E (Playwright) → Integration → Unit (Vitest)
- **Database Migrations:** EF Core migrations (backend), Drizzle migrations (frontend)
- **Test Database:** Separate `mizan_test` database for isolation
