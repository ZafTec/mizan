# Test & Infra Baseline (2026-07-07)

State the implementing agent starts from, established by actually running everything on 2026-07-07 (`master` @ `d154eea`, Windows host, Docker Desktop).

## Backend tests (Docker, `docker-compose --profile test up test`)

**Result: 219 total, 217 passed, 2 failed, ~51s.** Testcontainers-style isolation via the `mizan_test` database works.

The 2 failures are STALE TESTS, not product bugs. The meal-type widening (DEEP_DIVE §3 Spec 1 step 0) landed in the product (`Mizan.Domain/Constants/MealTypes.cs`: BREAKFAST, LUNCH, DINNER, SNACK, DRINK, MEAL) but these tests still assert the old 3-type validator:

| Test | Failure |
|---|---|
| `Mizan.Tests.Application.CreateFoodDiaryEntryCommandTests.Validator_ShouldFail_WhenInvalidMealType` | Expected IsValid false, got true (its "invalid" sample is now valid) |
| `Mizan.Tests.Application.LogFoodCommandTests.Validator_ShouldFail_WhenInvalidMealType` | Expects error text "breakfast, lunch, dinner, or snack"; actual message lists the new set |

**Phase 0 action:** update both tests to use a genuinely invalid meal type (e.g. `"BRUNCH"`) and assert against the new message.

Note: the test run also logs intentional MCP tool failure-path noise (`Tool failed: ...`) — those are passing tests exercising error paths, not failures.

## Frontend

- `bun run lint` (ESLint 9 flat config): **clean, exit 0**.
- **There is no frontend test infrastructure.** `package.json` has no `test`/`test:e2e` script; no Vitest, no Playwright, no Testing Library installed. CLAUDE.md's testing section is aspirational — treat it as the spec for what Phase 0 adds, not as something that exists.
- `bun run codegen` = single `openapi-typescript` script (no separate `codegen:types`/`codegen:zod` as CLAUDE.md claims; no Zod generation exists).

## Infra

- `docker compose up -d postgres redis`: both healthy (postgres:18, redis:7 per compose).
- Compose warns about unset `NEXT_PUBLIC_CLOUDINARY_*`/`CLOUDINARY_API_SECRET` (blank-string defaults). Non-fatal.
- Weak-secret fallbacks fire silently on `docker compose up` without a populated `.env` — see security finding S3; after R3 lands the compose will fail fast instead.
- Backend + frontend images build from compose (backend rebuilt today; frontend Next production build is the slow step).

## Browser smoke (live pass via Claude-in-Chrome against http://localhost:3000)

Full stack up (`docker compose up -d --build backend frontend`; backend dev container compiles at boot, allow ~90s before `/health` returns 200; frontend `/api/health` 503s until the backend is reachable).

- `/workouts`: renders, History tab, "No workouts yet", "Log a workout" CTA works. Tabs are client-state only (no `?tab=` deep link, confirming DEEP_DIVE).
- `/exercises`: **"0 exercises" / "No exercises found"** with all category filters present. B1 (empty exercise catalog on fresh install) reproduced live; workout logging is impossible for a new user. This is the first thing Phase 2 fixes.
- Console: only a hydration-mismatch warning caused by the Grammarly browser extension injecting `data-gr-*` attributes (not an app bug). No app console errors on the visited pages.
- Network: no failed `/api/*` requests on the visited pages.
- Backend container logs surface NuGet audit warnings at build: **MessagePack 2.5.187 (multiple HIGH advisories), Microsoft.OpenApi 2.4.1 (HIGH), OpenTelemetry 1.15.2 (moderate)**. Recorded in SECURITY-FINDINGS.md; bump in Phase 0/1.

## Known-red items the first implementation PR should NOT be blamed for

1. The 2 stale meal-type tests (above).
2. FluentAssertions license banner in test output (Xceed community license notice) — noise, but flags a future licensing decision: pin FluentAssertions <8 or budget a license; commercial use of newer versions requires a subscription.
3. Compose env warnings for Cloudinary vars.
