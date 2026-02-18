# Testing Configuration Guide

This document explains how to configure and run tests with different database backends.

## Overview

The test suite supports three modes:

1. **InMemory Database** - Fast, isolated unit tests (default for local development)
2. **Real PostgreSQL + Redis** - Full integration tests (CI/CD and Docker)
3. **Testcontainers** - Automatic PostgreSQL container (fallback for local integration tests)

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `USE_INMEMORY_DATABASE` | Set to `true` to use InMemory database | `false` |
| `TEST_DB_CONNECTION` | PostgreSQL connection string for tests | (none) |
| `ConnectionStrings__PostgreSQL` | Alternative PostgreSQL connection string | (none) |
| `ConnectionStrings__Redis` | Redis connection string | (none) |

### 1. Local Development (InMemory - Fast)

For quick unit testing during development:

```bash
# Windows PowerShell
$env:USE_INMEMORY_DATABASE="true"
dotnet test backend/Mizan.Tests/Mizan.Tests.csproj

# Linux/macOS/Bash
export USE_INMEMORY_DATABASE=true
dotnet test backend/Mizan.Tests/Mizan.Tests.csproj
```

**Pros:**
- Very fast execution
- No Docker required
- No database setup

**Cons:**
- Doesn't test real SQL queries
- No foreign key constraint testing
- Different behavior from production

### 2. CI/CD Pipeline (Real PostgreSQL 18 + Redis)

The GitHub Actions workflow automatically:
1. Starts PostgreSQL 18 and Redis services
2. Passes connection strings via environment variables
3. Runs migrations automatically
4. Executes all tests

See `.github/workflows/ci.yml` for the configuration.

**CI Environment Variables:**
```yaml
ConnectionStrings__PostgreSQL: "Host=localhost;Port=5432;Database=mizan_test;Username=mizan;Password=mizan_test_password"
ConnectionStrings__Redis: "localhost:6379"
```

### 3. Docker Compose Testing (Real Database)

For testing with real database using Docker:

```bash
# Start test environment
docker-compose -f docker-compose.test.yml up --abort-on-container-exit

# Or with the main compose file
docker-compose --profile test up test
```

This creates:
- PostgreSQL 18 on port 5433
- Redis on port 6380
- Runs all integration tests

### 4. Local Integration Testing (Testcontainers)

If no environment variables are set and `USE_INMEMORY_DATABASE` is not set, tests will automatically start a PostgreSQL container using Testcontainers:

```bash
# Ensure Docker is running, then just run tests
dotnet test backend/Mizan.Tests/Mizan.Tests.csproj
```

This will:
1. Download postgres:18-alpine image
2. Start a temporary container
3. Run migrations
4. Execute tests
5. Clean up container

**Requirements:**
- Docker Desktop or Docker Engine running
- ~500MB disk space for PostgreSQL image

## How It Works

### Test Fixture Logic

The `ApiTestFixture` class automatically detects which database to use:

```csharp
// Priority order:
1. USE_INMEMORY_DATABASE=true → InMemory database
2. TEST_DB_CONNECTION or ConnectionStrings__PostgreSQL env var → Real PostgreSQL
3. Neither → Testcontainers PostgreSQL
```

### Migration Handling

- **InMemory**: Uses `EnsureCreatedAsync()` - no migrations needed
- **Real PostgreSQL**: Runs `MigrateAsync()` to apply all migrations
- **Testcontainers**: Runs `MigrateAsync()` after container starts

### Database Reset

Each test gets a clean database state:

- **InMemory**: Removes all entities via EF Core
- **Real PostgreSQL**: Uses `TRUNCATE TABLE ... CASCADE`

## Troubleshooting

### Tests fail with "No DB connection string available"

**Cause:** Testcontainers couldn't start or environment variable not set.

**Fix:**
```bash
# Option 1: Use InMemory
export USE_INMEMORY_DATABASE=true

# Option 2: Start Docker
# Ensure Docker Desktop is running

# Option 3: Set explicit connection
export ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=mizan_test;Username=mizan;Password=your_password"
```

### Migration errors in CI/CD

**Cause:** Model changes not reflected in migrations.

**Fix:**
```bash
cd backend
dotnet ef migrations add MigrationName --project Mizan.Infrastructure --startup-project Mizan.Api
dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api
```

### Port conflicts

If you have PostgreSQL/Redis running locally:

```bash
# Use InMemory mode
export USE_INMEMORY_DATABASE=true

# Or stop local services
sudo systemctl stop postgresql
sudo systemctl stop redis
```

## Best Practices

1. **Unit Tests**: Use InMemory for fast feedback during development
2. **Pre-commit**: Run with InMemory before committing
3. **Pre-push**: Run with Testcontainers to catch SQL issues
4. **CI/CD**: Full integration tests with real PostgreSQL 18

## VS Code Configuration

Add to `.vscode/settings.json` for seamless testing:

```json
{
  "dotnet-test-explorer.testProjectPath": "backend/Mizan.Tests",
  "dotnet-test-explorer.autoWatch": false,
  "terminal.integrated.env.windows": {
    "USE_INMEMORY_DATABASE": "true"
  },
  "terminal.integrated.env.linux": {
    "USE_INMEMORY_DATABASE": "true"
  },
  "terminal.integrated.env.osx": {
    "USE_INMEMORY_DATABASE": "true"
  }
}
```

## Rider/Visual Studio

Set environment variables in test run configuration:

1. **Rider**: Run → Edit Configurations → Environment Variables
2. **VS**: Test → Test Settings → Default Processor Architecture → Environment

Add:
```
USE_INMEMORY_DATABASE=true
```
