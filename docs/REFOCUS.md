# Refocus Plan: Mizan is a Logging App

**Status:** proposed (rev 2)
**Branch:** `claude/cleanup-logging-refocus-rzfiv8`
**Rule:** decide what the project is about, build around it. Everything else
gets demoted, not deleted.

> **rev 2 changes the thesis.** Rev 1 proposed deleting recipes, billing,
> trainers, social, achievements, notifications and the admin panel. That was
> wrong. The features are not the problem — their *rank* is. This revision keeps
> them and subordinates them to logging. See §2 for the diagnosis that forced
> the change.

---

## 1. The decision

**Mizan logs three things: meals, lifts, body measurements.** That is the spine.
Two interfaces on it: the app, and the MCP server.

Everything else in the product is real and stays. It just stops competing with
the spine for attention. The organizing principle:

> **Nothing occupies permanent screen real estate until the user's own data
> gives it a reason to exist.**

---

## 2. The actual diagnosis

Rev 1 assumed the features were junk. They aren't. `AppShell.tsx` is:

```
Dashboard  →  Today, Meals, Habits
Food       →  Recipes, Meal Plan, Foods
Fitness    →  Workouts, Exercises, Body, Goals, Achievements
Community  →  AI Coach, Messages, Trainers, Feed, Notifications
Account    →  Profile, Household, Billing, Settings, Admin
```

**21 permanent nav items, all peer-level.** Meal Plan ranks equal to Achievements.
The mobile bottom bar is `Home | Meals | Train | AI | Me` — **"AI Coach" gets one
of five slots and body measurements gets none**, in an app whose reason to exist
is logging three things.

That is the entire "junky, features unavailable because of bad design" complaint,
and it is a **hierarchy bug, not a scope bug**. You cannot fix it by deleting
features; you fix it by ranking them. Deleting was the lazy read.

Consequence: the deletion budget drops from ~75% of the repo to ~30%, and the
center of gravity of this plan moves to two places — **navigation tiering (§4)**
and **the schema/auth unification (§6)**, which was always the highest-value
structural work and is unaffected by any of this.

---

## 3. Three tiers

### Tier 1 — The spine. Permanent, four slots and one action.

```
mobile bottom bar:   Today  ·  History  ·  ( + )  ·  Progress  ·  More
desktop left rail:   same five, vertical
```

`( + )` is the app. Tap → sheet → **Meal / Workout / Measurement**. It is the
largest touch target on screen and it is present on every route. Logging is
never a page navigation.

| Route | Contains |
|---|---|
| `/today` | today's meals, today's workout, today's weight, targets as thin bars |
| `/history` | day list, jump-to-date; tap a day → that day in the `/today` layout |
| `/progress` | bodyweight trend, calories+protein trend, per-exercise volume/e1RM |
| `/workout/active` | full-screen session, autosaves to `WorkoutDraft`. Not in nav — reached from `( + )` or the resume banner |
| `/more` | tier 3, below |

`/goal`, `/goal/dashboard` and `/goal/progress` fold into `/progress`.
`/dashboard` becomes `/today`.

### Tier 2 — Contextual. Zero permanent pixels; appears when data justifies it.

| Surface | Trigger |
|---|---|
| "Save as recipe" chip | a logged meal contains ≥3 foods (§5) |
| "Resume workout" banner | an open `WorkoutDraft` exists |
| Streak / achievement toast | unlock event, transient, never a permanent badge |
| Trainer strip on `/today` | an active `TrainerClientRelationship` exists |
| Household switcher in header | member of more than one household |
| Notification bell | unread count > 0 |
| Pro upsell | the moment a gated action is attempted — never as a standing banner |

This is the "subtle" half of subtle-yet-accessible. A user with no trainer, one
household and no unread notifications sees a screen with **nothing on it but
their own log**.

### Tier 3 — `/more`. One tap from anywhere, grouped, honest.

```
Food      Recipes · Meal Plans · Shopping Lists · Foods
Fitness   Exercises · Workout Templates
People    Feed · Trainers · Messages · Household
Account   Profile · Billing · Settings · MCP Tokens · Export
Admin     (role-gated)
```

