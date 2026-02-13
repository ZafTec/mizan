# Database Migration Guide

## Overview

This project uses a **split migration strategy**:
- **Backend (.NET/EF Core)**: Manages business logic tables (recipes, foods, workouts, goals, etc.)
- **Frontend (Drizzle/Better Auth)**: Manages authentication tables (users, accounts, sessions, verification, jwks)

## Migration Order

**IMPORTANT**: Migrations must run in this specific order:

1. **Frontend First**: Run Drizzle migrations to create auth tables (especially `users`)
2. **Backend Second**: Run EF Core migrations to create business tables

### Why This Order?

- Backend tables have foreign keys to the `users` table
- The `users` table must exist **before** backend migrations run
- Frontend (Better Auth) creates and owns the `users` table
- Backend migrations reference `users` via foreign keys but don't create it

**⚠️ ARCHITECTURAL WEAKNESS**: The current design has a circular dependency issue. Backend tables depend on `users` table (via foreign keys), but conceptually the backend should be independent. In the future, consider removing foreign keys from backend tables to `users` and instead use application-level validation, or have the backend own all tables with the frontend using API calls for auth.

## Frontend Migration (Drizzle) - RUN FIRST

### Step 1: Start Frontend

```bash
docker-compose up -d frontend
```

The startup script (`start.sh`) will:
1. Check if `RUN_DB_MIGRATIONS=true`
2. Run `drizzle-kit migrate` to create auth tables (users, accounts, sessions, verification, jwks)
3. Start the Next.js server

### Verify Auth Tables Created

```bash
docker exec -it postgresql-db psql -U admin -d mizan -c "\\dt users accounts sessions verification jwks"
```

## Backend Migration (EF Core) - RUN SECOND

### Prerequisites

Ensure you're in the backend directory and have the .NET SDK:
```bash
cd backend
```

### Step 1: Generate Migration

Since we deleted all old migrations, generate a fresh initial migration:

```bash
cd Mizan.Infrastructure
dotnet ef migrations add InitialCreate \
  --startup-project ../Mizan.Api \
  --project . \
  --output-dir Data/Migrations \
  --context MizanDbContext
```

**Note**: The `users` table is configured with `SetIsTableExcludedFromMigrations(true)` in `MizanDbContext.cs`. This correctly excludes the `users` table from migrations. The generated migration will create all backend tables and their foreign keys to `users`, but will NOT create the `users` table itself.

### Step 2: Verify Migration (Optional)

Check the generated file to confirm `users` table is not created:

```bash
grep 'name: "users"' Data/Migrations/*_InitialCreate.cs
```

This should return nothing (or only show foreign key references, not table creation).

### Step 3: Apply Migration

Run the backend in Development mode to apply migrations:

```bash
# Option 1: Run locally with Development environment
ASPNETCORE_ENVIRONMENT=Development dotnet run --project Mizan.Api

# Option 2: Using docker-compose (set environment in docker-compose.yml)
docker-compose up -d backend
```

**Note**: In Production mode, the backend does NOT auto-migrate. You must manually apply migrations or use a CI/CD pipeline.

## Troubleshooting

### Error: "relation 'users' already exists"

This happens when EF Core tries to create the `users` table that was already created by Drizzle.

**Solution**: Verify the User entity in `MizanDbContext.cs` has:
```csharp
entity.Metadata.SetIsTableExcludedFromMigrations(true);
```

If the exclusion isn't working, check that you're using EF Core 6.0+ and the table exclusion is set in `OnModelCreating`.

### Error: "Foreign key constraint violation"

This happens when backend migrations run before the frontend creates the `users` table.

**Solution**: Ensure migrations run in correct order:
1. Frontend migrations FIRST (creates users table)
2. Backend migrations SECOND (creates business tables with FKs to users)

### Mixed Migration State

If you have a messy migration state:

1. **Nuclear Option**: Drop all tables and start fresh:
   ```sql
   DROP SCHEMA public CASCADE;
   CREATE SCHEMA public;
   GRANT ALL ON SCHEMA public TO admin;
   ```

2. **Regenerate migrations**:
   - Delete `backend/Mizan.Infrastructure/Data/Migrations/*`
   - Delete `frontend/db/migrations/*`
   - Follow the steps above in correct order (Frontend FIRST, Backend SECOND)

## Database Schema Ownership

| Tables | Managed By | Migration Tool | Created By |
|--------|-----------|----------------|------------|
| users, accounts, sessions, verification, jwks | Frontend | Drizzle | Frontend First |
| foods, recipes, workouts, goals, households, etc. | Backend | EF Core | Backend Second |

## Technical Details

### How `SetIsTableExcludedFromMigrations` Works

In `MizanDbContext.cs`:
```csharp
modelBuilder.Entity<User>(entity =>
{
    entity.ToTable("users");
    // ... configuration ...
    
    // This tells EF Core to skip this table in migrations
    entity.Metadata.SetIsTableExcludedFromMigrations(true);
});
```

**What this does:**
- ✅ Excludes `users` table from `CreateTable` operations in migrations
- ✅ Excludes `users` table from `DropTable` operations in migrations
- ✅ Still allows foreign key references TO the `users` table
- ✅ Still allows queries on the `users` table (it's mapped but not migrated)

**What this does NOT do:**
- ❌ Does not prevent foreign keys from being created (which is what we want)
- ❌ Does not remove the table from the model (it's still queryable)

## Important Notes

1. **Always run migrations in order**: Frontend first, Backend second
2. **Never manually edit migration files** - the exclusion should work automatically
3. **Always backup database before running migrations in production**
4. **The circular dependency** (backend FKs to users table) is a known architectural weakness - see issue #XXX for future improvements
