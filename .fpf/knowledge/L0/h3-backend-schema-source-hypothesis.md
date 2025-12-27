---
id: h3-backend-schema-source
type: hypothesis
created: 2025-12-19T00:40:00Z
problem: "Architecture review: validation, security, schema duplication, Redis, UI bugs"
status: L0
formality: 5
novelty: Radical
complexity: Medium
author: Claude (generated), Human (to review)
scope:
  applies_to: "Projects with backend as schema authority, frontend as consumer"
  not_valid_for: "Projects where frontend needs schema independence (e.g., offline-first)"
  scale: "Solo developer willing to treat backend as single source of truth"
---

# Hypothesis: Backend Schema as Source, Frontend Consumes (Radical)

## 1. The Method (Design-Time)

### Proposed Approach
Make the **backend the single source of truth** for the database schema. Frontend abandons Drizzle schema definitions entirely and instead:
1. Fetches **OpenAPI spec** from backend (auto-generated from EF Core + controllers)
2. Generates **TypeScript types** from OpenAPI
3. Generates **Zod schemas** from OpenAPI
4. Uses **raw SQL queries** or a thin query builder instead of Drizzle ORM for BetterAuth schema

### Rationale
- Backend already has complete schema via EF Core
- OpenAPI generation from .NET controllers is built-in (Swashbuckle)
- TypeScript codegen from OpenAPI is mature (openapi-typescript, orval)
- Eliminates Drizzle ORM dependency on frontend (reduces bundle size)
- BetterAuth only needs minimal database access (can use raw Postgres client)
- Single schema authority simplifies governance

### Implementation Steps

1. **Enable OpenAPI Generation** (Day 1):
   - Backend already has Swagger/Swashbuckle configured
   - Add NSwag or Swashbuckle.AspNetCore.Cli to generate `openapi.json` at build time
   - Expose OpenAPI spec at `/swagger/v1/swagger.json` (already exists)

2. **Frontend Type Generation** (Day 2-3):
   - Install `openapi-typescript` or `orval`
   - Add script: `npm run codegen:types` → fetches OpenAPI spec → generates TypeScript types
   - Output: `frontend/types/api.generated.ts`
   - Add to pre-build hook

3. **Zod Schema Generation** (Day 3-4):
   - Use `@anatine/zod-openapi` or custom script to convert OpenAPI → Zod schemas
   - Output: `frontend/lib/validations/api.generated.ts`
   - All form validation now uses generated Zod schemas matching backend DTOs

4. **Replace Drizzle for BetterAuth** (Day 5-6):
   - BetterAuth supports custom database adapters
   - Implement `postgres` adapter using `postgres` npm package (already installed)
   - Define minimal BetterAuth schema via raw SQL migrations
   - Remove Drizzle dependency from frontend

5. **Fix API Integration** (Day 7):
   - Generated types ensure frontend/backend DTOs match exactly
   - Add missing proxy routes
   - Fix ingredient dropdown overflow (same portal approach as H1)

6. **Redis + Security** (Day 8-9):
   - Same Redis queue + caching as H1
   - Security hardening

7. **Testing** (Day 10):
   - Verify all API calls use generated types
   - Run E2E tests to ensure no regressions
   - Check bundle size reduction (Drizzle removed)

### Expected Capability
- **Zero schema drift**: Frontend types auto-generated from backend OpenAPI spec
- **Type safety**: DTOs match exactly (no PascalCase/camelCase issues if OpenAPI configured correctly)
- **Zod validation matches backend**: Generated from same OpenAPI spec that documents backend validators
- **Smaller frontend bundle**: Drizzle ORM removed (~50KB savings)
- **Backend as authority**: Schema changes always start in backend, flow to frontend

## 2. The Validation (Run-Time)

### Plausibility Assessment

| Filter | Score | Justification |
|--------|-------|---------------|
| **Simplicity** | Medium | Removes Drizzle but adds OpenAPI dependency |
| **Explanatory Power** | High | Backend schema authority eliminates dual-schema problem |
| **Consistency** | Medium | OpenAPI may not capture all EF Core nuances |
| **Falsifiability** | High | Easy to test if generated types match DTOs |

**Plausibility Verdict:** PLAUSIBLE (radical but technically sound)

### Assumptions to Verify
- [ ] BetterAuth works with raw Postgres adapter (not just Drizzle/Prisma)
  - **Performer:** AI Agent (BetterAuth docs research)
- [ ] OpenAPI spec includes all validation rules from FluentValidation
  - **Performer:** Developer (check Swagger output)
- [ ] Frontend doesn't need complex database queries (joins, aggregations)
  - **Performer:** Developer (audit current Drizzle usage)
- [ ] OpenAPI → Zod conversion preserves all validation rules
  - **Performer:** Developer (test with sample schema)

### Required Evidence
- [ ] **Internal Test:** BetterAuth custom adapter works with `postgres` package
  - **Performer:** Developer (prototype)
- [ ] **Internal Test:** Generated Zod schemas reject same inputs as FluentValidation
  - **Performer:** Unit tests
- [ ] **Research:** BetterAuth custom adapter examples
  - **Performer:** AI Agent
- [ ] **Research:** OpenAPI → Zod code generation tools comparison
  - **Performer:** AI Agent

## Falsification Criteria
- If BetterAuth requires Drizzle/Prisma (no raw adapter support) → approach blocked
- If OpenAPI spec doesn't include validation metadata → Zod schemas incomplete
- If frontend needs complex queries BetterAuth schema can't provide → raw SQL too limiting
- If generated types are wrong due to OpenAPI limitations → backend not sufficient source

## Estimated Effort
**10 days** (solo developer, full-time)
- OpenAPI setup: 1 day
- Type generation: 2 days
- Zod generation: 1 day
- BetterAuth adapter: 2 days
- API integration fixes: 1 day
- Redis + security: 2 days
- Testing: 1 day

## Weakest Link
**BetterAuth custom adapter** - If BetterAuth doesn't support custom database adapters or requires specific ORM features, this approach fails. The library may be tightly coupled to Drizzle/Prisma.

**Secondary risk**: OpenAPI spec may not include all FluentValidation rules (e.g., custom validators, conditional validation). Would need to extend OpenAPI generation or add manual Zod overrides.
