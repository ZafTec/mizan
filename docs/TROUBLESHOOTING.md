# MacroChef Troubleshooting Guide

**Version:** 1.0
**Last Updated:** 2025-12-27

---

## Table of Contents

- [Quick Checklist](#quick-checklist)
- [Docker & Infrastructure](#docker--infrastructure)
- [Backend (ASP.NET Core)](#backend-aspnet-core)
- [Frontend (Next.js)](#frontend-nextjs)
- [Database & Cache](#database--cache)
- [Authentication & Security](#authentication--security)
- [Real-time Features (SignalR)](#real-time-features-signalr)
- [Deployment Issues](#deployment-issues)
- [Performance Issues](#performance-issues)
- [Testing Issues](#testing-issues)

---

## Quick Checklist

**When something breaks, try this first:**

- [ ] Restart services: `docker-compose down && docker-compose up -d`
- [ ] Check logs: `docker-compose logs -f [service]`
- [ ] Verify health: `curl http://localhost:5000/health`
- [ ] Clear cache: `docker exec -it mizan-redis redis-cli FLUSHDB`
- [ ] Regenerate types: `cd frontend && bun run codegen`
- [ ] Restart container: `docker-compose restart [service]`
- [ ] Check environment variables: `cat .env`

---

## Docker & Infrastructure

### Issue: "docker: command not found"

**Symptom:** Docker commands fail

**Solution:**

```bash
# Install Docker Desktop
# Windows/Mac: https://www.docker.com/products/docker-desktop
# Linux (Ubuntu/Debian):
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
newgrp docker

# Verify
docker --version
```

### Issue: "Cannot connect to Docker daemon"

**Symptom:** `docker ps` returns connection error

**Solution:**

```bash
# Windows/Mac: Open Docker Desktop application

# Linux: Start Docker service
sudo systemctl start docker
sudo systemctl enable docker  # Auto-start on boot

# Verify
docker ps
```

### Issue: Port already in use (3000, 5000, 5432, 6379)

**Symptom:** `bind: address already in use` or similar

**Solution:**

```bash
# Find process using port (Windows)
netstat -ano | findstr :3000

# Find process using port (Linux/Mac)
lsof -i :3000

# Kill process
# Windows:
taskkill /PID <process_id> /F

# Linux/Mac:
kill -9 <process_id>

# Or change ports in .env
# FRONTEND_PORT=3001
# BACKEND_PORT=5001
# POSTGRES_PORT=5433
# REDIS_PORT=6380
```

### Issue: "docker-compose: command not found"

**Symptom:** Docker Compose commands fail

**Solution:**

```bash
# Update Docker Compose
docker-compose --version  # Should be 2.20+

# If old version, install new one
# Docker Desktop includes docker-compose v2+
# Or install standalone: https://docs.docker.com/compose/install/

# Verify
docker-compose version
```

### Issue: Container crashes immediately

**Symptom:** `docker-compose up` shows exited containers

**Solution:**

```bash
# Check logs
docker-compose logs <service>

# Common causes:
# 1. Missing environment variables
#    Check .env file has all required vars
cat .env

# 2. Database not ready
#    Restart postgres first
docker-compose restart postgres
docker-compose up -d

# 3. Connection string error
#    Verify ConnectionStrings in .env match docker-compose.yml
```

---

## Backend (ASP.NET Core)

### Issue: Backend returns 500 errors

**Symptom:** API returns "Internal Server Error"

**Solution:**

```bash
# 1. Check backend logs
docker-compose logs -f backend

# 2. Look for exception details in logs
# Common issues:
# - Database connection failed
# - Redis unavailable
# - Invalid JWT configuration

# 3. Verify services are healthy
docker-compose ps  # All should show "healthy" or "running"

# 4. Verify database migrations ran
docker exec -it mizan-postgres psql -U mizan -d mizan -c "\dt"
# Should list tables: foods, recipes, users, etc.

# 5. Force migration restart
docker-compose down
docker volume rm macro_chef_postgres_data  # WARNING: deletes data
docker-compose up -d
```

### Issue: "Unable to connect to database"

**Symptom:** Backend logs show `NpgsqlException` or connection timeout

**Solution:**

```bash
# 1. Verify PostgreSQL is running
docker ps | grep postgres

# 2. Check connection string in backend/.env.production
# Should be: Host=postgres;Database=mizan;...
# (Docker hostname is 'postgres' not 'localhost')

# 3. Test connection
docker exec -it mizan-postgres psql -U mizan -d mizan -c "SELECT 1"

# 4. Check logs
docker logs mizan-postgres | tail -50

# 5. If data is lost, restore from backup
# See DEPLOYMENT_GUIDE.md for restore procedure
```

### Issue: "JWT validation failed" (401 Unauthorized)

**Symptom:** All API requests return 401

**Solution:**

```bash
# 1. Verify JWKS endpoint is accessible
curl http://localhost:3000/api/auth/jwks

# 2. Check Redis cache
docker exec -it mizan-redis redis-cli
> KEYS "jwks:*"

# 3. Verify JWT configuration matches frontend
# backend/.env.production should have:
# BetterAuth__JwksUrl=http://frontend:3000/api/auth/jwks
# BetterAuth__Issuer=http://localhost:3000

# 4. Clear JWKS cache and try again
docker exec -it mizan-redis redis-cli FLUSHDB

# 5. Test with valid JWT
curl -H "Authorization: Bearer <valid-jwt>" http://localhost:5000/api/users/me
```

### Issue: "Validation failed" (400 Bad Request)

**Symptom:** POST requests return validation errors

**Solution:**

```bash
# 1. Check response body for error details
curl -X POST http://localhost:5000/api/recipes \
  -H "Content-Type: application/json" \
  -d '{"name": ""}' \
  -v  # Verbose to see response

# 2. Regenerate frontend validation schemas
cd frontend && bun run codegen

# 3. Verify FluentValidation rules in backend
# backend/Mizan.Application/Commands/*Validator.cs

# 4. Check API specification at Swagger UI
# http://localhost:5000/swagger
```

### Issue: "Access Denied" or "Forbidden" (403)

**Symptom:** Authenticated user gets 403 error

**Solution:**

```bash
# 1. Verify user role
# In PostgreSQL:
docker exec -it mizan-postgres psql -U mizan -d mizan
SELECT id, email, role FROM users WHERE email='your-email@example.com';

# 2. Update role if needed
UPDATE users SET role='trainer' WHERE id='user-id';

# 3. Verify authorization headers in request
curl -H "X-User-Id: your-uuid" \
     -H "X-User-Role: trainer" \
     http://localhost:5000/api/trainers/clients

# 4. Check handler authorization logic
# Look for [Authorize] attributes and permission checks
grep -r "Forbidden\|403" backend/Mizan.Application/
```

### Issue: EF Core migration conflicts

**Symptom:** Migration fails with "naming conflict" or "duplicate key"

**Solution:**

```bash
# 1. Check pending migrations
docker exec -it mizan-backend dotnet ef migrations list --project Mizan.Infrastructure --startup-project Mizan.Api

# 2. Remove last migration if not applied to database
docker exec -it mizan-backend dotnet ef migrations remove --project Mizan.Infrastructure --startup-project Mizan.Api

# 3. Create new migration
docker exec -it mizan-backend dotnet ef migrations add YourMigrationName --project Mizan.Infrastructure --startup-project Mizan.Api

# 4. Apply migration
docker exec -it mizan-backend dotnet ef database update --project Mizan.Infrastructure --startup-project Mizan.Api
```

---

## Frontend (Next.js)

### Issue: "Cannot find module" or "Module not found"

**Symptom:** Frontend fails to build or run

**Solution:**

```bash
cd frontend

# 1. Reinstall dependencies
rm -rf node_modules bun.lock
bun install

# 2. Clear Next.js cache
rm -rf .next

# 3. Run dev server
bun run dev

# 4. Check for missing imports
# Look at error message for specific file
```

### Issue: Types mismatch (TypeScript errors)

**Symptom:** TS errors about undefined properties or type mismatches

**Solution:**

```bash
cd frontend

# 1. Regenerate types from backend
bun run codegen

# 2. Check that backend is running
curl http://localhost:5000/swagger/v1/swagger.json

# 3. Clear TypeScript cache
bunx tsc --noEmit

# 4. Check frontend/types/api.generated.ts exists
# If missing, backend OpenAPI endpoint not accessible

# 5. Verify API response matches expected type
curl -H "Authorization: Bearer <token>" \
  http://localhost:5000/api/users/me | jq '.'
```

### Issue: "Cannot GET /" or blank page

**Symptom:** Frontend loads but shows nothing

**Solution:**

```bash
# 1. Check console errors (browser DevTools)
# Ctrl+Shift+I or Cmd+Option+I

# 2. Check frontend logs
docker-compose logs -f frontend

# Common issues:
# - Missing environment variables
# - Backend not accessible
# - Client-side error in component

# 3. Verify environment variables
docker exec -it mizan-frontend sh
env | grep API_URL

# 4. Test backend connectivity from frontend
docker exec -it mizan-frontend curl http://mizan-backend:8080/health

# 5. Hard refresh browser
# Ctrl+Shift+R (Windows/Linux)
# Cmd+Shift+R (Mac)
```

### Issue: CSS not loading (unstyled page)

**Symptom:** Page works but has no styling

**Solution:**

```bash
# 1. Clear Next.js cache
cd frontend
rm -rf .next

# 2. Rebuild
bun run build

# 3. Verify Tailwind is configured
# Check next.config.ts has proper content paths
# Check tailwind.config.ts exists

# 4. Check for missing imports in component
# All components should import from '@/components/ui/'

# 5. Rebuild with production settings
bun run build && bun run start
```

### Issue: "CORS error" or "Cannot access API"

**Symptom:** Browser shows CORS error when calling API

**Solution:**

```bash
# 1. Verify CORS is configured in backend
# backend/Program.cs should have AddCors

# 2. Check that requests use correct origin
# Server-side: http://mizan-backend:8080 (Docker network)
# Client-side: http://localhost:3000 (proxied)

# 3. Verify proxy rules in next.config.ts
# Should have rewrite for /api/* to backend

# 4. Test direct API call
curl -v http://localhost:5000/api/users/me \
  -H "Authorization: Bearer <token>"
# Should not show CORS errors (browser checks only)
```

### Issue: "Client hydration mismatch"

**Symptom:** Console shows hydration warning, page flickers

**Solution:**

```bash
# 1. Check for conditional rendering based on window
# DON'T: if (typeof window !== 'undefined') { ... }
# DO: Use useEffect for client-only code

// Wrong
export function MyComponent() {
  if (typeof window === 'undefined') return null;
  return <div>{window.location.href}</div>;
}

// Right
export function MyComponent() {
  const [url, setUrl] = useState('');
  useEffect(() => {
    setUrl(window.location.href);
  }, []);
  return <div>{url}</div>;
}

# 2. Rebuild and restart
cd frontend
bun run build
bun run start
```

---

## Database & Cache

### Issue: "Connection refused" to PostgreSQL

**Symptom:** Backend can't connect to database

**Solution:**

```bash
# 1. Check PostgreSQL is running
docker ps | grep postgres

# 2. Restart PostgreSQL
docker-compose restart postgres

# 3. Check database exists
docker exec -it mizan-postgres psql -U mizan -l | grep mizan

# 4. Check connection string (no 'localhost')
# Should be: Host=postgres;Database=mizan;...
# NOT: Host=localhost;...

# 5. Test connection manually
docker exec -it mizan-postgres psql -U mizan -d mizan -c "SELECT version();"
```

### Issue: "Redis connection failed"

**Symptom:** Backend logs show Redis errors

**Solution:**

```bash
# 1. Check Redis is running
docker ps | grep redis

# 2. Test Redis connection
docker exec -it mizan-redis redis-cli ping
# Should return PONG

# 3. Restart Redis
docker-compose restart redis

# 4. Check Redis logs
docker logs mizan-redis | tail -20

# 5. Verify connection string
# Should be: redis:6379 (Docker hostname)
# NOT: localhost:6379
```

### Issue: "Disk space full" error

**Symptom:** Database or cache stops responding

**Solution:**

```bash
# 1. Check disk usage
df -h

# 2. Find large files
du -sh /var/lib/docker/volumes/*

# 3. Clean up Docker
docker system prune -a

# 4. Clean old backups
rm -rf /backups/*_old.sql.gz

# 5. Expand disk (if on VM)
# Stop containers first
docker-compose down
# Then expand volume and restart
```

### Issue: "Cannot drop database because it's in use"

**Symptom:** Reset script fails due to database in use

**Solution:**

```bash
# 1. Kill all connections
docker exec -it mizan-postgres psql -U mizan -d mizan -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='mizan';"

# 2. Drop and recreate
docker exec -it mizan-postgres psql -U mizan -c "DROP DATABASE IF EXISTS mizan;"
docker exec -it mizan-postgres psql -U mizan -c "CREATE DATABASE mizan;"

# 3. Re-run migrations
docker-compose down
docker-compose up -d
```

---

## Authentication & Security

### Issue: "Session expired" or keeps logging out

**Symptom:** User gets logged out unexpectedly

**Solution:**

```bash
# 1. Check JWT expiration (15 minutes default)
# backend/.env.production or backend/appsettings.json
# Look for JwtLifetime or TokenExpiry

# 2. Check session duration
# frontend/lib/auth.ts for session configuration

# 3. Verify cookies are being set
# Browser DevTools → Application → Cookies
# Should have 'auth' or 'session' cookie with httpOnly flag

# 4. Check Redis session cache
docker exec -it mizan-redis redis-cli
> KEYS "*session*"

# 5. If cookies are cleared, disable browser cache clearing:
# For development: Add to .env
# NEXT_PUBLIC_DEBUG_AUTH=true
```

### Issue: "Cannot sign up" or "Email already exists"

**Symptom:** Registration fails even with new email

**Solution:**

```bash
# 1. Check if user exists
docker exec -it mizan-postgres psql -U mizan -d mizan
SELECT email FROM users WHERE email='test@example.com';

# 2. Delete duplicate user (if safe)
DELETE FROM users WHERE email='test@example.com';

# 3. Check email validation rules
# backend/Mizan.Application/Commands/*SignUp*Validator.cs

# 4. Verify BetterAuth email configuration
# frontend/lib/auth.ts

# 5. Check auth logs for specific error
docker logs mizan-frontend | grep -i signup
```

### Issue: "Invalid credentials" on login

**Symptom:** Correct password still fails

**Solution:**

```bash
# 1. Verify user exists and role is correct
docker exec -it mizan-postgres psql -U mizan -d mizan
SELECT id, email, role FROM users WHERE email='test@example.com';

# 2. Reset password in database (if needed)
# Use bcrypt hash or BetterAuth reset flow

# 3. Check login attempt logs
docker logs mizan-frontend | grep -i login

# 4. Verify email format
# Some auth systems are case-sensitive

# 5. Clear auth cache and try again
docker exec -it mizan-redis redis-cli FLUSHDB
```

---

## Real-time Features (SignalR)

### Issue: "WebSocket connection failed"

**Symptom:** Real-time chat doesn't work, console shows WS error

**Solution:**

```bash
# 1. Test WebSocket connectivity
curl -i -N -H "Connection: Upgrade" \
  -H "Upgrade: websocket" \
  http://localhost:5000/hubs/chat

# 2. Verify SignalR is enabled in backend
# backend/Program.cs should have AddSignalR and MapHub

# 3. Check proxy WebSocket support
# nginx.conf should have Upgrade headers configured

# 4. Verify Redis backplane is running
docker exec -it mizan-redis redis-cli ping

# 5. Restart backend SignalR service
docker-compose restart backend
```

### Issue: "Cannot join conversation" error

**Symptom:** Users can't enter chat, see error message

**Solution:**

```bash
# 1. Verify trainer-client relationship exists
docker exec -it mizan-postgres psql -U mizan -d mizan
SELECT * FROM trainer_client_relationships WHERE trainer_id='uuid' AND client_id='uuid';

# 2. Check relationship status is 'active'
UPDATE trainer_client_relationships
SET status='active'
WHERE id='relationship-id';

# 3. Verify both users are authenticated
# Check browser console for auth errors

# 4. Check backend logs for authorization errors
docker logs mizan-backend | grep -i "chat\|hub"

# 5. Restart SignalR connection
# Browser: Refresh page
# Frontend: reconnect logic in lib/services/signalr-chat.ts
```

### Issue: Messages not appearing in real-time

**Symptom:** Messages sent but don't show up, need page refresh

**Solution:**

```bash
# 1. Check browser console for errors
# Ctrl+Shift+I → Console tab

# 2. Verify SignalR is connected
# frontend/lib/services/signalr-chat.ts should show "connected" status

# 3. Check chat service initialization
# Verify chatService.connect() is called on mount

# 4. Test message persistence in database
docker exec -it mizan-postgres psql -U mizan -d mizan
SELECT * FROM chat_messages ORDER BY created_at DESC LIMIT 10;

# 5. Restart SignalR connection
# Clear browser cache and reload
```

---

## Deployment Issues

### Issue: "Connection refused" in production

**Symptom:** After deployment, services can't communicate

**Solution:**

```bash
# 1. Verify services are healthy
docker-compose -f docker-compose.prod.yml ps

# 2. Check logs
docker-compose -f docker-compose.prod.yml logs backend
docker-compose -f docker-compose.prod.yml logs frontend

# 3. Verify environment variables
# backend/.env.production, frontend/.env.production

# 4. Check network connectivity
docker exec mizan-backend curl http://mizan-frontend:3000/health
docker exec mizan-frontend curl http://mizan-backend:8080/health

# 5. Verify DNS/domain resolves
nslookup yourdomain.com
```

### Issue: SSL certificate expired

**Symptom:** "Certificate verification failed" or "ERR_CERT_DATE_INVALID"

**Solution:**

```bash
# 1. Check certificate expiry
openssl s_client -connect yourdomain.com:443 | grep -A 5 validity

# 2. Renew with Let's Encrypt
sudo certbot renew --quiet

# 3. Restart nginx/reverse proxy
sudo systemctl restart nginx

# 4. Verify renewal
curl -v https://yourdomain.com

# 5. Check auto-renewal is configured
sudo crontab -l | grep certbot
# Should show daily renewal job
```

### Issue: "Out of memory" or "Container killed"

**Symptom:** Container suddenly stops, no error message

**Solution:**

```bash
# 1. Check resource limits
docker stats --no-stream

# 2. Increase memory limit in docker-compose.prod.yml
services:
  backend:
    deploy:
      resources:
        limits:
          memory: 2G  # Increase from 1G

# 3. Optimize queries
# Look for N+1 queries in backend logs
# Add database indexes

# 4. Clear old data
docker exec -it mizan-postgres psql -U mizan -d mizan
DELETE FROM chat_messages WHERE created_at < NOW() - INTERVAL '90 days';
```

---

## Performance Issues

### Issue: "Slow API responses" (>1s)

**Symptom:** API calls take longer than expected

**Solution:**

```bash
# 1. Check database query performance
# Enable slow query log in PostgreSQL
ALTER SYSTEM SET log_min_duration_statement = 1000;  -- Log queries >1s
SELECT pg_reload_conf();

# 2. Check for missing indexes
# Look at slow logs for frequently queried columns
CREATE INDEX idx_food_diary_userid_date ON food_diary_entries(user_id, date);

# 3. Monitor API response times
# Use Application Insights or check backend logs

# 4. Profile with browser DevTools
# Network tab → Sort by time

# 5. Clear Redis cache if excessive DB hits
docker exec -it mizan-redis redis-cli FLUSHDB
```

### Issue: "High CPU usage"

**Symptom:** Server running hot, fans spinning up

**Solution:**

```bash
# 1. Identify heavy process
docker stats

# 2. Check backend logs for infinite loops
docker logs mizan-backend | tail -100

# 3. Check for stuck migrations
# These can run indefinitely
docker exec -it mizan-postgres psql -U mizan -d mizan
SELECT * FROM pg_stat_activity WHERE wait_event_type IS NOT NULL;

# 4. Kill stuck process
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE query LIKE '%migration%';

# 5. Optimize problematic query
# Use EXPLAIN ANALYZE to see execution plan
EXPLAIN ANALYZE SELECT * FROM large_table WHERE expensive_filter;
```

### Issue: "Frontend bundle too large"

**Symptom:** Slow page load, large JS files

**Solution:**

```bash
cd frontend

# 1. Analyze bundle
bun run build  # Shows bundle size in output

# 2. Check for large dependencies
bunx npm-check-updates

# 3. Use dynamic imports for code splitting
// Instead of
import HeavyComponent from './HeavyComponent';

// Use
const HeavyComponent = dynamic(() => import('./HeavyComponent'));

# 4. Enable production optimizations
NODE_ENV=production bun run build

# 5. Check unused dependencies
bunx depcheck
```

---

## Testing Issues

### Issue: "Testcontainers fails to start"

**Symptom:** Tests fail with Docker error

**Solution:**

```bash
# 1. Verify Docker is running
docker ps

# 2. Check Docker daemon permissions
docker run hello-world

# 3. Check free disk space
docker system df

# 4. Clear unused images
docker system prune -a

# 5. Rebuild test images
docker-compose --profile test build test
```

### Issue: "Tests hang or timeout"

**Symptom:** `dotnet test` doesn't complete, times out

**Solution:**

```bash
# 1. Check for infinite loops in test code
# Look at most recent test file

# 2. Increase timeout
# In test file: [Timeout(60000)] on test method

# 3. Run single test to isolate
dotnet test --filter "YourTestClass.YourTestMethod"

# 4. Check database is responding
docker exec -it mizan-postgres pg_isready -U mizan

# 5. Kill hung process
docker-compose --profile test down
docker system prune -f
```

### Issue: "Database connection error in tests"

**Symptom:** `NpgsqlException` during test run

**Solution:**

```bash
# 1. Verify test database connection string
# backend/Mizan.Tests/appsettings.Test.json or env var
# Should use mizan_test database

# 2. Check test database exists
docker exec -it mizan-postgres psql -U mizan -l | grep test

# 3. Clear test database
docker exec -it mizan-postgres psql -U mizan -c "DROP DATABASE IF EXISTS mizan_test; CREATE DATABASE mizan_test;"

# 4. Run tests with verbose logging
dotnet test --logger:console --verbosity:detailed

# 5. Check Testcontainers configuration
# backend/Mizan.Tests/Fixtures/DatabaseFixture.cs
```

---

## Emergency Procedures

### Complete System Reset

Use only if everything is broken and you need a fresh start:

```bash
# WARNING: This deletes all data

docker-compose down -v

# Remove all data
docker system prune -a
docker volume prune

# Delete database backup
rm -rf /backups

# Restart fresh
git checkout .env
docker-compose up -d
```

### Database Restore from Backup

```bash
# List backups
ls -lh /backups/

# Restore database
gunzip < /backups/postgres_YYYYMMDD_HHMMSS.sql.gz | \
  docker exec -i mizan-postgres psql -U mizan mizan

# Verify restoration
docker exec -it mizan-postgres psql -U mizan -d mizan \
  -c "SELECT COUNT(*) as recipe_count FROM recipes;"
```

---

## Additional Help

- **Architecture questions:** See `ARCHITECTURE.md`
- **Deployment help:** See `DEPLOYMENT_GUIDE.md`
- **API documentation:** See `API_REFERENCE.md` or Swagger UI
- **Testing guide:** See `TESTING_GUIDE.md`
- **Development setup:** See `DEVELOPER_ONBOARDING.md`

**Still stuck?** Check the logs first, then review this guide systematically.
