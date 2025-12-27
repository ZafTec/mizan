# Authentication Architecture

## Overview

Macro Chef uses a **hybrid authentication architecture** where the frontend handles all authentication logic while the backend maintains user data for application features.

## Architecture Principles

### 1. Frontend Authentication (Better Auth)
- **Responsibility**: Complete authentication flow
  - User registration and login
  - Session management
  - Token generation and validation
  - Password hashing
  - OAuth providers (if configured)
  - Email verification
  - JWT/JWK key management

- **Technology**: Better Auth + Drizzle ORM
- **Database Access**: Direct PostgreSQL connection via Drizzle
- **Schema Location**: `frontend/db/schema.ts`

### 2. Backend User Management
- **Responsibility**: Application features requiring user context
  - Recipes owned by users
  - Meal plans
  - Workout tracking
  - Body measurements
  - Trainer-client relationships
  - Food diary entries
  - Achievements and streaks

- **Technology**: .NET 10 + Entity Framework Core
- **Database Access**: PostgreSQL via EF Core
- **Schema Location**: `backend/Mizan.Domain/Entities/`

## Schema Synchronization

### ⚠️ CRITICAL: Source of Truth

**Frontend schema (`frontend/db/schema.ts`) is the SOURCE OF TRUTH for ALL user-related tables:**

- `users`
- `sessions`
- `accounts`
- `jwks`
- `verification`

**Backend MUST sync to match frontend schema.**

### Synchronization Process

When modifying user authentication tables:

1. **Edit frontend schema FIRST**
   ```bash
   # Edit frontend/db/schema.ts
   # Better Auth manages these tables via Drizzle migrations
   ```

2. **Update corresponding backend entities**
   ```bash
   # Edit backend/Mizan.Domain/Entities/*.cs
   # Match field names, types, and constraints EXACTLY
   ```

3. **Update EF Core mappings**
   ```bash
   # Edit backend/Mizan.Infrastructure/Data/MizanDbContext.cs
   # Map PascalCase properties to snake_case columns
   ```

4. **Create EF Core migration**
   ```bash
   cd backend/Mizan.Api
   dotnet ef migrations add <MigrationName> --project ../Mizan.Infrastructure --startup-project .
   ```

5. **Apply migrations**
   ```bash
   # Migrations auto-apply on backend startup
   docker-compose up -d --build
   ```

## User-Related Tables

### Tables Managed by Better Auth (Frontend)

| Table | Purpose | Backend Entity |
|-------|---------|----------------|
| `users` | Core user data | `User.cs` |
| `sessions` | Active user sessions | `Session.cs` |
| `accounts` | OAuth/credential accounts | `Account.cs` |
| `jwks` | JWT signing keys | `Jwk.cs` |
| `verification` | Email verification tokens | `Verification.cs` |

### Backend-Only Tables

All other tables are managed by the backend:
- Food and nutrition
- Recipes and meal plans
- Workouts and exercises
- Body measurements
- Trainer-client relationships
- Gamification (achievements, streaks)
- AI chat threads

## Data Flow

### Registration Flow
```
User → Frontend (Better Auth) → PostgreSQL
                              ↓
                     Backend reads user data
```

### Authentication Flow
```
User → Frontend (Better Auth) → Validate credentials
                              ↓
                     Create session in PostgreSQL
                              ↓
                     Return session token
                              ↓
Backend receives token → Validates against session table
```

### Application Feature Flow
```
User request → Backend API → Check session
                           ↓
                    Fetch user from EF Core
                           ↓
                    Execute business logic
                           ↓
                    Return response
```

## Key Mappings (Frontend ↔ Backend)

### Naming Conventions
- **Frontend (Drizzle)**: `snake_case` column names
- **Backend (EF Core)**: `PascalCase` properties, mapped to `snake_case` columns

### Example: User Table

**Frontend (`schema.ts`):**
```typescript
export const users = pgTable("users", {
  id: uuid("id").defaultRandom().primaryKey(),
  email: varchar("email", { length: 255 }).notNull().unique(),
  emailVerified: boolean("email_verified").default(false),
  createdAt: timestamp("created_at").defaultNow(),
  // ...
});
```

**Backend (`User.cs`):**
```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    // ...
}
```

**Backend Mapping (`MizanDbContext.cs`):**
```csharp
modelBuilder.Entity<User>(entity =>
{
    entity.ToTable("users");
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.Email).HasColumnName("email");
    entity.Property(e => e.EmailVerified).HasColumnName("email_verified");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
    // ...
});
```

## Why This Architecture?

### Benefits
1. **Separation of Concerns**: Auth logic isolated in frontend, business logic in backend
2. **Better Auth Integration**: Leverages Better Auth's complete auth solution
3. **Type Safety**: Frontend gets Drizzle types, backend gets EF Core types
4. **Flexibility**: Easy to swap auth providers without changing backend
5. **Performance**: Backend doesn't handle auth overhead

### Trade-offs
1. **Schema Duplication**: User tables defined in both frontend and backend
2. **Sync Complexity**: Must keep schemas in sync manually
3. **Migration Coordination**: Two migration systems (Drizzle + EF Core)

## Troubleshooting

### Schema Mismatch Errors

If you see errors like:
```
ERROR: column "some_field" does not exist
```

1. Check frontend schema has the field
2. Check backend entity has the property
3. Check EF Core mapping includes the column
4. Create and apply migration
5. Restart containers

### Common Issues

| Error | Cause | Solution |
|-------|-------|----------|
| Column doesn't exist | Backend entity missing field | Add property, update mapping, migrate |
| Wrong data type | Type mismatch | Match types exactly (Guid ↔ uuid, string ↔ text/varchar) |
| Constraint violation | Missing required field | Add field with proper default value |

## Docker Configuration Notes

### Backend Container

The backend runs in development mode without hot reload to prevent memory exhaustion in containerized environments.

**Issue**: `dotnet watch` with polling file watcher causes `System.IO.IOException: Cannot allocate memory` when scanning build output directories with many localization folders.

**Solution**: Use `dotnet run` instead of `dotnet watch run` in Dockerfile CMD. Hot reload is disabled in containers - code changes require container rebuild.

See: [backend/Dockerfile](../backend/Dockerfile:22)

```dockerfile
# Uses dotnet run instead of dotnet watch to avoid memory issues
CMD ["dotnet", "run", "--project", "Mizan.Api/Mizan.Api.csproj", "--urls", "http://0.0.0.0:8080"]
```

## Future Improvements

1. **Automated Schema Sync**: Script to generate backend entities from Drizzle schema
2. **Shared Types**: Generate TypeScript types from C# entities for frontend DTOs
3. **Migration Validation**: Pre-commit hook to verify schema consistency
4. **Documentation**: Auto-generate schema comparison reports
