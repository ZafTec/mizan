# Refocus Plan: Mizan is a Logging App

**Status:** rev 3 — phases 0, 1 and 2 executed
**Branch:** `claude/cleanup-logging-refocus-rzfiv8`
**Rule:** decide what the project is about, build around it. Everything else
gets demoted, not deleted.

> **rev 3 adds the AI platform (§10), pins households as kept (§9), and folds in
> the phase 1 route audit — see `docs/ROUTE-AUDIT.md`. Phases 0, 1 and 2 are done.
>
> **rev 2 changed the thesis.** Rev 1 proposed deleting recipes, billing,
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
center of gravity of this plan moves to two places — **navigation tiering (§3)**
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
| "Save as recipe" chip | a logged meal contains ≥3 foods (§4) |
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
Account   Profile · Billing · Usage · Settings · MCP Tokens · Export
Admin     (role-gated)
```

Rule for this screen: **an entry with no data renders as a one-line "set this up"
row, never as an empty or broken page.** That is the other half of the "features
unavailable to users" complaint — several of these routes exist but dead-end.
Every tier-3 entry must be reachable in ≤2 taps and must work. Auditing all
~70 routes against that bar is a work item, not an assumption.

This is the "accessible" half: everything is two taps away and nothing costs a
pixel until asked for.

### What the phase 1 audit found

`scripts/route-audit.mjs` (full report: `docs/ROUTE-AUDIT.md`) measured it:
**73 routes, 21 reachable from the nav, 9 orphaned.** Six of those orphans are
real bugs — including `/trainers/my-trainer` (245 LOC) and `/trainers/requests`
(173 LOC), which are built, wired to live `Trainers` endpoints, and linked from
nowhere.

That pair settles the rev 1 vs rev 2 argument. The trainer feature is not
unloved because it is bad; it is unloved because nothing links to it. Rev 1
would have deleted working code to fix a missing `<Link>`.

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

## 9. Households stay

Households are not multi-tenancy overhead — they are the sharing boundary that
makes meal prep and shopping lists work. Two people in a household cook from one
plan and shop from one list. That is the feature working as designed.

Kept as built: `Household`, `HouseholdMember`, `HouseholdInvitation`,
`UserHouseholdPreference`, and the invite/switch flows. Meal plans, shopping
lists and recipes stay household-scoped.

What changes is only rank. The household switcher is tier 2 — it appears in the
header only when you belong to more than one. `/profile/household` moves to
tier 3. A solo user never sees the concept; a shared user finds it where they
expect it.

The AI in §10 is household-aware for the same reason: "what should we prep this
week" is a household question, and the plan and list it writes to are shared.

---

## 10. AI platform

An OpenAI-compatible endpoint and key get plugged into config. **The backend
owns every part of this.** The frontend never sees the key, never calls the
provider, and never decides whether a call is allowed.

### Provider

One interface, `IAiProvider`, over an OpenAI-compatible `/chat/completions`
client. Configuration is the entire integration surface:

```
Ai__BaseUrl        provider endpoint
Ai__ApiKey         secret, backend only, never in a frontend env var
Ai__Model          e.g. gpt-5.6-luna
Ai__MaxOutputTokens
Ai__TimeoutSeconds
```

Model choice is config, not code. Swapping providers is an env change, and the
model id never appears in a call site.

### Structured output, not prose parsing

Every non-chat AI feature returns **JSON against a declared schema**, requested
via the provider's structured-output mode. Food analysis returns typed data the
app can act on:

```csharp
record FoodAnalysis(
    IReadOnlyList<AnalyzedItem> Items,   // name, quantity, unit, confidence
    NutritionTotals Totals,              // kcal, protein, carbs, fat, fiber
    string? Note);                       // caveats, never the payload
