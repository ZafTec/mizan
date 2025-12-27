---
id: h4-hybrid-pragmatic
type: hypothesis
created: 2025-12-19T00:45:00Z
problem: "Architecture review: validation, security, schema duplication, Redis, UI bugs"
status: L0
formality: 6
novelty: Novel
complexity: Medium
author: Claude (generated), Human (to review)
scope:
  applies_to: "Projects accepting intentional schema separation with automated validation sync"
  not_valid_for: "Teams requiring zero manual schema maintenance"
  scale: "Solo developer seeking balance between pragmatism and automation"
---

# Hypothesis: Hybrid Pragmatic Approach (Novel Balanced)

## 1. The Method (Design-Time)

### Proposed Approach
Accept that **two schemas are intentional** (frontend owns auth schema via BetterAuth/Drizzle, backend owns business logic via EF Core), but automate the **validation layer** synchronization. Generate Zod schemas from backend OpenAPI spec, keep database schemas independent but add automated drift detection.

**Key Principles:**
1. **Backend owns business logic schema** (Recipes, Foods, Meals, etc.) → EF Core
2. **Frontend owns auth schema** (Users, Sessions, JWKS) → Drizzle (BetterAuth requirement)
3. **Validation is synchronized** → Zod generated from OpenAPI (FluentValidation metadata)
4. **Drift detection alerts** → CI job compares schema hashes, fails if diverged

### Rationale
- Recognizes BetterAuth requires ORM control (Drizzle/Prisma)
- Backend business logic naturally lives in C# domain models
- Validation is the critical layer that must match (schemas can differ in implementation)
- Automated drift detection catches manual schema changes before they cause bugs
- Pragmatic balance: automation where it matters most (validation), manual where necessary (schema)

### Implementation Steps

1. **Schema Boundary Definition** (Day 1):
   - Document in `ARCHITECTURE.md`:
     - **Frontend Schema Zone**: BetterAuth tables (users, sessions, accounts, jwks, verification)
     - **Backend Schema Zone**: Business entities (recipes, foods, meals, workouts, etc.)
     - **Shared Concern**: Households (linked by both, owned by backend)
   - Add schema governance rules

2. **OpenAPI → Zod Generation** (Day 2-3):
   - Configure Swashbuckle to include FluentValidation metadata in OpenAPI spec
   - Use `Swashbuckle.AspNetCore.Filters` + `FluentValidation.AspNetCore` integration
   - Install `@anatine/zod-openapi` or build custom generator
   - Generate `frontend/lib/validations/generated/` with Zod schemas for all DTOs
   - Add pre-build script: `npm run codegen:validation`

3. **TypeScript Type Generation** (Day 3):
   - Use `openapi-typescript` to generate `frontend/types/api.generated.ts`
   - Ensures DTOs match exactly (PascalCase → camelCase handled via config)
   - Add type-safe fetch wrapper using generated types

4. **Schema Drift Detection** (Day 4):
   - Create `scripts/check-schema-drift.ts`:
     - Reads backend EF Core migrations
     - Reads frontend Drizzle migrations
     - Compares shared table names (Households, HouseholdMembers)
     - Exits with error if columns/types mismatch
   - Add to CI pipeline (runs on PR)
   - Alerts developer if manual sync needed

5. **Fix Immediate Bugs** (Day 5):
   - Add `/api/Recipes/:path*` proxy route
   - Fix ingredient dropdown overflow (use Radix UI Dropdown or Headless UI)
   - Fix PascalCase/camelCase via type generation config

6. **React Hook Form Integration** (Day 6-7):
   - Install `react-hook-form` + `@hookform/resolvers`
   - Wrap all forms with RHF + Zod resolver
   - Add inline error display components
   - Example:
     ```typescript
     const schema = RecipeCreateDtoSchema // generated from OpenAPI
     const form = useForm({ resolver: zodResolver(schema) })
     ```

