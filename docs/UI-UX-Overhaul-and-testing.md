# MacroChef Frontend UI/UX Overhaul + E2E Testing

## Context

The MacroChef frontend has a solid 8/10 design foundation but suffers from:
- **Inconsistent patterns**: `DeleteConfirmModal` uses `gray-*` while everything else uses `slate-*`, mixed `rounded-lg`/`rounded-2xl`, inline SVG spinners copy-pasted everywhere
- **Zero test coverage**: No Playwright, no Vitest, no test scripts - Docker infra prepared but empty
- **CI gap**: Pipeline only runs lint+build for frontend, no tests
- **Missing UX polish**: Non-functional social login buttons, no debouncing on search, no body scroll lock on mobile menu, no focus traps

Goal: Apply premium website strategy, visual web development patterns, and comprehensive E2E testing across the entire frontend, then integrate into CI.

---

## Phase 1: Testing Infrastructure Setup

Install dependencies, create configs, add scripts.

**New files:**
- `frontend/playwright.config.ts` - Playwright config (chromium + mobile-chrome projects, `baseURL: http://localhost:3000`, `testDir: ./e2e`, screenshots on failure, trace on retry, `webServer` to auto-start Next.js)
- `frontend/e2e/fixtures/auth.fixture.ts` - Extended test fixture with `authenticatedPage` (creates test user via API, signs in, stores cookies)
- `frontend/e2e/fixtures/api.fixture.ts` - API helper for test data setup (createRecipe, createIngredient, etc.)
- `frontend/e2e/helpers/selectors.ts` - Shared `data-testid` constants

**Modified files:**
- `frontend/package.json` - Add scripts (`test:e2e`, `test:e2e:ui`, `test:e2e:headed`) + devDependencies (`@playwright/test`)
- `frontend/.gitignore` - Add `test-results/`, `playwright-report/`, `.auth/`

---

## Phase 2: Design System Consolidation

Fix all inconsistencies before touching pages. Pure cleanup, no visual redesign.

**Modified files:**
- `frontend/app/globals.css` - Add `.btn-danger` (red), `.btn-ghost` (transparent), `.btn-sm`/`.btn-lg` size variants, `.input-error` (red ring), `@keyframes fadeIn` (missing, referenced in NavbarContent)
- `frontend/components/DeleteConfirmModal.tsx` - Replace all `gray-*` with `slate-*`, `rounded-lg` with `rounded-2xl`, align with design system card/button patterns
- `frontend/components/Navbar/NavbarContent.tsx` - Add body scroll lock when mobile menu open, add `data-testid` attributes

**New files:**
- `frontend/components/ui/spinner.tsx` - Shared spinner component (replace ~8 inline SVG copies across login, register, recipe add, trainers, etc.)

---

## Phase 3: Landing Page Enhancement (Premium Strategy)

Apply conversion-optimized patterns for unauthenticated visitors.

**Modified files:**
- `frontend/app/page.tsx` - Enhance hero with animated gradient background, add trust indicators ("AI-Powered" badge already exists, add user count placeholder), add 3-column feature showcase section (Track Nutrition / Plan Meals / Get Coached), add testimonial section (3 static cards), add final conversion CTA section with gradient background. For authenticated users: add "Welcome back, {name}" greeting with slideUp animation on stats.
- `frontend/app/globals.css` - Add `@keyframes gradient-shift` for hero background animation, stagger animation delays

**New files:**
- `frontend/components/Landing/FeatureSection.tsx` - Three-column feature cards with icons
- `frontend/components/Landing/TestimonialCard.tsx` - Testimonial avatar + quote card
- `frontend/components/Landing/CTASection.tsx` - Bottom conversion CTA block

---

## Phase 4: Auth Pages Polish

Build trust, remove broken elements, improve validation UX.

**Modified files:**
- `frontend/app/(auth)/login/page.tsx` - Remove non-functional social login buttons + "Or continue with" divider, add password visibility toggle, replace inline spinner with `<Spinner />`, add `data-testid` attributes
- `frontend/app/(auth)/register/page.tsx` - Remove social login buttons, add password strength indicator (weak/medium/strong bar), add terms/privacy checkbox, replace inline spinner, add testids
- `frontend/app/(auth)/forgot-password/page.tsx` - Replace spinner, add testids
- `frontend/app/(auth)/reset-password/page.tsx` - Same cleanup pass

**New files:**
- `frontend/components/PasswordInput.tsx` - Reusable password field with visibility toggle + optional strength meter

---

