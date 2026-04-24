# MacroChef

[![zread](https://img.shields.io/badge/Ask_Zread-_.svg?style=flat-square&color=00b0aa&labelColor=000000&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB3aWR0aD0iMTYiIGhlaWdodD0iMTYiIHZpZXdCb3g9IjAgMCAxNiAxNiIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPHBhdGggZD0iTTQuOTYxNTYgMS42MDAxSDIuMjQxNTZDMS44ODgxIDEuNjAwMSAxLjYwMTU2IDEuODg2NjQgMS42MDE1NiAyLjI0MDFWNC45NjAxQzEuNjAxNTYgNS4zMTM1NiAxLjg4ODEgNS42MDAxIDIuMjQxNTYgNS42MDAxSDQuOTYxNTZDNS4zMTUwMiA1LjYwMDEgNS42MDE1NiA1LjMxMzU2IDUuNjAxNTYgNC45NjAxVjIuMjQwMUM1LjYwMTU2IDEuODg2NjQgNS4zMTUwMiAxLjYwMDEgNC45NjE1NiAxLjYwMDFaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik00Ljk2MTU2IDEwLjM5OTlIMi4yNDE1NkMxLjg4ODEgMTAuMzk5OSAxLjYwMTU2IDEwLjY4NjQgMS42MDE1NiAxMS4wMzk5VjEzLjc1OTlDMS42MDE1NiAxNC4xMTM0IDEuODg4MSAxNC4zOTk5IDIuMjQxNTYgMTQuMzk5OUg0Ljk2MTU2QzUuMzE1MDIgMTQuMzk5OSA1LjYwMTU2IDE0LjExMzQgNS42MDE1NiAxMy43NTk5VjExLjAzOTlDNS42MDE1NiAxMC42ODY0IDUuMzE1MDIgMTAuMzk5OSA0Ljk2MTU2IDEwLjM5OTlaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik0xMy43NTg0IDEuNjAwMUgxMS4wMzg0QzEwLjY4NSAxLjYwMDEgMTAuMzk4NCAxLjg4NjY0IDEwLjM5ODQgMi4yNDAxVjQuOTYwMUMxMC4zOTg0IDUuMzEzNTYgMTAuNjg1IDUuNjAwMSAxMS4wMzg0IDUuNjAwMUgxMy43NTg0QzE0LjExMTkgNS42MDAxIDE0LjM5ODQgNS4zMTM1NiAxNC4zOTg0IDQuOTYwMVYyLjI0MDFDMTQuMzk4NCAxLjg4NjY0IDE0LjExMTkgMS42MDAxIDEzLjc1ODQgMS42MDAxWiIgZmlsbD0iI2ZmZiIvPgo8cGF0aCBkPSJNNCAxMkwxMiA0TDQgMTJaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik00IDEyTDEyIDQiIHN0cm9rZT0iI2ZmZiIgc3Ryb2tlLXdpZHRoPSIxLjUiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCIvPgo8L3N2Zz4K&logoColor=ffffff)](https://zread.ai/Euaell/macro_chef)

> **A modern, full-stack meal planning, nutrition tracking, and fitness application with trainer-client collaboration features**

MacroChef (internally known as "Mizan") is a comprehensive web application designed to help users plan meals, track nutrition, log workouts, and collaborate with fitness trainers. Built with a hybrid architecture separating frontend authentication from backend business logic, it offers a scalable, secure, and feature-rich platform for health and fitness management.

## Features

### Core Functionality

- **Meal Planning**: Interactive weekly calendar with drag-and-drop meal scheduling
- **Recipe Management**: Create, search, and store recipes with detailed nutritional breakdowns
- **Shopping Lists**: Auto-generate shopping lists from meal plans with ingredient grouping
- **Nutrition Tracking**: Log daily meals and track macros (calories, protein, carbs, fat, fiber)
- **Workout Logging**: Record exercises, sets, reps, and track progress over time
- **Body Composition**: Monitor weight, body fat %, muscle mass with visual charts

### Advanced Features

- **Gamification**: Earn achievements and badges for hitting nutrition and fitness goals
- **Social Sharing**: Share progress, achievements, and goals with the community (privacy controls available)
- **Trainer Collaboration**: Assign trainers to your account for personalized coaching
- **Real-time Chat**: In-app messaging between clients and trainers via SignalR
- **Goal Management**: Trainers can set and monitor client goals
- **Progress Analytics**: Comprehensive dashboards for tracking trends and performance

### Role-Based Access

- **User**: Full access to personal meal plans, nutrition logs, and workouts
- **Trainer**: Manage multiple clients, view their progress, set goals, and provide feedback
- **Admin**: User management, role assignments, and system configuration

## Tech Stack

### Frontend
- **Framework**: Next.js 16 (App Router) + React 19
- **Language**: TypeScript
- **Styling**: Tailwind CSS + shadcn/ui components
- **Package Manager**: Bun
- **Authentication**: BetterAuth (JWT with ES256)
- **ORM**: Drizzle (auth schema only)
- **Real-time**: SignalR client for chat and notifications

### Backend
- **Framework**: ASP.NET Core 10 (Web API)
- **Language**: C# (.NET 10)
- **Architecture**: Clean Architecture (CQRS with MediatR)
- **ORM**: Entity Framework Core 10
- **Validation**: FluentValidation
- **Real-time**: SignalR (with Redis backplane)
- **Caching**: Redis 7

### Infrastructure
- **Database**: PostgreSQL 18
- **Cache/Backplane**: Redis 7
- **Deployment**: Docker Compose (self-hosted)
- **Reverse Proxy**: Configured via Next.js rewrites

## Quick Start

### Prerequisites

- **Docker & Docker Compose** (recommended)
- **Bun** 1.0+ (for frontend local development)
- **.NET 10 SDK** (for backend local development)
- **PostgreSQL 18** (if running without Docker)
- **Redis 7** (if running without Docker)

### Installation with Docker Compose (Recommended)

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/macro_chef.git
   cd macro_chef
   ```

2. **Create environment file** (optional - uses defaults if not provided)
   ```bash
   cp .env.example .env
   # Edit .env with your preferred values
   ```

3. **Start all services**
   ```bash
   docker-compose up -d
   ```

4. **Access the application**
   - **Frontend**: http://localhost:3000
   - **Backend API**: http://localhost:5000
   - **Swagger UI**: http://localhost:5000/swagger
   - **PostgreSQL**: localhost:5432
   - **Redis**: localhost:6379

5. **View logs**
   ```bash
   docker-compose logs -f [frontend|backend|postgres|redis]
   ```

6. **Stop services**
   ```bash
   docker-compose down
   ```

### Running Locally (Without Docker)

#### Backend
```bash
cd backend

# Install dependencies (automatic with .NET)
dotnet restore

# Apply database migrations
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api

# Run the API
dotnet run --project Mizan.Api
```

#### Frontend
```bash
cd frontend

# Install dependencies
bun install

# Generate types from backend API
bun run codegen

# Run development server
bun run dev
```

**Note**: Ensure PostgreSQL and Redis are running and connection strings in `.env` are configured correctly.

## Documentation

Comprehensive documentation is available in the `docs/` directory:

- **[Architecture](docs/ARCHITECTURE.md)** - System design, schema boundaries, and architectural principles
- **[API Reference](docs/API_REFERENCE.md)** - Complete API endpoint documentation
- **[Developer Onboarding](docs/DEVELOPER_ONBOARDING.md)** - New contributor setup, workflows, and testing
- **[DTO Contracts](docs/DTO_CONTRACTS.md)** - Contract rules between backend DTOs and generated frontend types

## Development

### Backend Development

```bash
cd backend

# Build
dotnet build

# Run tests (use Docker for proper isolation)
docker-compose --profile test up test

# Run tests locally (fallback)
ConnectionStrings__PostgreSQL="Host=localhost;Database=mizan_test;Username=mizan;Password=mizan_dev_password" dotnet test

# Database migrations
dotnet ef migrations add MigrationName --project Mizan.Infrastructure --startup-project Mizan.Api
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api

# Format code
dotnet format
```

### Frontend Development

```bash
cd frontend

# Install dependencies
bun install

# Run dev server
bun run dev

# Build for production
bun run build
bun run start

# Lint
bun run lint

# Run tests
bun run test              # Unit/integration tests (Vitest)
bun run test:e2e          # E2E tests (Playwright)

# Database operations (Drizzle - auth schema only)
bun run db:generate       # Generate migrations
bun run db:migrate        # Apply migrations
bun run db:push           # Push schema without migrations
bun run db:studio         # Open Drizzle Studio

# Code generation from OpenAPI
bun run codegen           # Generate both types and Zod schemas
bun run codegen:types     # Generate TypeScript types only
bun run codegen:zod       # Generate Zod validation schemas only
```

**CRITICAL**: Always run `bun run codegen` after backend API/DTO changes to sync types and validation schemas.

### Running Tests

#### Backend Tests (Recommended: Docker)
```bash
# Run all tests in isolated environment
docker-compose --profile test up test

# Run with live output
docker-compose --profile test up --attach test

# Run specific test category
docker-compose run --rm test dotnet test --filter "Category=Integration"
```

#### Frontend Tests
```bash
cd frontend

# Unit/integration tests
bun run test

# E2E tests
bun run test:e2e
```

## Features by Role

### User Features
- Create and manage meal plans
- Log daily nutrition and workouts
- Track body composition changes
- Earn achievements and badges
- Assign a trainer for personalized coaching
- Real-time chat with assigned trainer
- Privacy controls for shared data

### Trainer Features
- Manage multiple client accounts
- View client progress (nutrition, workouts, body metrics)
- Set and track client goals
- Real-time chat with clients
- Provide personalized feedback and recommendations
- Review achievement progress

### Admin Features
- User management (create, edit, delete)
- Role assignments (User, Trainer, Admin)
- View system-wide statistics
- Manage trainer-client relationships
- System configuration

## Environment Variables

### Frontend (.env)
```bash
DATABASE_URL="postgresql://mizan:password@localhost:5432/mizan"
BETTER_AUTH_SECRET="your-secret-key-min-32-chars"
BETTER_AUTH_URL="http://localhost:3000"
BETTER_AUTH_ISSUER="http://localhost:3000"
BETTER_AUTH_AUDIENCE="mizan-api"
API_URL="http://mizan-backend:8080"              # Server-side (Docker network)
NEXT_PUBLIC_API_URL="http://localhost:5000"      # Client-side (direct backend)
```

### Backend (appsettings.json or env vars)
```bash
ConnectionStrings__PostgreSQL="Host=postgres;Database=mizan;Username=mizan;Password=password"
ConnectionStrings__Redis="redis:6379"
Jwt__Issuer="http://localhost:3000"
Jwt__Audience="mizan-api"
Jwt__JwksUrl="http://localhost:3000/api/auth/jwks"
```

See `.env.example` for complete list with descriptions.

## Architecture Overview

MacroChef uses a hybrid architecture with intentional schema separation:

- **Frontend Schema (Drizzle)**: Manages authentication tables (`users`, `sessions`) required by BetterAuth
- **Backend Schema (EF Core)**: Manages business logic tables (`foods`, `recipes`, `meal_plans`, `workouts`, etc.)
- **API Gateway**: Next.js serves auth routes and the UI; backend is called directly from the browser
- **Authentication**: BetterAuth issues JWTs and publishes JWKS for backend validation
- **Real-time**: SignalR with Redis backplane for horizontal scaling

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

**Key Patterns**: CQRS (MediatR), Repository Pattern, Dependency Injection, Functional Core / Imperative Shell

## Common Issues & Debugging

### Frontend can't connect to backend
- Check `API_URL` env var (should be `http://mizan-backend:8080` in Docker)
- Verify backend is running: `docker-compose logs backend`

### Authentication fails with 401
- Ensure JWT issuer/audience match in both services

### Type mismatch errors
- Run `bun run codegen` to regenerate from latest OpenAPI spec
- Verify backend is running (OpenAPI endpoint must be accessible)

### Tests failing with database errors
- Use `docker-compose --profile test up test` for proper isolation
- Test database is `mizan_test`, not `mizan`

## Contributing

This is a personal project, but contributions are welcome. Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Follow existing code conventions (check CLAUDE.md)
4. Run tests before committing
5. Submit a pull request

## License

This project is licensed under the MIT License. See LICENSE file for details.

---

**Built with** Next.js, React, ASP.NET Core, PostgreSQL, and Redis.