7. **Redis Expansion** (Day 8):
   - Add BullMQ for AI suggestion task queue
   - Add Redis cache layer for:
     - Ingredient search results (1 hour TTL)
     - Recipe detail (5 minute TTL, invalidate on update)
     - Meal plan weekly view (5 minute TTL)
   - Use cache-aside pattern with stale-while-revalidate

8. **Security Hardening** (Day 9):
   - Add `@upstash/ratelimit` for API routes (Redis-backed)
   - Add CSRF token to forms (use `csrf-csrf` package)
   - Configure BetterAuth cookies with `sameSite: "lax"`, `secure: true`
   - Hide sensitive health endpoint data, require auth

9. **Documentation & Testing** (Day 10):
   - Update `ARCHITECTURE.md` with schema zones
   - Add `SCHEMA_SYNC.md` with manual sync checklist
   - Add E2E tests for form validation
   - Add integration tests for Redis cache invalidation

### Expected Capability
- **Validation always in sync**: Zod matches FluentValidation (auto-generated)
- **Types always match**: DTOs generated from OpenAPI
- **Schema drift detected early**: CI alerts on divergence
- **Clear boundaries**: Developers know which schema owns which tables
- **All forms validated**: React Hook Form + Zod on every form
- **Redis optimized**: Task queue for AI, caching for hot paths
- **Security hardened**: Rate limits, CSRF, secure cookies

## 2. The Validation (Run-Time)

### Plausibility Assessment

| Filter | Score | Justification |
|--------|-------|---------------|
| **Simplicity** | Medium | More complex than H1, simpler than H2 |
| **Explanatory Power** | High | Addresses validation sync and schema boundaries explicitly |
| **Consistency** | High | Aligns with BetterAuth requirements and Clean Architecture |
| **Falsifiability** | High | Drift detection and validation tests provide clear pass/fail |

**Plausibility Verdict:** PLAUSIBLE (best balance of automation and pragmatism)

### Assumptions to Verify
- [ ] FluentValidation metadata can be extracted to OpenAPI spec
  - **Performer:** AI Agent (research Swashbuckle.AspNetCore.Filters)
- [ ] Schema drift detection can catch all meaningful divergences
  - **Performer:** Developer (test with intentional mismatch)
- [ ] React Hook Form doesn't conflict with existing form state management
  - **Performer:** Developer (prototype on one form)
- [ ] Redis cache invalidation strategy won't cause stale data
  - **Performer:** Integration tests

### Required Evidence
- [ ] **Internal Test:** OpenAPI includes FluentValidation rules (required, max length, etc.)
  - **Performer:** Developer (inspect Swagger UI)
- [ ] **Internal Test:** Generated Zod schemas validate correctly
  - **Performer:** Unit tests
- [ ] **Internal Test:** Schema drift detection catches intentional mismatch
  - **Performer:** CI pipeline test
- [ ] **Research:** Best practices for cache invalidation in Next.js + Redis
  - **Performer:** AI Agent

## Falsification Criteria
- If OpenAPI cannot include FluentValidation metadata → need manual Zod schemas
- If schema drift detection has false positives → tool too noisy
- If React Hook Form adds >30% to bundle size → alternative validation approach needed
- If Redis cache causes >5% stale data issues → caching strategy wrong

## Estimated Effort
**10 days** (solo developer, full-time)
- Schema boundaries: 1 day
- OpenAPI → Zod generation: 2 days
- Type generation: 1 day
- Drift detection: 1 day
- Bug fixes: 1 day
- React Hook Form: 2 days
- Redis: 1 day
- Security: 1 day

## Weakest Link
**OpenAPI metadata completeness** - If Swashbuckle cannot extract all FluentValidation rules to OpenAPI spec, the generated Zod schemas will be incomplete. May require custom OpenAPI generator or manual Zod overrides for complex validation rules (e.g., "password must match confirm password").

**Mitigation**: Start with simple validators (required, max length, min/max), verify completeness. Add manual Zod schemas for complex cross-field validation.