## Phase 5: Dashboard Layout & Navigation

Improve nav for authenticated users. Keep top-nav (no sidebar - would be overengineered for this app's link count).

**Modified files:**
- `frontend/components/Navbar/NavbarContent.tsx` - Add more authenticated nav links (Meals, Meal Plan), add user dropdown menu (Profile, Goals, Workouts, Achievements, Body Measurements), improve mobile menu animation (slide-in, backdrop overlay, body scroll lock), add `aria-expanded`/`aria-controls`, add testids
- `frontend/app/(dashboard)/layout.tsx` - Add breadcrumb support or page header context area

---

## Phase 6: Feature Pages Enhancement

Apply consistent patterns across all dashboard pages. Add `data-testid` attributes everywhere.

**Tier 1 (high traffic):**
- `frontend/app/(dashboard)/recipes/page.tsx` - Fix dark mode on headings, add testids
- `frontend/app/(dashboard)/recipes/add/page.tsx` - Add debouncing to ingredient search, replace spinner, add testids
- `frontend/app/(dashboard)/meals/page.tsx` - Consistent dark mode, add testids
- `frontend/app/(dashboard)/meal-plan/page.tsx` - Fix dark mode on stat cards, add testids

**Tier 2 (medium traffic):**
- `frontend/app/(dashboard)/ingredients/page.tsx` - Add testids
- `frontend/app/(dashboard)/profile/page.tsx` - Replace inline modals with Dialog, add testids
- `frontend/app/(dashboard)/goal/page.tsx` - Consistent patterns
- `frontend/app/(dashboard)/trainers/page.tsx` - Add debouncing to search, add testids

**Tier 3 (lower traffic, lighter touch):**
- `frontend/app/(dashboard)/body-measurements/page.tsx`
- `frontend/app/(dashboard)/achievements/page.tsx`
- `frontend/app/(dashboard)/workouts/page.tsx`
- `frontend/app/(dashboard)/exercises/page.tsx`
- `frontend/app/admin/*` pages - consistency pass only

**New files:**
- `frontend/lib/hooks/useDebounce.ts` - Generic debounce hook for search inputs

---

## Phase 7: E2E Tests (~20 tests)

**Test files:**
- `frontend/e2e/landing.spec.ts` (2 tests) - Hero renders, CTA links work, feature sections visible
- `frontend/e2e/auth.spec.ts` (4 tests) - Login valid/invalid, register, forgot password
- `frontend/e2e/navigation.spec.ts` (2 tests) - Desktop nav links, mobile hamburger menu
- `frontend/e2e/recipes.spec.ts` (4 tests) - List view, create recipe, view detail, collections
- `frontend/e2e/meals.spec.ts` (3 tests) - View page, navigate days, delete meal
- `frontend/e2e/meal-plan.spec.ts` (2 tests) - View plans, navigate to create
- `frontend/e2e/ingredients.spec.ts` (2 tests) - List view, search filter
- `frontend/e2e/profile.spec.ts` (2 tests) - View profile, edit profile

---

## Phase 8: CI/CD Integration

**Modified files:**
- `.github/workflows/ci.yml` - Add `frontend-e2e` job that:
  1. Sets up PostgreSQL + Redis services
  2. Builds and starts backend
  3. Builds and starts frontend
  4. Installs Playwright chromium
  5. Runs `bun run test:e2e -- --project=chromium`
  6. Uploads `playwright-report/` as artifact on failure

---

## Execution Order

```
Phase 1 (test infra) -> Phase 2 (design system) -> Phase 3 (landing) ->
Phase 4 (auth) -> Phase 5 (nav) -> Phase 6 (features) ->
Phase 7 (e2e tests) -> Phase 8 (CI)
```

## Verification

After each phase:
- `bun run build` must pass (no type errors)
- `bun run lint` must pass
- Visual check in browser (dev server)

After Phase 7:
- `bun run test:e2e` must pass locally with backend running

After Phase 8:
- Push to a PR branch, verify CI pipeline runs E2E tests

## Scope Summary

| Phase | New Files | Modified Files |
|-------|-----------|----------------|
| 1. Test Infra | 5 | 2 |
| 2. Design System | 1 | 3 |
| 3. Landing Page | 3 | 2 |
| 4. Auth Pages | 1 | 4 |
| 5. Dashboard Nav | 0 | 2 |
| 6. Feature Pages | 1 | ~14 |
| 7. E2E Tests | 8 | 0 |
| 8. CI/CD | 0 | 1 |
| **Total** | **~19** | **~28** |