```

Rules: the schema is versioned and lives next to the DTO; a response failing
schema validation is a failed call, retried once then surfaced as a typed
error — never regex-scraped, never shown raw. Analysis results are **proposals**
that land in the log-entry sheet pre-filled and require the user to confirm.
The AI never writes to the food diary unattended.

### Chat

`AiChatThread` already exists in the domain — this builds on it rather than
adding an entity. Threads are per-user, backend-persisted, with a trimmed
context window and the user's recent log summary injected as system context so
"how did I eat this week" works without the model guessing.

### The onboarding agent does things

Onboarding is where the AI earns its cost: instead of a six-screen form, a
conversation that **performs setup via tool calls**.

The model gets a fixed allowlist of tools, each mapping to an existing MediatR
command with its existing validator:

| Tool | Command |
|---|---|
| `set_targets` | `CreateUserGoalCommand` |
| `log_measurement` | `LogBodyMeasurementCommand` |
| `log_meal` | `LogFoodCommand` |
| `create_household` | `CreateHouseholdCommand` |
| `create_meal_plan` | `CreateMealPlanCommand` |

Non-negotiables: the model never touches the database, only this allowlist.
Every argument goes through the same FluentValidation the HTTP path uses. Every
tool call is authorized as the calling user — the agent cannot act outside their
scope. Mutations are echoed back in the UI as "I set X, undo?" Nothing
destructive is exposed as a tool, ever.

This is the same shape as the MCP server, which is the point — MCP already
proved the pattern. `Mizan.Mcp.Server` and the onboarding agent should share the
tool-to-command mapping rather than each defining their own.

### Limits: per-user and global

Two independent ceilings. Both must pass.

**Per-user**, by entitlement tier — daily request cap plus a monthly token
budget. Free gets enough to see the value; Pro gets a working allowance.

**Global**, across all users — a daily token and estimated-cost ceiling that is
a hard circuit breaker on the whole provider bill. This is the one that stops a
loop or an abusive account from producing a surprise invoice. It is not
optional and it ships in the same commit as the first AI call.

Mechanics:

- `AiUsageLog` (Postgres) — the durable ledger: user, feature, model, prompt and
  completion tokens, estimated cost, latency, outcome, timestamp. This is the
  source of truth for the usage tab and for billing reconciliation.
- Redis counters keyed by user/day and global/day — the hot-path check, so a
  quota test is not a table scan. Postgres is authoritative; Redis is the cache
  and is rebuildable from the ledger.
- Reserve-then-settle: estimate before the call, write actual usage after. A
  crashed call still settles, so tokens cannot leak.
- Exhaustion is a **typed 429 with which limit tripped and when it resets** —
  never a 500, never a silent degradation. Global exhaustion tells the user the
  service is at capacity, not that they are out of quota; those are different
  messages and conflating them is a support ticket.

### Usage tab

`/more → Settings → Usage`. Requests and tokens used against your cap for the
current period, a short history, and what resets when. Admins additionally see
global spend against the ceiling — that view is the difference between noticing
a cost problem and being told about it by the invoice.

### Gating

AI is where Pro gets something worth paying for. Current gating is three
endpoints (§5), which is thin. Proposed split:

| | Free | Pro |
|---|---|---|
| Chat | small daily cap | working allowance |
| Food photo analysis | — | included |
| Onboarding agent | included, once | included |
| Weekly log insights | — | included |

Enforced in one place: an `IAiQuotaService` check ahead of every provider call,
reading `IEntitlementService` for the tier. Not per-controller attributes —
those drift, and a missed attribute is an unmetered call.

Widening Pro beyond AI is still a product decision and still out of scope.

---

## 11. MCP server

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

## 12. Execution order

Each phase is one commit and leaves the build green.

| # | Phase | Risk | Status | Notes |
|---|---|---|---|---|
| 0 | Docs + scratch purge | none | **done** | 47 files → 4. `AGENTS.md` merged into `CLAUDE.md`; dead `.fpf/` pointers removed |
| 1 | Route audit | none | **done** | `scripts/route-audit.mjs` + `docs/ROUTE-AUDIT.md`. 73 routes, 21 in nav, 9 orphaned |
| 2 | Nav tiering | low | **done** | spine + `( + )` sheet + `/more`; nav model extracted to `components/Layout/nav.ts`; 21 flat entries → 4; orphans 9 → 4 |
| 3 | Fix + delete per audit | low | next | link the orphaned trainer and admin screens, delete `/community`, resolve the 5 `TODO` routes, drop frontend OTel, halve the admin panel |
| 4 | Recipe inversion | medium | | promotion chip, unified picker, sub-table collapse, migration |
| 5 | Contextual surfaces | medium | | tier 2: resume banner, trainer strip, household switcher, in-context Pro wall |
| 6 | **Remove BetterAuth** → Identity | **high** | | delete `lib/auth.ts`, `lib/auth-client.ts`, `app/api/auth/[...all]`, the `better-auth` deps; stand up `AddIdentityApiEndpoints`; scrypt-compat hasher; rehearse on a prod DB copy |
| 7 | Schema unification | **high** | | drop Drizzle and `frontend/db/`, squash migrations, export/import **all** surviving tables, snapshot first |
| 8 | Storage abstraction | low | | `IStorageService`, Cloudinary impl, drop `next-cloudinary`. S3 lands in v2 |
| 9 | AI platform | medium | | `IAiProvider`, `AiUsageLog`, `IAiQuotaService`, per-user + global ceilings, usage tab. **Limits ship with the first provider call, not after** |
| 10 | AI surfaces | medium | | structured food analysis, chat on `AiChatThread`, onboarding agent over the allowlisted tool→command map shared with MCP |
| 11 | UI rebuild on the new tiers | medium | | `/today`, `/history`, `/progress`, sheet-based logging |
| 12 | Docs rewrite | none | | README, CLAUDE.md, ARCHITECTURE.md, MCP.md, AI.md |

Ordering constraints that actually bind:

- **9 before 10.** Metering and the global ceiling exist before anything can
  call the provider. Shipping a feature first and limiting it later is how you
  find out about the bill from the invoice.
- **8 before 10.** Food photo analysis needs storage behind an interface, or the
  v2 S3 swap has to touch the AI code too.
- **6 before 7.** Identity must own `users` before Drizzle can be removed.
- **2 and 3 before 11.** Rebuild the screens against the tier structure, not
  before it exists.

Everything else can move. **If only one thing gets done: phase 2.** The audit in
§2 makes the case — 73 routes, 21 reachable.

## 13. What this costs

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
- **A variable provider bill.** AI cost scales with use, and the global ceiling
  in §10 caps the damage but also means the feature can be *off* for everyone
  when it trips. That is the correct failure mode and it will still generate
  support questions. Set the ceiling deliberately, not as a placeholder.
- **AI is wrong sometimes.** Food photo analysis will misjudge portions. That is
  why every result is a pre-filled proposal the user confirms, never a silent
  write to the diary. Do not let a later "just log it automatically" convenience
  request erode that — an unattended wrong entry corrupts the log the whole
  product is built on.
- **The name.** With recipes kept, "MacroChef" survives — but the product is now
  a logger that has recipes, not a recipe app that logs. Worth deciding which
  name leads before the UI rebuild in phase 9.