Rule for this screen: **an entry with no data renders as a one-line "set this up"
row, never as an empty or broken page.** That is the other half of the "features
unavailable to users" complaint — several of these routes exist but dead-end.
Every tier-3 entry must be reachable in ≤2 taps and must work. Auditing all
~70 routes against that bar is a work item, not an assumption.

This is the "accessible" half: everything is two taps away and nothing costs a
pixel until asked for.

---

## 4. Recipes: one interface, both directions

Recipes stay. What goes is the separate recipe-authoring product. A recipe is
no longer something you sit down and write — **it is a byproduct of logging.**

### Direction 1 — Log → Recipe (promotion)

You log a meal from several foods. The day view groups them. A chip appears:
**"Save as recipe."** Name it, done. Ingredients and quantities come from what
you actually logged.

This is the **only** way a recipe is created. `/recipes/add` and
`/recipes/[recipeId]/edit` as standalone form pages are deleted. Editing a
recipe means logging it again and re-promoting, or adjusting quantities inline.

### Direction 2 — Recipe → Log (reuse)

Recipes appear in the **same picker as foods** — one search field, foods and
recipes interleaved, recents and pinned first. Logging a recipe expands it into
its component entries under one collapsible group. Log-from-recipe marks it used,
which floats it up the picker. That is the "logs a meal from a recipe, it is
saved" behavior.

### Model simplification

| Now | After |
|---|---|
| `RecipeInstruction` table (ordered rows) | one nullable `Instructions` text column |
| `RecipeNutrition` table | computed from ingredients on read, never stored |
| `RecipeTag` | dropped — the picker sorts by recency and pins, not tags |
| `RecipeIngredient.SubRecipeId` + `RecipeCircularDependencyValidator` | dropped; recipes do not nest |
| `FavoriteRecipe` | kept, reframed as a pin that boosts picker rank |

Four tables and a graph-cycle validator collapse into a recipe plus its
ingredients. `RecipeCircularDependencyValidatorTests` goes with it.

---

## 5. Billing stays, and stays out of the way

Paddle, `Subscription`, `PaddleWebhooksController`, `EntitlementService` and the
`RequirePro` policy all stay as built. The integration works; it is revenue.

Today Pro gates exactly three endpoints:

- `POST /api/Nutrition/ai/chat`
- `POST /api/Nutrition/ai/analyze-image`
- `GET /api/Goals/progress`

That is a thin tier for a paid plan, and `/billing` currently sits in the nav as
a peer of Workouts — the worst of both worlds: prominent and unconvincing.

**Change:** billing moves to tier 3 (`/more → Billing`). The Pro *upsell* moves
to tier 2 — it appears at the moment a gated action is attempted, in context,
with the specific thing being unlocked named. A standing "Upgrade" nav item
converts worse than an in-context wall and costs a permanent slot.

Widening what Pro gates is a product decision, out of scope here. Flagging it:
three endpoints, two of which are AI features, is not a plan.

---

## 6. Kill the shared schema (unchanged from rev 1 — still the main event)

Today: BetterAuth owns `users`/`accounts`/`sessions`/`jwks`/`verification` via
Drizzle; EF Core owns everything else and treats `User` as read-only with a
comment block begging you not to touch it. Two ORMs, two migration tools, one
database, and a `frontend/db/schema.ts` that documents tables it does not own.

**Target: ASP.NET Core owns the entire schema. Next.js owns zero tables.**

- **ASP.NET Core Identity** via `AddIdentityApiEndpoints<MizanUser>()` — ships
  register/login/refresh/confirmEmail/manage. No hand-rolled auth.
- **Cookie auth, not JWT.** `mizan.euaell.me` and `api.mizan.euaell.me` share a
  parent domain, so `Domain=.euaell.me; HttpOnly; Secure; SameSite=Lax` covers
  browser→frontend and browser→API. Deletes `EdDsaJwtSignatureValidator`,
  `JwksProvider`, `JwksRefreshService`, `JwtAuthenticationExtensions`,
  `JwtOptions`, the Redis JWKS cache and the whole `Jwt__*` config surface.
- **MCP tokens untouched.** `ApiKeyAuthenticationHandler` already runs as a
  separate scheme over `McpToken`.
