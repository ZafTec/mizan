# Session Handoff — Paddle Billing + Full-App QA Sweep

**Branch:** `paddle.integration` | **Date:** 2026-07-01

## What was done

1. **Paddle billing integration** (subscriptions: Pro Monthly $1.99/mo, Pro Yearly $15/yr w/ 7-day trial, Lifetime $48 one-time). Backend entitlement resolution, webhook ingestion with signature verification, Paddle.js checkout overlay, billing page, Pro gates on AI/analytics endpoints and free-tier quotas (meal plans, shopping lists, household invites).
2. **Live end-to-end proof**: real sandbox checkout → real webhook → real DB write → UI flips to Pro, using an ngrok tunnel (Paddle rejects `localhost` as a checkout domain and requires a separate "Default Payment Link" account setting).
3. **UI polish**: Pro badges/glow across nav, dashboard greeting, dropdown; upgrade banners on dashboard/meal-plan for free users; premium gradient billing card for Pro users; Pro upsell gate on the goal trends dashboard.
4. **Full-app QA sweep** (explicit user request: "test every freaking thing"), page by page via Chrome browser automation, logged in as a real Pro trial account.

## Approach

- Live-tested every route by navigating in a real browser (not just reading code), screenshotting, and checking console/network for errors — this is how the real bugs below were actually caught, not via static review.
- When something looked broken, root-caused it before touching code: read backend logs (`backend/logs/mizan-errors-*.log`), queried the DB/API directly via `javascript_exec` fetch calls, checked EF Core's actual generated-query error rather than guessing.
- Fixed bugs at the source (CSS cascade layer, LINQ query shape, missing guard) rather than patching symptoms.
- Committed each logical fix separately, one-line messages, no AI attribution, matching existing repo convention (`git log --oneline` style).

## Bugs found and fixed this session

| Bug | Root cause | Fix |
|---|---|---|
| Household invite email input collapsed to ~34px, unusable | `.input`'s `w-full` in `globals.css` was **unlayered** CSS (outside any `@layer`), which Tailwind v4 gives higher priority than the `utilities` layer — so `sm:w-40` on a sibling `<select>` never won | Wrapped `.input` in `@layer components` |
| `GET /api/Households/{id}/invitations` 500'd every time | EF Core can't translate `.OrderByDescending()` applied *after* a `Join`-based DTO projection | Moved `.OrderByDescending()` before the `.Join()` |
| `/profile/settings` hard-crashed (whole page, not just avatar upload) when Cloudinary env vars weren't set | Missing the `hasCloudinary` guard that `recipes/add/page.tsx` already uses correctly | Added the same guard around `<CldUploadWidget>` |
| Paddle webhook 500 on concurrent `subscription.created` + `subscription.trialing` | Unique index race on `subscriptions.user_id` | Catch-detect-retry using provider-agnostic `DbException.SqlState` (Application layer has no Npgsql reference) |
| OTLP crash on every docker boot | `${VAR:-}` compose substitution yields `""` not `null`; `new Uri("")` throws | Changed null checks to `IsNullOrWhiteSpace` |
| Free users got 403 loading meals/body-measurements pages | `GET /api/Goals/history` was wrongly `RequirePro`-gated, but free pages consume it for goal reference lines (only `/api/Goals/progress`, the actual trends dashboard, should be gated) | Removed the gate from `/history`, left `/progress` gated |
| Subscription quantity stepper on checkout | Paddle prices created without explicit `quantity` object, defaulting to 1–100 | Set `quantity: { minimum: 1, maximum: 1 }` on all three prices via Paddle API |

## Confirmed working, not touched

Dashboard, AI Coach, Goals, Billing, Meal Plan, Recipes, Foods, Body Measurements, Achievements, Trainers, Habits, Notifications, Profile, Community/Feed, Messaging. A few pages show intentional "coming in v2" placeholders (Notifications, Community) — that's by design, not a bug.

## Known gap, not fixed (needs a product decision)

**Workouts is a dead end for any fresh account.** The exercise library is completely empty (no seed data) and there is no UI to create a custom exercise, even though the backend already supports it (`POST /api/Exercises`, no auth restriction beyond being logged in — `frontend/data/exercise.ts` has `createExercise()` defined but it's never called from any component). Someone needs to either seed a global exercise library or build a "create exercise" form before this feature is usable.

## Environment notes for whoever picks this up

- Local dev this session ran **hybrid**: Postgres/Redis/frontend in Docker, backend via host `dotnet run` (avoids the cross-platform `obj`/`bin` corruption that happens when mixing host and container builds on the same bind-mounted folder — don't run `dotnet build`/`dotnet ef` on the host while the docker backend container is the active one, and vice versa).
- Docker Turbopack dev server on Windows bind mounts is unreliable about picking up file changes — if a fix "doesn't seem to apply," do a hard reload (`Ctrl+Shift+R`) first (dev-mode chunk URLs aren't content-hashed, so the browser can serve a stale cached JS chunk even after the server recompiles), then `docker restart mizan-frontend` if that doesn't work.
- `docker-compose.yml` has env-var indirection (`PUBLIC_APP_URL`, `PUBLIC_API_URL`, `SERVER_API_URL`, `ALLOWED_DEV_ORIGINS`) added to support testing through an ngrok tunnel for Paddle checkout. All default back to plain `localhost` values, so this is backward compatible but could use a cleanup pass once billing testing is fully done.
- Shared Postgres DB between Drizzle (auth, frontend-owned) and EF Core (business, backend-owned) — this was respected throughout; no cross-schema migrations were touched.

## Uncommitted at end of session

Reviewed and committed in logical groups (see git log). If you're reading this before that finished, run `git status` and `git log --oneline -15` to see where it landed.
