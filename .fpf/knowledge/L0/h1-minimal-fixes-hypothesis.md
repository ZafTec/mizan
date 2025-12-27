---
id: h1-minimal-fixes
type: hypothesis
created: 2025-12-19T00:30:00Z
problem: "Architecture review: validation, security, schema duplication, Redis, UI bugs"
status: L0
formality: 5
novelty: Conservative
complexity: Low
author: Claude (generated), Human (to review)
scope:
  applies_to: "Current architecture with minimal refactoring"
  not_valid_for: "Projects requiring automated schema sync or full type safety"
  scale: "Solo developer, <10k users, low-maintenance requirement"
---

# Hypothesis: Minimal Tactical Fixes (Conservative)

## 1. The Method (Design-Time)

### Proposed Approach
Accept the dual-schema architecture as intentional separation of concerns. Fix immediate bugs and add missing validation/features with minimal structural changes. Focus on quick wins that improve security and UX without requiring architectural overhaul.

### Rationale
- Solo developer has limited time for large refactors
- Current architecture works for the scale target (1k-10k users)
- Tactical fixes reduce risk compared to architectural rewrites
- Zod and FluentValidation can coexist if manually synchronized via documentation
- BetterAuth + Drizzle on frontend is intended (manages auth schema independently)

### Implementation Steps

1. **Fix Critical Bugs** (Day 1):
   - Add `/api/Recipes/:path*` to Next.js rewrites in `next.config.ts`
   - Fix property casing mismatch: Create DTO mappers in `apiClient` to convert PascalCase → camelCase
   - Fix ingredient dropdown overflow: Change parent card from `overflow-hidden` to `overflow-visible` + use portals for dropdown

2. **Add Zod Validation** (Day 2-3):
   - Create Zod schemas mirroring backend validators in `frontend/lib/validations/`
   - Add React Hook Form integration with Zod resolvers
   - Wire up validation to all forms (recipe add, ingredient add, meal plan, etc.)
   - Display inline error messages using existing helper patterns

3. **Security Hardening** (Day 4):
   - Add rate limiting to API routes using `upstash/ratelimit` (Redis-backed)
   - Add CSRF token validation to form submissions
   - Set `SameSite=Lax` and `Secure` flags on BetterAuth cookies
   - Hide `/health` endpoint details behind auth check or remove sensitive data

4. **Redis Expansion** (Day 5):
   - Create BullMQ queue for AI suggestion tasks (using existing Redis instance)
   - Add Redis caching layer for frequently accessed recipes/meals (TTL: 5 minutes)
   - Cache ingredient search results (TTL: 1 hour)

5. **Documentation** (Day 6):
   - Create `ARCHITECTURE.md` documenting dual-schema intentionality
   - Add schema sync checklist to `CONTRIBUTING.md`
   - Document API proxy routes and how to add new ones

### Expected Capability
- All forms validated client-side before submission
- Zero type mismatch errors between frontend and backend
- Ingredient dropdown works on all screen sizes
- AI suggestions processed asynchronously via Redis queue
- Recipe/ingredient lookups 50% faster with Redis cache
- Clear documentation prevents future schema drift

## 2. The Validation (Run-Time)

### Plausibility Assessment

| Filter | Score | Justification |
|--------|-------|---------------|
| **Simplicity** | High | No architectural changes, just additive fixes |
| **Explanatory Power** | Medium | Fixes immediate bugs but doesn't prevent future schema drift |
| **Consistency** | High | Compatible with current architecture and constraints |
| **Falsifiability** | High | Easy to test each fix independently |

**Plausibility Verdict:** PLAUSIBLE

### Assumptions to Verify
- [ ] Frontend team (solo dev) can maintain two schema definitions via documentation
  - **Performer:** Developer (via code review)
- [ ] Zod schemas won't drift from FluentValidation rules over time
  - **Performer:** Developer (manual sync required)
- [ ] Redis cache invalidation won't cause stale data issues
  - **Performer:** Integration tests
- [ ] Ingredient dropdown portal won't break mobile layouts
  - **Performer:** E2E tests (Playwright)

### Required Evidence
- [ ] **Internal Test:** Recipe submission works with new proxy route
  - **Performer:** Developer (curl test)
- [ ] **Internal Test:** Ingredient dropdown renders outside card boundaries
  - **Performer:** E2E test
- [ ] **Internal Test:** Zod validation rejects same inputs as FluentValidation
  - **Performer:** Unit tests
- [ ] **Research:** Best practices for React portals with Tailwind
  - **Performer:** AI Agent (docs search)

## Falsification Criteria
- If schema drift occurs within 2 months → manual sync approach failed
- If Redis cache causes data inconsistency bugs → caching strategy wrong
- If portal approach breaks mobile responsiveness → need different dropdown solution
- If Zod schemas require >10% maintenance overhead → duplication not sustainable

## Estimated Effort
**6 days** (solo developer, full-time)
- Bug fixes: 1 day
- Zod validation: 2 days
- Security: 1 day
- Redis: 1 day
- Documentation: 1 day

## Weakest Link
**Manual schema synchronization** - No tooling prevents frontend/backend schema drift. Relies entirely on developer discipline and documentation.
