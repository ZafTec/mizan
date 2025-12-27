# Changelog

All notable changes to MacroChef will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.2.0] - 2025-12-27

### Added

#### Better Auth Access Control Plugin
- Integrated Better Auth access control plugin with role-based permissions
- Defined custom resources and actions (user, recipe, mealPlan, workout, household, trainerClient)
- Created permission definitions in `frontend/lib/permissions.ts`
- Configured three-tier role system (user, trainer, admin)
- Added permission checking capabilities (`checkRolePermission`)

#### Trainer Features
- **Client Management Dashboard** (`/trainer` route)
  - View active clients with permission badges
  - See pending client requests
  - Stats overview (active clients, pending requests, paused clients)
- **New Backend Endpoints:**
  - `GET /api/Trainers/clients` - List trainer's clients
  - `GET /api/Trainers/requests` - List pending client requests
  - `GET /api/Trainers/clients/{clientId}/nutrition` - View client nutrition data (permission-based)
- **Authorization Implementation:**
  - Validates active trainer-client relationship
  - Checks granular permissions (canViewNutrition, canViewWorkouts, canViewMeasurements)
  - Comprehensive logging for all trainer actions

#### Admin Features
- Admin dashboard (`/admin` route) with user management
- User list page with search, filter, and pagination (`/admin/users`)
- User detail page with role management and actions (`/admin/users/[id]`)
- Create user functionality (`/admin/users/create`)
- Admin actions: ban/unban, set role, revoke sessions, impersonate, delete
- Relationship management page (`/admin/relationships`)

#### Documentation
- **API_REFERENCE.md** - Complete API documentation with request/response examples
- **DEPLOYMENT_GUIDE.md** - Production deployment guide with Docker Compose, SSL, backups
- **TESTING_GUIDE.md** - Comprehensive testing guide (unit, integration, E2E)
- **SECURITY.md** - Security documentation, best practices, vulnerability disclosure
- **CHANGELOG.md** - This file
- Updated **ARCHITECTURE.md** with BFF pattern, authorization, trainer features, SignalR

#### Next.js 16 Migration
- Created `frontend/proxy.ts` to replace deprecated `middleware.ts`
- Implemented optimistic cookie checks (no database queries on every request)
- Route protection for `/admin/*` and `/trainer/*`
- Follows Next.js 16 best practices from official documentation

### Fixed

#### Critical Security Vulnerabilities (All Fixed - Commit b6acf69)

**VULN-01: Shopping List Horizontal Privilege Escalation**
- Added ownership validation to `GetShoppingListQuery`
- Added ownership validation to `AddShoppingListItemCommand`
- Added ownership validation to `ToggleShoppingListItemCommand`
- Added ownership validation to `UpdateShoppingListItemCommand`
- Added ownership validation to `DeleteShoppingListCommand`

**VULN-02: Meal Plan Unauthorized Access**
- Added ownership validation to `GetMealPlanByIdQuery`
- Added ownership validation to `AddRecipeToMealPlanCommand`
- Added ownership validation to `DeleteMealPlanCommand`

**VULN-03: Household Information Disclosure**
- Added membership validation to `GetHouseholdQuery`
- Now verifies user is member before returning household data

**VULN-04: Chat Conversation Unauthorized Access**
- Added participation validation to `GetChatConversationQuery`
- Verifies user is either trainer or client in relationship

**VULN-05: SignalR Hub Unauthorized Group Join**
- Added conversation access validation to `ChatHub.JoinConversation`
- Validates user participation before allowing group join

#### Bug Fixes
- Fixed recipe creation validation issues
- Improved session validation in proxy middleware
- Fixed query performance issues with proper indexing

### Changed

- **Middleware Migration:** Replaced `middleware.ts` with `proxy.ts` (Next.js 16)
- **Authorization Pattern:** Standardized authorization checks across all handlers
- **Logging:** Enhanced logging for trainer and admin actions
- **Error Handling:** Improved error messages for authorization failures

### Security