- **Password migration:** BetterAuth stores scrypt hashes in `accounts.password`.
  A custom `IPasswordHasher<MizanUser>` verifies the BetterAuth format and
  returns `SuccessRehashNeeded`, so Identity rehashes transparently on first
  successful login. Nobody resets a password.
- **Dropped auth features:** magic link, `haveIBeenPwned`, `lastLoginMethod`,
  the BetterAuth `admin` plugin (impersonation/ban → Identity lockout + a role
  claim). Google OAuth returns later via `AddGoogle()` if wanted.
- **Next.js becomes a pure client.** `frontend/db/`, `drizzle.config.ts`,
  `drizzle-orm`, `drizzle-kit`, `better-auth`, `@better-auth/infra`, `postgres`,
  `nodemailer`, `lib/auth.ts`, `lib/auth-client.ts`, `app/api/auth/[...all]`,
  `lib/email.ts` all go. `DATABASE_URL` leaves the frontend environment.
- Email moves to the backend behind one `IEmailSender`.

**Redis stays.** Rev 1 dropped it on the assumption SignalR was going. SignalR
stays, so the backplane stays, and `HybridCache`'s L2 (used by
`EntitlementService` and `UserStatusService`) stays with it.

### Migrations

13 migrations, 38,218 lines. Most of it describes tables that survive, so rev 1's
squash argument is weaker now — but the history still encodes a two-ORM world
and a `users` table this codebase was forbidden to touch.

**Squash anyway, at phase 7 only.** Delete `Data/Migrations/`, generate one
`InitialSchema` against the unified model, and ship `scripts/export-data.mjs` /
`scripts/import-data.mjs` covering **every surviving table**, not just the three
logs — that list is now much longer than rev 1 assumed. Take a database snapshot
before running it.

---

## 7. Storage: Cloudinary → `IStorageService` now, S3 in v2

Do not delete image handling — rev 1 had this wrong; it was only ever there for
recipe images, and recipes stay.

Introduce `IStorageService` with `UploadAsync` / `DeleteAsync` / `GetUrl`, back
it with the existing Cloudinary implementation in v1, and swap in an S3
implementation in v2 with no call-site changes. Small, and it makes the v2 swap
a one-file change instead of a grep.

Consumers to route through it: recipe images, profile avatars, and
`POST /api/Nutrition/ai/analyze-image`. Drop `next-cloudinary` from the frontend
— uploads go to the backend, which signs and stores; the frontend never talks to
a storage provider directly. That also deletes `app/api/sign-cloudinary-params`.

---

## 8. What actually gets deleted

Short list now, and honest about it:

**Routes and features**
- `/community` — a 5-line stub
- `/suggestions`, `/suggestions/regenerate` — AI recipe suggestion, redundant
  against both AI Coach and MCP
- `/habits` — 194 LOC that restates streaks
- `/goal`, `/goal/dashboard`, `/goal/progress` — folded into `/progress`
- Recipe authoring pages: `/recipes/add`, `/recipes/[recipeId]/edit` (§4)
- Frontend OpenTelemetry: 7 packages + `instrumentation.ts`. **The backend and
  the MCP server are both fully instrumented** (6 OTel packages, OTLP +
  Prometheus exporters) — the frontend layer duplicates that for Next.js SSR
  spans nobody reads. Backend telemetry stays untouched. *Flagging this one:
  it's my call, not yours — say so and I'll keep it.*

**Recipe sub-tables** — `RecipeInstruction`, `RecipeNutrition`, `RecipeTag`,
`SubRecipeId` + the cycle validator (§4)

**Admin panel — halved, not deleted.** 19 routes / 4,376 LOC. Keep the
table-driven screens where a UI genuinely beats a tool call: users, ingredients,
recipes, moderation. Drop the ones the MCP admin tools already do better:
achievements CRUD + analytics, audit-log browsing, households, relationships,
sessions. Roughly 2,000 LOC out, and admin work moves toward the interface you
already like.

