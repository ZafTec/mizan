# Refocus Plan: Mizan is a Logging App

**Status:** proposed
**Branch:** `claude/cleanup-logging-refocus-rzfiv8`
**Rule:** decide what the project is about, build around it, delete the rest.

---

## 1. The decision

**Mizan logs three things: meals, lifts, body measurements. Everything else is
supporting cast or dead weight.**

Two interfaces, both first-class:

1. **The app** — open it, log the thing, close it. Sub-10-second interactions.
2. **The MCP server** — the same three logs, driven by an agent. This is the
   differentiator and it stays.

Everything that is not (a) capturing a log entry, (b) showing you what you
logged, or (c) comparing what you logged against a target, gets cut.

### What survives

| Domain | Why |
|---|---|
| Food diary (`FoodDiaryEntry`, `Food`) | meal logging |
| Workouts (`Workout`, `WorkoutExercise`, `ExerciseSet`, `Exercise`) | lift logging |
| Body measurements (`BodyMeasurement`) | measurement logging |
| Workout templates + drafts (`WorkoutTemplate`, `WorkoutDraft`) | starting and resuming a session *is* logging |
| Targets (`UserGoal`, collapsed) | a log with nothing to compare against is a spreadsheet |
| Streak (`Streak`) | the only gamification that serves logging consistency |
| MCP (`McpToken`, `McpUsageLog`, `Mizan.Mcp.Server`) | second interface |
| User | auth |

### What dies

| Domain | Reason |
|---|---|
| Recipes, recipe ingredients/instructions/nutrition/tags, favorites | meal-planning product, not logging |
| Meal plans, meal plan recipes | same |
| Shopping lists, shopping list items | same |
| Suggestions / AI recipe generation (`NutritionAiService`, `NutritionPlugin`, `/ai`, `/suggestions`) | MCP is the AI surface; a second one is redundant |
| Households, members, invitations, preferences | multi-tenancy nobody asked for; three tables and a permission model for zero users |
| Trainers, trainer-client relationships, chat, SignalR, ChatHub | a whole second product bolted on |
| Social: feed, follows, share links, reactions, comments, reports, moderation | a whole third product bolted on |
| Achievements, user achievements, achievement analytics, `/habits`, `/community` | engagement theater |
| Notifications (in-app bell, `Notification`, NotificationWriter) | nothing left to notify about |
| Subscriptions, Paddle webhooks, entitlements, `/billing`, Pro policy | self-hosted, one tier, no billing |
| Audit logs | admin feature for an admin panel that is also dying |
| Admin panel (all of `frontend/app/admin`, 4,376 LOC) | replaced by MCP admin tools + direct SQL |
| BetterAuth + Drizzle + the shared schema | see §3 |
| OpenTelemetry in the frontend (11 deps) | observability budget for a self-hosted app is `docker logs` |
| Cloudinary | its only consumer was recipe images |

**Replacement for recipes — "Saved Meals."** One entity (`SavedMeal` +
`SavedMealItem`): a named set of foods and quantities you log in one tap.
That is the 5% of recipes that served logging, at 2% of the cost. No
instructions, no tags, no nutrition rollup table, no sub-recipes, no circular
dependency validator.

---

## 2. Size of the cut

Measured, not guessed:

| Area | Now | After (est.) |
|---|---|---|
| `backend/Mizan.Api` | 2,905 | ~1,300 |
| `backend/Mizan.Application` | 11,322 | ~3,800 |
| `backend/Mizan.Domain` | 928 | ~450 |
| `backend/Mizan.Infrastructure` (non-migration) | 2,261 | ~1,400 |
| `backend/Mizan.Infrastructure` (migrations) | 38,218 | ~2,500 (one squashed baseline) |
| `backend/Mizan.Mcp.Server` | 1,878 | ~900 |
| `backend/Mizan.Tests` | 8,002 | ~3,000 |
| `frontend/app` | 20,381 | ~4,500 |
| `frontend/components` | 8,191 | ~1,800 |
| `frontend/types` | 6,976 | ~1,500 (regenerated) |
| `docs/` | 47 files | 3 files |