- **BFF Secret Validation:** Constant-time comparison to prevent timing attacks
- **JWT Configuration:** ES256 algorithm, 15-minute expiry, httpOnly cookies
- **CSRF Protection:** Double-submit cookie pattern via `csrf-csrf`
- **Authorization Logging:** All unauthorized access attempts logged with user ID and resource

---

## [1.1.0] - 2025-12-20

### Added

#### SignalR Real-Time Features
- ChatHub for trainer-client messaging
- GoalHub for goal assignment notifications
- NotificationHub for system notifications
- Redis backplane for horizontal scaling
- Frontend SignalR service (`lib/services/signalr-chat.ts`)

#### BFF Authentication Pattern
- Backend-for-Frontend authentication with trusted headers
- `BffAuthenticationHandler` with constant-time secret validation
- User context forwarding via `X-User-Id`, `X-User-Email`, `X-User-Role`
- JWKS caching in Redis (1-minute TTL)

#### Code Generation
- OpenAPI → TypeScript type generation
- OpenAPI → Zod schema generation
- Automatic case conversion (PascalCase ↔ camelCase)
- `bun run codegen` command for full sync

### Changed

- **Authentication Flow:** JWT validation now happens in Next.js BFF
- **API Communication:** Backend trusts BFF via shared secret
- **Type Safety:** All DTOs auto-generated from OpenAPI spec

---

## [1.0.0] - 2025-12-01

### Added

#### Core Features
- User authentication and registration (BetterAuth)
- Recipe management (create, edit, delete, share)
- Meal planning (weekly planner)
- Shopping list generation from meal plans
- Nutrition tracking (food diary)
- Workout logging
- Body measurement tracking
- Household management (multi-user groups)
- Achievement system (gamification)

#### Tech Stack
- **Frontend:** Next.js 16 + React 19 + TypeScript + Tailwind CSS
- **Backend:** ASP.NET Core 10 + Clean Architecture
- **Database:** PostgreSQL 18
- **Cache:** Redis 7
- **Auth:** BetterAuth (JWT-based, ES256)

#### Infrastructure
- Docker Compose deployment
- Automated database migrations (EF Core + Drizzle)
- Health check endpoints
- Logging infrastructure

---

## Version History

- **1.2.0** - Better Auth access control, trainer features, admin UI, security fixes
- **1.1.0** - SignalR, BFF pattern, code generation
- **1.0.0** - Initial release with core meal planning and nutrition tracking

---

## Upgrade Guide

### Upgrading to 1.2.0

**Required Steps:**

1. **Update Environment Variables:**
   ```bash
   # Add to frontend/.env.production
   BFF_SECRET=<generate-32-char-secret>

   # Add to backend/.env.production
   Bff__TrustedSecret=<same-as-BFF_SECRET>
   ```

2. **Run Database Migrations:**
   ```bash
   # Backend (EF Core)
   docker exec -it mizan-backend dotnet ef database update

   # Frontend (Drizzle)
   docker exec -it mizan-frontend bun run db:migrate
   ```

3. **Create First Admin User:**
   ```sql
   -- Sign up via UI, then:
   UPDATE users SET role = 'admin' WHERE email = 'your-email@example.com';
   ```

4. **Rebuild and Restart:**
   ```bash
   docker-compose -f docker-compose.prod.yml build
   docker-compose -f docker-compose.prod.yml up -d
   ```

5. **Verify Security Fixes:**
   - Test shopping list access (should fail for non-owners)
   - Test meal plan access (should fail for non-owners)
   - Test household access (should fail for non-members)
   - Test chat access (should fail for non-participants)

**Breaking Changes:**
- `middleware.ts` removed - use `proxy.ts` instead
- Shopping list/meal plan APIs now enforce ownership (unauthorized requests return 401/403)
- Chat conversations require active relationship

**Deprecations:**
- None

---

## Contributing

When adding entries to this changelog:
- Use format: `### Added|Changed|Fixed|Deprecated|Removed|Security`
- Link to issues/PRs when applicable
- Include migration steps for breaking changes
- Date format: YYYY-MM-DD