**Naming, not deletion.** These read as duplicates in the route list but are
distinct screens and all four stay — they need names, not the axe:
`/meal-plan/add` (add *to* a plan) vs `/meal-plan/create` (create a plan);
`/trainer` (trainer-side dashboard) vs `/trainers` (client-side browse — rename
to `/coach` and `/trainers`); `/verify` (token handler) vs `/verifyemail` (the
UI).

**Repo docs** — unchanged from rev 1: delete `docs/liftlog-integration/` (21 md
+ 27 SVG), the dated session artifacts (`ANALYSIS_2026-06-10`,
`DEEP_DIVE_2026-06-12`, `SESSION_HANDOFF`, `PADDLE_INTEGRATION_PLAN`,
`VPS_MONITORING_PATCHES`), `API_REFERENCE.md` (Swagger is the reference),
`DTO_CONTRACTS.md` (the generated types are the contract), `SECURITY-FINDINGS.md`,
`.superdesign/`. Merge `AGENTS.md` into `CLAUDE.md`. Rewrite `README.md`,
`CLAUDE.md`, `docs/ARCHITECTURE.md`; add `docs/MCP.md`.

---

## 9. MCP server

Survives intact — it is the part of this codebase that works. ~120 tools stay
roughly as-is now that their endpoints survive; the trim is limited to tools
whose endpoints actually disappear (recipe authoring, suggestions, habits).

Two additions, both aimed at the spine:

- **`log_day`** — one call taking meals, sets and a weight for a date. The
  natural agent interaction is "here's my whole day," not fourteen round trips.
- **`promote_to_recipe`** — the agent-side of §4's promotion chip, so
  "save what I just logged as 'chicken and rice'" works conversationally.

`docs/MCP.md` becomes the one piece of documentation worth writing well: token
setup, tool catalogue, example sessions.

---

## 10. Execution order

Each phase is one commit and leaves the build green.

| # | Phase | Risk | Notes |
|---|---|---|---|
| 0 | Docs + scratch purge | none | §8, last block |
| 1 | Route audit | none | walk all ~70 routes, record which dead-end. Input to phases 2 and 3 |
| 2 | Nav tiering | low | `AppShell` rebuild: 5-slot spine, `( + )` sheet, `/more` drawer. **Highest value per hour in the whole plan** |
| 3 | Small deletions | low | §8 routes, frontend OTel, admin halving |
| 4 | Recipe inversion | medium | promotion chip, unified picker, sub-table collapse, migration |
| 5 | Contextual surfaces | medium | tier 2: resume banner, trainer strip, household switcher, in-context Pro wall |
| 6 | Auth → Identity | **high** | the only phase that can lock users out; scrypt-compat hasher; rehearse on a prod DB copy |
| 7 | Schema unification | **high** | drop Drizzle, squash migrations, export/import **all** surviving tables, snapshot first |
| 8 | Storage abstraction | low | `IStorageService`, Cloudinary impl, drop `next-cloudinary`. S3 in v2 |
| 9 | UI rebuild on the new tiers | medium | `/today`, `/history`, `/progress`, sheet-based logging |
| 10 | Docs rewrite | none | README, CLAUDE.md, ARCHITECTURE.md, MCP.md |

Phases 0–3 land in a day or two and fix the complaint that started this.
Phases 6–7 are the real engineering. Phases 4–5 and 9 are the product work.

**If only one thing gets done: phase 2.** The nav is the bug.

---

## 11. What this costs

- **Auth downtime risk.** Phase 6 is the one place a mistake locks users out.
  Snapshot, rehearse on a copy, keep a rollback.
- **OAuth and magic-link login go away** until deliberately re-added.
- **Recipe authoring changes shape.** Existing recipes migrate fine
  (instructions collapse to text, nutrition becomes computed), but anyone who
  wrote recipes by hand loses that form. Nested recipes, if any exist in prod,
  must be flattened by the migration — check before running it.
- **Discoverability trade.** Tier 3 features get *less* prominent, and a feature
  two taps away is used less than one in the nav. That is the deliberate price
  of the spine being unmissable. If billing conversion drops, the in-context
  upsell in §5 is the lever, not a nav slot.
- **The name.** With recipes kept, "MacroChef" survives — but the product is now
  a logger that has recipes, not a recipe app that logs. Worth deciding which
  name leads before the UI rebuild in phase 9.