Roughly **~75% of the repo goes away.** Controllers 25 → 8. MCP tools 120 → ~35.
Frontend routes 60+ → 9.

---

## 3. Kill the shared schema

Today: BetterAuth owns `users`/`accounts`/`sessions`/`jwks`/`verification` via
Drizzle; EF Core owns everything else and treats `User` as read-only with a
comment block begging you not to touch it. Two ORMs, two migration tools, one
database, and a `frontend/db/schema.ts` that lies about which tables it owns.

**Target: ASP.NET Core owns the entire schema. Next.js owns zero tables.**

### Mechanism

- **ASP.NET Core Identity** with `AddIdentityApiEndpoints<MizanUser>()`. That
  ships `/register`, `/login`, `/refresh`, `/confirmEmail`, `/manage/info`
  out of the box. No hand-rolled auth.
- **Cookie auth, not JWT.** `mizan.euaell.me` and `api.mizan.euaell.me` share
  a parent domain, so a `Domain=.euaell.me; HttpOnly; Secure; SameSite=Lax`
  cookie works for both browser→frontend and browser→API. This deletes:
  `EdDsaJwtSignatureValidator`, `JwksProvider`, `JwksRefreshService`,
  `JwtAuthenticationExtensions`, `JwtOptions`, the Redis JWKS cache, and the
  entire `Jwt__*` config surface.
- **MCP tokens are unaffected** — `ApiKeyAuthenticationHandler` already runs as
  a separate scheme against the `McpToken` table. It stays exactly as is.
- **Password migration:** BetterAuth stores scrypt hashes in `accounts.password`.
  Ship a custom `IPasswordHasher<MizanUser>` that recognizes the BetterAuth
  format, verifies against it, and returns `SuccessRehashNeeded` so Identity
  transparently rehashes to its own format on first successful login. No forced
  password resets.
- **Dropped auth features:** magic link, `haveIBeenPwned`, `lastLoginMethod`,
  the `admin` plugin (impersonation, ban). Google OAuth can come back later via
  `AddGoogle()` if wanted; it is not in scope for the cut.
- **Next.js becomes a pure client.** `frontend/db/`, `drizzle.config.ts`,
  `drizzle-orm`, `drizzle-kit`, `better-auth`, `postgres`, `nodemailer`,
  `lib/auth.ts`, `lib/auth-client.ts`, `app/api/auth/[...all]`,
  `lib/redis.ts`, `lib/email.ts` all delete. `DATABASE_URL` leaves the frontend
  environment entirely.
- Email (verification, reset) moves to the backend via a single
  `IEmailSender` implementation.

### Migration strategy

13 EF migrations, 38k lines, most of them describing tables we are deleting.
Do not write a 2,000-line drop migration.

**Squash.** Delete `Data/Migrations/` entirely, generate one `InitialSchema`
against the new model, and ship a one-off `scripts/export-logs.mjs` that dumps
the three logs + foods + exercises + users to JSON from the old database and a
`scripts/import-logs.mjs` that loads them into the new one. Self-hosted, small
data, one operator — a clean baseline is worth far more than migration history
for tables that no longer exist.

---

## 4. The UI

The current UI is 60+ routes deep with features unreachable by design. Replace
it with nine routes.

```
/                      → redirect to /today (or landing if logged out)
/login  /register      → Identity endpoints, two forms, no ceremony
/today                 → THE APP. Today's meals, today's workout, today's weight.
                         Three sections, each with an inline "+" that opens a sheet.
                         Targets shown as thin progress bars, not donut charts.
/workout/active        → full-screen session: exercise, set, reps, weight, rest timer.
                         Autosaves to WorkoutDraft. The only route that isn't a list.
/history               → scrollable day list, jump-to-date. Tap a day → that day's /today.
/progress              → three charts: bodyweight trend, calories+protein trend,
                         per-exercise volume/e1RM. Nothing else.
/settings              → profile, targets, MCP tokens, data export, delete account.
```

