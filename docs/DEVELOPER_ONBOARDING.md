# MacroChef Developer Onboarding Guide

**Version:** 1.0
**Last Updated:** 2025-12-27
**Target Audience:** New developers joining MacroChef project

---

## Table of Contents

- [Welcome](#welcome)
- [Project Overview](#project-overview)
- [Development Environment Setup](#development-environment-setup)
- [First Build](#first-build)
- [Code Structure and Organization](#code-structure-and-organization)
- [Development Workflow](#development-workflow)
- [Common Development Tasks](#common-development-tasks)
- [Code Style and Conventions](#code-style-and-conventions)
- [Getting Help](#getting-help)

---

## Welcome

MacroChef (internally "Mizan") is a full-stack meal planning and nutrition tracking application. This guide gets you productive within the first 30 minutes.

**Key facts:**
- Hybrid architecture: Next.js frontend + ASP.NET Core backend
- Schema separation: Frontend (Drizzle) + Backend (EF Core)
- Type-safe API communication via OpenAPI code generation
- Real-time features via SignalR

---

## Project Overview

### What MacroChef Does

- Users create meal plans and track nutrition
- Trainers manage clients and provide coaching
- Admins manage users and system configuration
- All features sync in real-time via SignalR

### Tech Stack at a Glance

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Frontend** | Next.js 16 + React 19 | User interface (SSR + client components) |
| **Backend** | ASP.NET Core 10 | REST API + real-time hubs |
| **Database** | PostgreSQL 18 | Primary data store |
| **Cache** | Redis 7 | JWT caching + SignalR backplane |
| **Auth** | BetterAuth | JWT-based session management |
| **Styling** | Tailwind CSS + shadcn/ui | Component library |

---

## Development Environment Setup

### Prerequisites

**Required (30 minutes to install):**
- Git
- Docker Desktop + Docker Compose
- Either: Bun 1.0+ (frontend) OR Node.js 20+ (npm fallback)
- Either: .NET 10 SDK (backend) OR Docker-only approach

**Recommended:**
- Visual Studio Code
- Postman or Insomnia (API testing)
- DBeaver or pgAdmin (database exploration)

### Windows Setup

```powershell
# 1. Install Docker Desktop
# Download from https://www.docker.com/products/docker-desktop
# Run installer, restart computer

# 2. Install Git
# Download from https://git-scm.com/download/win

# 3. Install Node/Bun
# Option A: Bun (faster)
irm bun.sh/install.ps1 | iex

# Option B: Node.js (fallback)
# Download from https://nodejs.org/ (LTS version)

# 4. Install .NET SDK (optional - Docker fallback works)
# Download from https://dotnet.microsoft.com/download
```

### macOS/Linux Setup

```bash
# 1. Install Docker Desktop
# Via Homebrew:
brew install docker

# 2. Install Bun (faster than npm)
curl -fsSL https://bun.sh/install | bash

# 3. Clone repository
git clone https://github.com/yourusername/macrochef.git
cd macrochef
```

### Verify Installation

```bash
# Check all tools
docker --version          # Should be 24.0+
docker-compose --version  # Should be 2.20+
bun --version            # Should be 1.0+
git --version            # Should be 2.30+

# Optional: Check .NET (if installing locally)
dotnet --version         # Should be 10.0+
```

---

## First Build

### Using Docker Compose (Recommended)

**Total time: 5-10 minutes**

```bash
# 1. Clone and navigate
git clone https://github.com/yourusername/macrochef.git
cd macrochef

# 2. Start all services
docker-compose up -d

# 3. Wait for services to be healthy
docker-compose ps
# All containers should show "healthy" or "running"

# 4. Access the application
# Frontend: http://localhost:3000
# Backend: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

### Using Local Development (Advanced)

If you want to run services locally for faster iteration:

```bash
# Terminal 1: Backend
cd backend
dotnet restore
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api
dotnet run --project Mizan.Api

# Terminal 2: Frontend
cd frontend
bun install
bun run codegen  # Generate types from backend API
bun run dev

# Terminal 3: Database + Redis
docker-compose up postgres redis
```

**Prerequisites for local:**
- PostgreSQL 18 running + accessible
- Redis 7 running + accessible
- .NET 10 SDK installed

---

## Code Structure and Organization

### Frontend Structure

```
frontend/
├── app/                          # Next.js App Router pages
│   ├── (auth)/                  # Authentication routes (public)
│   │   ├── login/
│   │   └── signup/
│   ├── (dashboard)/             # Protected routes (require auth)
│   │   ├── meals/               # Meal planning
│   │   ├── nutrition/           # Nutrition tracking
│   │   └── workouts/            # Workout logs
│   ├── admin/                   # Admin-only routes
│   │   ├── users/               # User management
│   │   └── relationships/       # Trainer-client relationships
│   ├── trainer/                 # Trainer-only routes
│   │   └── clients/             # Client management
│   └── api/                     # Next.js API routes
│       ├── auth/                # BetterAuth endpoints
│       └── health/              # Health checks
├── components/                  # Reusable React components
│   ├── ui/                      # shadcn/ui components
│   ├── forms/                   # Form components
│   └── shared/                  # Shared UI components
├── lib/                         # Services and utilities
│   ├── auth.ts                  # BetterAuth server config
│   ├── auth-client.ts          # Client-side auth + API client
│   ├── hooks/                   # Custom React hooks
│   ├── services/                # SignalR, etc.
│   └── utils/                   # Helper functions
├── db/                          # Drizzle ORM (auth schema)
│   ├── schema.ts                # BetterAuth tables
│   └── client.ts                # Database connection
├── types/                       # TypeScript types
│   └── api.generated.ts        # Generated from OpenAPI
└── middleware.ts               # Next.js middleware (route protection)
```

### Backend Structure

```
backend/
├── Mizan.Api/                   # Presentation Layer (Controllers, Hubs)
│   ├── Controllers/             # HTTP endpoints
│   ├── Hubs/                    # SignalR hubs
│   └── Middleware/              # Custom middleware
├── Mizan.Application/           # Application Layer (CQRS)
│   ├── Commands/                # Write operations
│   ├── Queries/                 # Read operations
│   ├── Dtos/                    # Data Transfer Objects
│   └── Validators/              # FluentValidation rules
├── Mizan.Domain/                # Domain Layer (Core Logic)
│   └── Entities/                # Domain models
├── Mizan.Infrastructure/        # Infrastructure Layer (I/O)
│   ├── Data/                    # EF Core DbContext
│   ├── Migrations/              # Database migrations
│   └── Services/                # External service integrations
└── Mizan.Tests/                 # Test project
    ├── Commands/                # Command handler tests
    ├── Queries/                 # Query handler tests
    └── Integration/             # API tests
```

### Key Directories to Know

| Path | Purpose | Edit When |
|------|---------|-----------|
| `frontend/lib/auth-client.ts` | API client + JWT handling | Adding new API endpoints |
| `backend/Mizan.Api/Controllers/` | HTTP endpoints | Adding new features |
| `backend/Mizan.Infrastructure/Data/MizanDbContext.cs` | Database schema | Modifying entities |
| `frontend/db/schema.ts` | Auth schema (Drizzle) | Changing auth logic |
| `frontend/middleware.ts` | Route protection | Changing access control |

---

## Development Workflow

### Daily Workflow

```bash
# 1. Pull latest changes
git pull origin main

# 2. Start services (if using Docker)
docker-compose up -d

# 3. Create feature branch
git checkout -b feature/your-feature-name

# 4. Make changes (see Common Tasks below)

# 5. Run tests
# Frontend
cd frontend && bun run test
# Backend
docker-compose --profile test up test

# 6. Commit changes
git add .
git commit -m "feat: description of changes"

# 7. Push and create PR
git push origin feature/your-feature-name
```

### Branch Naming Convention

```
feature/noun-description       # New feature
fix/noun-description           # Bug fix
refactor/noun-description      # Code improvement
docs/noun-description          # Documentation
test/noun-description          # Test-related
chore/noun-description         # Maintenance
```

Examples:
- `feature/recipe-search`
- `fix/authentication-timeout`
- `refactor/meal-plan-queries`

### Commit Message Format

```
type(scope): subject

body (optional)

footer (optional)
```

Types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`

Examples:
```
feat(recipes): add bulk import functionality
fix(auth): fix JWT expiration handling
refactor(database): optimize meal plan queries
```

---

## Common Development Tasks

### Adding a New API Endpoint

1. **Backend (ASP.NET Core)**

```csharp
// 1. Add entity to domain if needed
// backend/Mizan.Domain/Entities/MyEntity.cs

// 2. Create Command/Query
// backend/Mizan.Application/Commands/CreateMyEntityCommand.cs
public class CreateMyEntityCommand : IRequest<MyEntityDto>
{
    public string Name { get; set; }
}

// 3. Add validator
// backend/Mizan.Application/Commands/CreateMyEntityCommandValidator.cs
public class CreateMyEntityCommandValidator : AbstractValidator<CreateMyEntityCommand>
{
    public CreateMyEntityCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

// 4. Add handler
// backend/Mizan.Application/Commands/CreateMyEntityCommandHandler.cs
public class CreateMyEntityCommandHandler : IRequestHandler<CreateMyEntityCommand, MyEntityDto>
{
    public async Task<MyEntityDto> Handle(CreateMyEntityCommand request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}

// 5. Add controller endpoint
// backend/Mizan.Api/Controllers/MyEntitiesController.cs
[HttpPost]
public async Task<ActionResult<MyEntityDto>> Create(CreateMyEntityCommand command)
{
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

2. **Frontend (TypeScript)**

```bash
# Generate types from backend API
cd frontend
bun run codegen

# Types are now available in frontend/types/api.generated.ts
# Validation schemas in frontend/lib/validations/api.generated.ts
```

3. **Update next.config.ts if adding new endpoint namespace**

```javascript
// frontend/next.config.ts
const rewrites = async () => ({
  beforeFiles: [
    {
      source: '/api/:path*',
      destination: 'http://mizan-backend:8080/api/:path*' // Docker
    }
  ]
});
```

### Adding a shadcn/ui Component

```bash
cd frontend

# Search for component
# bunx shadcn@latest add button
# bunx shadcn@latest add dialog
# bunx shadcn@latest add form

# Components installed to: frontend/components/ui/
```

### Running Database Migrations

**Backend (EF Core):**

```bash
cd backend

# Create migration
dotnet ef migrations add MigrationName --project Mizan.Infrastructure --startup-project Mizan.Api

# Apply migration
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api

# Or via Docker
docker-compose exec backend dotnet ef database update
```

**Frontend (Drizzle - auth schema only):**

```bash
cd frontend

# Generate migration files
bun run db:generate

# Apply migrations
bun run db:migrate

# Or push schema directly (development only)
bun run db:push
```

### Running Tests

```bash
# Backend (via Docker - recommended)
docker-compose --profile test up test

# Frontend
cd frontend
bun run test            # Unit/integration tests
bun run test:e2e        # End-to-end tests
```

### Debugging

**Backend:**

```bash
# View backend logs
docker-compose logs -f backend

# Connect debugger
# In Visual Studio: Debug → Attach to Process → dotnet process
# In VSCode: Run and Debug → .NET (attach)
```

**Frontend:**

```bash
# View frontend logs
docker-compose logs -f frontend

# Open DevTools
# Ctrl+Shift+I (Windows/Linux) or Cmd+Option+I (Mac)
```

**Database:**

```bash
# Access PostgreSQL
docker exec -it mizan-postgres psql -U mizan -d mizan

# Common queries
SELECT * FROM users;
SELECT COUNT(*) FROM recipes;
SELECT * FROM recipes WHERE user_id = 'your-uuid';
```

---

## Code Style and Conventions

### TypeScript / JavaScript

**File naming:**
- Components: PascalCase (`RecipeForm.tsx`)
- Utilities: camelCase (`calculateMacros.ts`)
- Hooks: camelCase with `use` prefix (`useFormValidation.ts`)

**Component structure:**
```typescript
// Imports
import { useState } from 'react';
import { Button } from '@/components/ui/button';

// Component definition
export function MyComponent() {
  const [state, setState] = useState('');

  return (
    <div>
      <Button onClick={() => setState('new')}>Click me</Button>
    </div>
  );
}

// Export
export default MyComponent;
```

**Naming conventions:**
- Use descriptive names: `handleRecipeSubmit` not `handle`
- Boolean variables: `isLoading`, `hasError`, `canSubmit`
- Event handlers: `handleClick`, `handleChange`

### C# / Backend

**File naming:**
- Classes: PascalCase (`CreateRecipeCommand.cs`)
- Namespaces: PascalCase (`Mizan.Application.Commands`)

**Naming conventions:**
- Private fields: `_contextMock`
- Public properties: PascalCase (`UserId`)
- Constants: UPPER_SNAKE_CASE (`MAX_RECIPE_LENGTH`)

**Class structure:**
```csharp
public class CreateRecipeCommandHandler : IRequestHandler<CreateRecipeCommand, RecipeDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateRecipeCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RecipeDto> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### Code Quality Standards

**DO:**
- Write self-documenting code with clear variable names
- Keep functions small (<25 lines guideline)
- Handle errors explicitly
- Test critical paths (E2E > Integration > Unit)
- Use type-safe operations (avoid `any`)

**DON'T:**
- Add comments explaining what code does (use clear naming instead)
- Leave empty catch blocks
- Mix concerns (I/O mixed with business logic)
- Hardcode values (use constants)
- Commit console.log statements

---

## Getting Help

### Documentation Resources

| Resource | Purpose | Location |
|----------|---------|----------|
| Architecture guide | System design and layers | `docs/ARCHITECTURE.md` |
| API reference | All endpoint documentation | `docs/API_REFERENCE.md` |
| Testing guide | How to write tests | `docs/TESTING_GUIDE.md` |
| Deployment guide | Production setup | `docs/DEPLOYMENT_GUIDE.md` |
| CLAUDE.md | Development guidelines | `CLAUDE.md` |

### Quick Links

- **Swagger UI** (when backend running): http://localhost:5000/swagger
- **Code examples**: Check existing endpoints in `backend/Mizan.Api/Controllers/`
- **Test examples**: See `backend/Mizan.Tests/` for reference
- **Component examples**: Browse `frontend/components/`

### Troubleshooting

**Frontend can't reach backend:**
```bash
# Check if backend is running
docker ps | grep backend

# Test connectivity
curl http://localhost:5000/health
```

**Type mismatch errors:**
```bash
# Regenerate types from API spec
cd frontend && bun run codegen
```

**Tests failing:**
```bash
# Ensure proper isolation
docker-compose --profile test up test

# Check test database exists
docker exec -it mizan-postgres psql -U mizan -l | grep test
```

### Who to Ask

- **Architecture questions:** Check `ARCHITECTURE.md` first
- **API questions:** Check `API_REFERENCE.md` or Swagger UI
- **Testing questions:** Check `TESTING_GUIDE.md`
- **Deployment questions:** Check `DEPLOYMENT_GUIDE.md`
- **Feature-specific:** Read the code comments and existing tests

---

## Next Steps

1. **Get the code running** (15 minutes)
   - Clone repo and run `docker-compose up -d`
   - Verify all services are healthy

2. **Explore the codebase** (30 minutes)
   - Open frontend in code editor
   - Open backend in code editor
   - Browse file structure to understand organization

3. **Run tests** (10 minutes)
   - `docker-compose --profile test up test`
   - `cd frontend && bun run test`

4. **Make a small change** (30 minutes)
   - Create a feature branch
   - Make a small UI change or add a test
   - Commit and push

5. **Read the architecture docs** (30 minutes)
   - Understand schema separation (frontend vs backend)
   - Understand CQRS pattern (Commands and Queries)
   - Understand Clean Architecture layers

---

## Welcome to the Team

You're now set up to contribute to MacroChef. Check `CLAUDE.md` for development philosophy and best practices.

**Questions?** Check the docs first, then ask. We're here to help!