Logging is a **sheet**, never a page navigation. Meal, measurement, and set entry
all open over the current context and dismiss back to it.

**Design direction:** dark-first, dense, mobile-first single column, large touch
targets on the numeric inputs, keyboard-navigable on desktop. One accent color.
No illustrations, no confetti, no celebration SVGs.

**Dependency cull:** drop `@opentelemetry/*` (7), `@paddle/paddle-js`,
`cloudinary`, `next-cloudinary`, `@microsoft/signalr`, `remixicon`,
`lucide-animated`, `motion`, `better-auth`, `@better-auth/infra`, `drizzle-orm`,
`drizzle-kit`, `postgres`, `nodemailer`, `@next/third-parties`. Keep
`lucide-react`, `recharts`, `date-fns`, `zod`, `sonner`, `tailwind-merge` and
the ~6 Radix primitives actually used. `components/ui` shrinks to button, input,
sheet, dialog, tabs, popover, checkbox, label.

Also delete: `components/Landing`, `components/illustrations`,
`components/gamification`, `components/trainer`, `components/Messaging`,
`components/ai`, `components/billing`, `components/consent`,
`components/Habits`, `components/SuggestedRecipes`, `components/Recipes`,
`components/RecipeOptions`, `components/MealPlanningCalendar`,
`components/AddIngredient`, `components/IngredientTable`,
`components/AddMealFromRecipe`, `.superdesign/`.

---

## 5. Backend consolidation

The CQRS layer has 54 command files and 41 query files, many holding a single
20-line handler. That is ceremony, not architecture.

Collapse to one file per log domain, each holding its commands, queries, DTOs
and validators:

```
Mizan.Application/
  Meals/        LogMeal, UpdateEntry, DeleteEntry, GetDay, GetRange, SearchFoods, SavedMeals
  Workouts/     StartSession, LogSet, FinishWorkout, GetWorkout, ListWorkouts, Templates, Draft
  Measurements/ LogMeasurement, ListMeasurements, DeleteMeasurement
  Targets/      SetTargets, GetTargets, GetProgress
  Foods/        CreateFood, UpdateFood, SearchFoods
  Exercises/    CreateExercise, UpdateExercise, ListExercises
  Account/      GetProfile, UpdateProfile, ExportData, McpTokens
```

Controllers: `MealsController`, `WorkoutsController`, `MeasurementsController`,
`TargetsController`, `FoodsController`, `ExercisesController`,
`AccountController`, `HealthController`. Eight, down from twenty-five.

Keep MediatR and the validation pipeline behavior — they earn their keep. Drop
`EntitlementService`, `TrainerAuthorizationService`, `AchievementEvaluator`,
`NotificationWriter`, `UserStatusService` (fold the ban check into Identity).

**Redis:** with SignalR and JWKS gone, Redis serves only ingredient-search
caching. Drop the dependency; Postgres full-text on a few thousand foods is
faster than the round trip. One less container.

---

## 6. MCP server

The strongest part of the codebase and it survives nearly intact — but it
currently exposes ~120 tools, most pointing at endpoints that are about to
disappear.

**Delete:** `RecipeTools` (241), `MealPlanTools` (111), `ShoppingListTools` (71),
`TrainerTools` (105), `HouseholdTools` (104), `AchievementTools` (71),
`SocialTools` (27), `NotificationTools` (18), `WorkoutTemplateTools` merged into
`WorkoutTools`, `AdminTools` reduced to food/exercise promotion.

**Keep and sharpen:** `MealTools`, `FoodTools`, `WorkoutTools`,
`BodyMeasurementTools`, `ExerciseTools`, `GoalTools` → `TargetTools`,
`NutritionTools`, `ProfileTools`.

Result: ~35 tools, all of them "log this" / "show me what I logged" / "how am I
tracking." A tool list an agent can actually hold in its head.

Add the one tool the current set is missing: `log_day` — a single call that
takes meals, sets, and a weight for a date. The natural agent interaction is
"here's my whole day," not fourteen round trips.

---

## 7. Repo hygiene

**Delete:**
- `docs/liftlog-integration/` — 21 markdown files + 27 SVGs of a completed integration
- `docs/ANALYSIS_2026-06-10.md`, `docs/DEEP_DIVE_2026-06-12.md`,
  `docs/SESSION_HANDOFF.md`, `docs/PADDLE_INTEGRATION_PLAN.md`,
  `docs/VPS_MONITORING_PATCHES.md` — dated session artifacts, not documentation
- `docs/API_REFERENCE.md` — Swagger is the API reference; a hand-maintained copy
  is a second source of truth that is already wrong
- `docs/DTO_CONTRACTS.md` — the contract is the generated types
- `SECURITY-FINDINGS.md` — a findings log, not a document; findings belong in
  issues or in the fix
- `AGENTS.md` — 11k of duplicate guidance; merge the non-overlapping parts into
  `CLAUDE.md`
- `.superdesign/` — design-tool scratch
- `docker-compose.prod.yml` Redis service; `--profile test` stays

**Keep, rewritten:**
- `README.md` — what it is (three logs + MCP), how to run it, nothing else. Cut from 11k to ~2k.
- `CLAUDE.md` — cut to ~6k: commands, architecture, the one rule. The MCP tool
  catalogue and the shared-schema warnings both become obsolete.
- `SECURITY.md` — keep as is.
- `docs/ARCHITECTURE.md` — rewritten for the single-schema world.
- `docs/MCP.md` — new: token setup, tool catalogue, example agent sessions.
  This is the feature worth documenting well.

---

## 8. Execution order

Each phase is one commit and leaves the build green.

| # | Phase | Risk | Notes |
|---|---|---|---|
| 0 | Docs + scratch purge | none | `docs/*`, `AGENTS.md`, `SECURITY-FINDINGS.md`, `.superdesign/` |
| 1 | Backend amputation | low | delete entities, commands, queries, controllers, tests, services for every domain in §1's kill list; DbContext trimmed; build must pass |
| 2 | MCP trim | low | delete dead tool classes, re-point survivors |
| 3 | Frontend amputation | low | delete routes, components, deps; `/today` is a stub |
| 4 | Auth to Identity | **high** | the one phase that touches live user data; scrypt-compat hasher + rehash-on-login; test against a prod DB copy before deploying |
| 5 | Schema unification | **high** | drop Drizzle, squash migrations, export/import scripts |
| 6 | Application-layer collapse | medium | CQRS consolidation, Redis removal, `SavedMeal` added |
| 7 | UI rebuild | medium | the nine routes, sheet-based logging, new design system |
| 8 | Docs rewrite | none | README, CLAUDE.md, ARCHITECTURE.md, MCP.md |

Phases 0–3 are pure deletion and can land in a day. Phases 4–5 are the real
work and want a database snapshot taken first. Phases 6–8 are the rebuild.

---

## 9. What this costs

Stated plainly so it is a decision and not a surprise:

- **Data loss on cut features.** Recipes, meal plans, shopping lists, social
  content, achievements, chat history are deleted, not archived. If any of it
  matters, the export script in phase 5 must be widened before phase 1 runs.
- **Auth downtime risk.** Phase 4 is the one place a mistake locks users out.
  Snapshot first, rehearse on a copy.
- **OAuth and magic-link login go away** until deliberately re-added.
- **Multi-user features go away entirely.** Households and trainers were the
  only reason this was a multi-tenant app. After the cut it is a single-user
  logging tool that happens to support multiple accounts.
- **The MacroChef identity goes away.** Without recipes and meal planning, the
  name no longer describes the product. It is Mizan, a logger.
