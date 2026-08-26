# Refocus Plan: Mizan is a Logging App

**Status:** rev 9 — phases 0-6, 8 and 9 done, 10 next 
**Branch:** `claude/cleanup-logging-refocus-rzfiv8`
**Rule:** decide what the project is about, build around it. Everything else
gets demoted, not deleted.

> **rev 9 records three departures made while building phase 9**, all in §10
> and §11: reserve-then-settle is one ledger row rather than Redis counters,
> the intersection rule landed in phase 9 with the policy rather than waiting
> for 10, and chat is not Pro-gated because the quota tiers already say what
> free gets.
>
> **rev 8 rewrites §6 around a new database.** The owner dropped the
> data-preservation constraint, which deletes the password-compat hasher, the
> export/import scripts and the rehearsal, and merges old phases 6 and 7 into
> one. It also drops browser JWTs for opaque session cookies.
>
> **rev 7 replaces §4's "recipes do not nest" with preparations: marking a
> recipe as a preparation derives a `Food`, so reuse works without a recipe
> graph or a cycle validator.**
>
> **rev 6 added §13, a Telegram bot service, and rewrites §5 after auditing the
> Paddle issue against the code: most of its backend scope is already built.**
>
> **rev 5 added §12: an admin AI console with draft/eval/publish, and the
> hard-vs-soft guardrail split that keeps it from becoming a way around §11.**
>
> **rev 4 added §11: three principal types, per-axis consent, and the
> trainer × AI intersection, and recorded three enforcement defects found while
> checking the plan against the code.
>
> **rev 3 added the AI platform (§10), pinned households as kept (§9), and folded
> in the phase 1 route audit — see `docs/ROUTE-AUDIT.md`. Phases 0, 1 and 2 are done.
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
| "Save as recipe" chip | a logged meal contains ≥2 items (§4) |
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

The floor is **two items** — one food is not a recipe, it is that food, and two
(chicken and rice) is the smallest combination worth naming. `PromoteMealToRecipeCommand`
enforces it server-side so the MCP and Telegram paths cannot bypass the rule.

This is the **only** way a recipe is created. `/recipes/add` and
`/recipes/[recipeId]/edit` as standalone form pages are deleted. Editing a
recipe means logging it again and re-promoting, or adjusting quantities inline.

### Mixed meals: recipes and ingredients together

A logged meal is rarely all one kind of thing. Two eggs, a slice of bread, and
a spoon of your own mayonnaise — two foods and a preparation. Promotion has to
handle that, or the feature only works for the simplest case.

`FoodDiaryEntry` already supports it: an entry carries either a `FoodId` or a
`RecipeId`. Promotion maps each entry by what it is:

| Logged entry | Becomes |
|---|---|
| a food | `RecipeIngredient.FoodId` |
| a recipe already marked as a preparation | `RecipeIngredient.FoodId`, pointing at its derived `Food` |
| a recipe that is **not** a preparation | derive one now, then reference it |

The third row is the interesting one. Promoting a meal that contains a plain
recipe turns that recipe into a preparation as a side effect, because there is
no other way to keep the macros right. That needs `YieldGrams`, so when it is
missing the promotion asks for it rather than guessing — a silent guess here
produces a recipe with wrong calories, which is the failure this whole section
exists to avoid.

Nothing is flattened to text. Every ingredient in a promoted recipe resolves to
something with real macros.

### Direction 2 — Recipe → Log (reuse)

Recipes appear in the **same picker as foods** — one search field, foods and
recipes interleaved, recents and pinned first. Logging a recipe expands it into
its component entries under one collapsible group. Log-from-recipe marks it used,
which floats it up the picker. That is the "logs a meal from a recipe, it is
saved" behavior.

### Preparations: a recipe can become an ingredient

Rev 6 said "recipes do not nest" and proposed dropping `SubRecipeId` outright.
That was wrong, and the counter-example kills it: homemade low-fat mayonnaise.
You make a batch, then use it in other recipes.

Flattening leaves two options, both bad. Re-enter the mayo's ingredients into
every recipe that uses it, or carry "mayonnaise" as free text with no macros.
The second is fatal — a recipe with wrong calories in a calorie-logging app is
a defect, not a simplification. Schema tidiness lost to the product's core job.

**But the fix is not `SubRecipeId` either.** Raw recipe→recipe references are
what forced the circular-dependency validator into existence. Narrow it
instead:

> Marking a recipe as a **preparation** derives a `Food` from it.

Everything follows from that:

- `RecipeIngredient.FoodId` already exists, so the mayo is referenced the same
  way any other ingredient is. No `SubRecipeId`, no recipe graph, **no cycle
  validator** — `RecipeCircularDependencyValidator` still goes.
- Nutrition is correct, because `Food` already carries per-100g macros.
- The mayo becomes loggable **on its own** — "a tablespoon of my mayo" — which
  sub-recipes never gave you.
- One primitive for "thing with macros that I consume an amount of", which is
  the same primitive the picker in §4 already searches.

**Macros are snapshotted at promotion, not computed live.** The derived `Food`
stores fixed per-100g values calculated when you promote. Edit the mayo recipe
and re-promote, and the `Food` updates from then on. No live recursion means
cycles are not merely validated against, they are impossible. This also matches
how `FoodDiaryEntry` already behaves: it copies calories and protein onto the
entry instead of recomputing them later.

#### What it needs

**Foods get an owner.** This is decided, and it is worth doing on its own
merits regardless of preparations: `Food` is currently a single global table
with no owner column, and `SearchFoodsQuery` returns all of it, so every
user-created food today lands in everyone else's search results. `/admin/ingredients`
is titled "Public Ingredients" for a set that has no private counterpart.

| Field | Why |
|---|---|
| `Food.UserId` (nullable) | null = public/global; set = private to that user. Scopes search to `UserId == null \|\| UserId == me` |
| `Food.SourceRecipeId` (nullable) | provenance, and lets re-promotion find the row to update instead of duplicating it |

`IsVerified` keeps its current meaning — admin-curated — and is unrelated to
ownership. A private food can be promoted to public by an admin, which is what
`/admin/ingredients` becomes: a queue of user-created foods worth sharing,
rather than a list of everything anyone ever typed.

**This changes who may create a food.** `POST /api/Foods` was admin-only, and
had to be, because every food went into everyone's search. With ownership it
opens to any signed-in user, whose foods are private to them; admins still
create public ones. `IsVerified` is ignored for non-admins, so nobody can mark
their own entry as verified. Update and delete follow the same rule — you may
maintain your own foods, the shared catalogue stays admin-only.

Migration note: every existing row becomes public (`UserId = null`), because
there is no ownership information to recover and silently privatising foods
that recipes already reference would break them. Ownership starts applying to
new foods.

`PromoteRecipeToIngredientCommand` does the derivation: sum the recipe's
ingredient macros, divide by total yield weight, write per-100g values onto the
`Food`. Recipes gain a `YieldGrams` field, because "serves 4" cannot be
converted into per-100g without it.

#### Still not supported

A preparation whose own recipe uses another preparation is fine — it is just a
`Food` reference like any other. What is not supported is a preparation
referencing itself transitively *and* expecting live recalculation, which the
snapshot rules out by construction.

### Model simplification

| Now | After |
|---|---|
| `RecipeInstruction` table (ordered rows) | one nullable `Instructions` text column |
| `RecipeNutrition` table | computed from ingredients on read, never stored |
| `RecipeTag` | dropped — the picker sorts by recency and pins, not tags |
| `RecipeIngredient.SubRecipeId` + `RecipeCircularDependencyValidator` | dropped — replaced by preparations, which reference a derived `Food` and cannot cycle |
| `FavoriteRecipe` | kept, reframed as a pin that boosts picker rank |

Four tables and a graph-cycle validator collapse into a recipe plus its
ingredients, with reuse handled by preparations rather than a recipe graph. `RecipeCircularDependencyValidatorTests` goes with it.

---

## 5. Billing: mostly built, and the open issue is stale

Paddle, `Subscription`, `PaddleWebhooksController`, `EntitlementService` and the
`RequirePro` policy all stay as built. The integration works; it is revenue.

### The tracking issue asks for work that already exists

Audited against the code, most of the issue's backend scope is done:

| Issue item | Reality |
|---|---|
| `Subscription` entity with Paddle ids, period end, status | **built** — and richer: `Plan`, `IsLifetime`, `PaddlePriceId`, `TrialEndsAt`, `CanceledAt` |
| Webhook verifies `Paddle-Signature`, 401 on invalid | **built** — `PaddleSignatureVerifier`, returns `Unauthorized()` |
| Handle `subscription.{created,updated,canceled}` | **built** — `ProcessPaddleWebhookCommand` handles `subscription.*` |
| `transaction.completed` for Lifetime | **built** — sets `IsLifetime` |
| Tier lookup cached, invalidated on webhook | **built** — `EntitlementService` over `HybridCache`, tag `entitlement:{userId}`, `InvalidateAsync` on receipt |
| Frontend checkout | **built** — `lib/paddle.ts`, billing page |

The issue also specifies webhook idempotency nowhere, and the code already has
it: events are deduped by event id, with a note about Paddle delivering
`subscription.created` and `subscription.trialing` concurrently.

Three specifics in the issue contradict what exists. The code is right in each
case and the issue should be corrected, not the code:

- **Route.** Issue says `/api/Paddle/webhook`; actual is `/api/webhooks/paddle`.
  The endpoint is already registered in Paddle; changing it to match a ticket
  would break live billing for nothing.
- **`Tier` enum (Free/Pro/Lifetime).** Lifetime is not a third entitlement, it
  is Pro that never expires — which is exactly what `IsLifetime` encodes. A
  three-value enum invites `if (tier == Pro)` checks that silently exclude
  Lifetime customers, who paid the most. Keep the binary entitlement.
- **`[RequireTier(Tier.Pro)]` as a MediatR behaviour.** Gating already runs as
  the `RequirePro` authorization policy. Two mechanisms is worse than either;
  and §10 argues gating belongs in one service call, not in attributes that
  drift.

### What is actually left

1. **The feature split.** This is the real gap. Three endpoints are gated today
   (AI chat, AI image analysis, goal progress history) against the issue's much
   wider list.
2. **Customer portal link** from `/more → Billing`.
3. **Upgrade chips** on gated UI — tier 2, in context, per §3.
4. **Sandbox round-trip**, which needs live Paddle credentials.
5. **Legal pages on zaftech.co** naming Paddle as Merchant of Record. Different
   repo; out of scope here but a launch blocker.

### One conflict worth deciding before implementing the split

The issue puts **trainer-client chat and goals** behind Pro. That collides with
§11. If a client's subscription lapses, does their trainer lose access to data
the client explicitly granted? Two bad answers: revoke it, and billing silently
overrides consent; keep it, and the gate does nothing.

Recommendation: **gate the relationship's creation, not its contents.** A Free
user may hold one active trainer relationship; Pro removes the cap. An existing
relationship keeps working when a subscription lapses. Consent and billing stay
independent, which is the only version where §11 still means anything.

Same principle for households: gate invitations beyond two members, never
retroactively eject people or hide data already shared.

### Where billing sits in the UI

Billing moves to tier 3 (`/more → Billing`). The Pro *upsell* is tier 2 — it
appears when a gated action is attempted, naming the specific thing being
unlocked. A standing "Upgrade" nav item converts worse and costs a permanent
slot.

---

## 6. Kill the shared schema — v2 is a fresh start

Today: BetterAuth owns `users`/`accounts`/`sessions`/`jwks`/`verification` via
Drizzle; EF Core owns everything else and treats `User` as read-only with a
comment block begging you not to touch it. Two ORMs, two migration tools, one
database, and a `frontend/db/schema.ts` that documents tables it does not own.

**Target: ASP.NET Core owns the entire schema. Next.js owns zero tables.**

### The constraint that changed

Rev 7 planned this as the high-risk pair: rehearse on a production copy, write a
scrypt-compatible hasher so nobody resets a password, export and re-import every
surviving table. All of that existed to protect data in the live database.

The owner removed the constraint: **v2 gets a new database.** History is not
carried forward here; moving the old rows across is a separate exercise with its
own migration step, outside this scope.

That deletes, in one stroke: the password-compat hasher, the rehash-on-login
path, `scripts/export-data.mjs` / `scripts/import-data.mjs`, the rehearsal, the
snapshot, and the "keep both schemas alive during the cutover" dance. What is
left is a straight rewrite, which is why 6 and 7 are now one phase.

### Migrations: one file, and it stays one file

Delete `Mizan.Infrastructure/Data/Migrations/` — 16 migrations, 38k lines,
every one of them describing a two-ORM world — and generate a single
`InitialCreate` from the unified model. The history has no value once the
database it describes is gone.

**This is a standing rule, not a one-off cleanup.** A schema change deletes
`Migrations/` and regenerates `InitialCreate`; it never stacks a second file on
top. v2 has no deployed database to preserve, so a migration history buys
nothing and costs a growing pile of files nobody reads. The seeds live at the
end of `Up()` and every one of them is `ON CONFLICT DO NOTHING`, so
regenerating is safe to repeat.

When there is a database worth preserving, this rule ends and normal
incremental migrations begin. Until then, one file.

### Auth: opaque session cookies, not browser JWTs

The JWT bearer scheme exists for exactly one client: the browser. The MCP server
authenticates with a service API key plus an `X-Impersonate-User` header, and
never presents a JWT. So the browser's move off JWTs removes the scheme
entirely, and with it `EdDsaJwtSignatureValidator`, `JwksProvider`,
`JwksRefreshService`, `JwtAuthenticationExtensions`, `JwtOptions`, the Redis
JWKS cache, the whole `Jwt__*` config surface, `GET /api/auth/token`, the
client-side token cache in `lib/api.client.ts`, and the 15-minute refresh dance
that would otherwise need Next.js middleware to run on every request.

What replaces it: one httpOnly cookie holding an opaque 256-bit session token.

```
mizan_session   HttpOnly; Secure; SameSite=Lax; Domain=.euaell.me; Path=/
```

`mizan.euaell.me` and `api.mizan.euaell.me` are the same site, so Lax sends the
cookie on both browser→frontend and browser→API requests, including the SignalR
WebSocket upgrade. Server components read the same cookie and forward it. The
API resolves it against `user_sessions`, cached in `HybridCache` — the same
lookup budget JWKS already spent, for a session that is now genuinely
revocable, which a JWT never was.

### Auth: what we build vs. what we take

`AddIdentityApiEndpoints<T>()` was rev 7's answer. Checked against what this app
actually needs, it covers about 60%: it has no external logins, no session list
or revoke, no ban, no sign-in notification, and it forces `IdentityUser<Guid>`
onto the `users` table along with eleven columns nothing reads and three store
tables nothing writes. The other 40% would be ours anyway, on a different
session model from the half Identity owns. Two session models in one app is the
clanky outcome, so:

**Take the primitives, own the endpoints.**

| Concern | What we use |
|---|---|
| Password hashing | `PasswordHasher<User>` from `Microsoft.AspNetCore.Identity`, standalone — no stores, no `AspNet*` tables. PBKDF2-HMAC-SHA512, the exact code Identity itself calls |
| Sessions | 32 random bytes, SHA-256 at rest, 7-day sliding expiry, revoke = delete |
| One-time tokens | 32 random bytes, SHA-256 at rest, single use: 24h to confirm an email, 1h to reset a password |
| Lockout | `access_failed_count` + `lockout_end` on `users`, 5 failures per 15 minutes |
| External login | `AddGoogle` / `AddGitHub` (`AspNet.Security.OAuth.GitHub`), correlated through a short-lived external cookie scheme, exchanged for a session on callback |
| Roles, ban | The `role`, `banned`, `ban_reason`, `ban_expires` columns that already exist and that the admin UI already reads |

The only cryptography we write is "generate random bytes, store their hash".

### Schema

`users` keeps every column it has and gains `password_hash`,
`access_failed_count`, `lockout_end`. Three tables arrive:

| Table | Holds |
|---|---|
| `user_sessions` | one row per signed-in browser: token hash, expiry, IP, user agent, last-seen. Powers `/profile/sessions` and admin revocation |
| `user_tokens` | pending email confirmations and password resets: purpose, token hash, expiry, consumed-at |
| `external_logins` | provider + provider key → user, for Google and GitHub |

Five tables leave: `accounts`, `sessions`, `verification`, `jwks`, `rateLimit`.

### Auth surface, kept and dropped

**Kept:** email + password, mandatory email verification, password reset,
session list and revoke, roles, ban, delete account, theme preferences,
per-endpoint credential rate limits, the sign-in notification email, and both
social providers.

**Dropped:** magic link — password reset is the same flow with an extra name,
and the login page keeps one "forgot password" link instead of two paths to the
same inbox. `haveIBeenPwned` — an outbound call on the signup path, replaced by
a 10-character minimum. The BetterAuth `admin` plugin's impersonation; the MCP
`X-Impersonate-User` path is unaffected and stays.

### Next.js becomes a pure client

`frontend/db/`, `drizzle.config.ts`, `drizzle-orm`, `drizzle-kit`, `postgres`,
`better-auth`, `@better-auth/infra`, `nodemailer`, `lib/auth.ts`,
`lib/auth-client.ts`, `lib/permissions.ts`, `lib/email.ts`, `lib/redis.ts`,
`app/api/auth/[...all]`, `app/api/admin/revoke-sessions`,
`app/api/test/verify-user` all go. `DATABASE_URL`, `BETTER_AUTH_*` and every
SMTP variable leave the frontend environment. Email moves to the backend behind
one `IEmailSender`.

**Redis stays.** SignalR needs the backplane, and `HybridCache`'s L2 now backs
session lookups as well as `EntitlementService` and `UserStatusService`.

---

## 7. Storage: S3 now, not v2

Do not delete image handling - rev 1 had this wrong; it was only ever there for
recipe images and avatars, and both stay.

Rev 8 planned `IStorageService` over Cloudinary now and S3 later. The owner
brought S3 forward: the deployment will be either a self-hosted MinIO or
Cloudflare R2, undecided. That makes flexibility the requirement rather than a
future nicety - and it is free, because both speak S3.

**One implementation, two backends, nothing but configuration between them.**

| | MinIO | Cloudflare R2 |
|---|---|---|
| `Storage__ServiceUrl` | `https://minio.example` | `https://<account>.r2.cloudflarestorage.com` |
| `Storage__Region` | `us-east-1` (ignored, but the SDK insists) | `auto` |
| `Storage__PublicBaseUrl` | whatever fronts the public bucket | an R2 custom domain, or the r2.dev host |

Two things make one client work against both, and neither is a workaround:

- **Path-style addressing.** Both address buckets by path, not by subdomain.
- **Checksums off unless required.** The v4 AWS SDK adds flexible-checksum
  headers to every request; R2 rejects them and older MinIO builds do too.
  `RequestChecksumCalculation = WHEN_REQUIRED` sends them only where the
  protocol demands one.

### The shape

`IStorageService` is `UploadAsync` / `DeleteAsync` / `GetUrlAsync` /
`TryGetKey`. The last one earns its place: it answers "did we store this URL?",
which is what lets a replaced avatar be deleted without ever deleting a Google
or GitHub avatar URL that happens to be sitting in the same column.

Keys are built in one pure function - `{folder}/{yyyy}/{MM}/{uuidv7}{ext}` -
where the folder comes from a closed enum and the extension from a fixed list.
Nothing a caller sends can steer a write outside its folder, and
`StorageKeyTests` holds that line with `../../etc/passwd` and friends.

**Uploads go through the API, never the browser to the store.** That deletes
`app/api/sign-cloudinary-params`, `next-cloudinary` and `cloudinary`, and means
the browser never holds a storage credential. `POST /api/Uploads/image`
authenticates, sniffs the first twelve bytes to decide what the file actually
is - a `Content-Type` is the caller's claim, not evidence - and refuses
anything that is not a JPEG, PNG, WebP or GIF.

**Unconfigured is a supported state.** With no `Storage__ServiceUrl` the API
starts and only uploads refuse, with a message that says storage is not
configured rather than a connection error. Local development and the test suite
both run in it.

**AI food-photo analysis does not go through storage.**
`POST /api/Nutrition/ai/analyze-image` reads the bytes, sends them to the model
and forgets them. Keeping a user's food photos is a decision for §11 to make
deliberately, not something storage should start doing by accident.

**`next/image` needs the media host allowlisted**, and that host is
deployment-specific, so `next.config.ts` reads it from `NEXT_PUBLIC_MEDIA_URL`
rather than hardcoding one.

**The signup avatar field is gone.** Uploads require a session and a signup has
none; the field had already stopped being read when registration moved to the
backend in §6. Avatars are set from Settings, after signing in.

### `Mizan.Contracts`

The MCP server built every request body as an anonymous object. That is exactly
how the phase 4 recipe bug happened: `instructions` changed from an array to a
string, the tool kept sending an array, the API answered 400, and the tool
reported success while creating nothing. The compiler could not have known.

`Mizan.Contracts` is a dependency-free library holding the wire shapes. The
Application's commands inherit them:

```csharp
// Mizan.Contracts
public record CreateFoodRequest { /* the wire shape */ }

// Mizan.Application
public record CreateFoodCommand : CreateFoodRequest, IRequest<CreateFoodResult>;
```

No mapping layer, no controller churn, and the MCP server constructs the same
type the API binds. A renamed field, a removed field or a changed type now
fails the build. An *added* optional field still defaults silently, which is
the correct behaviour anyway.

**Scope: the spine's writes** - foods, recipes, meals, workouts, body
measurements. That is §1's three things plus what they are made of, and it is
where the one real drift bug landed. The remaining tools convert as they are
touched; reads pass JSON straight through and gain little from typing.

**Why not generate a client from the OpenAPI document**, the way the frontend
does? Because it needs a running API at build time. The frontend already lives
with that (`bun run codegen`), and a committed generated client that nobody
re-runs is not much better than an anonymous object.

One thing had to move with the types: converting a string argument to a `Guid`
or a `DateOnly` now happens in the MCP server rather than at the API boundary,
so `ToolArguments` does it with messages that name the argument. "Invalid
foodId 'abc'. Expected a Guid" is what an agent can act on;
"Unrecognized Guid format." is not.

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

**Admin panel — reduced by evidence.** Phase 3 checked each screen against the
MCP tool list rather than trusting this section's original list. Achievements,
audit logs and relationships went; households and sessions stayed, because no
MCP tool covers them and session revocation is a real security operation.
`/admin/ai` (§12) later makes the strongest case of all for keeping a panel.
See `docs/ROUTE-AUDIT.md` for the table. Drop the ones the MCP admin tools already do better:
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
context window.

The user's log summary is injected as system context **only for the axes they
have consented to**, scoped to their active household. Consent defaults to off
on every axis. See §11 — the context builder asks `IDataAccessPolicy` what it
may include and receives nothing more; it does not receive the full log and
filter it afterwards.

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
tool call is authorized as the calling principal **against the grant, not just
the identity** — a trainer is a legitimate user with partial access to a client,
so identity alone is the wrong check (§11). Mutating tools act only on the
caller's own records. Mutations are echoed back in the UI as "I set X, undo?" Nothing
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
- **Reserve-then-settle is one ledger row, not a Redis counter beside one.**
  *(Departure, taken while building phase 9.)* This section originally called
  for Redis counters keyed by user/day and global/day, on the grounds that a
  quota test should not be a table scan. It is not a table scan — it is one
  indexed aggregate over a day, in front of a call that takes seconds. Against
  that, two stores means skew, a rebuild path, and two places to be wrong. So
  the reservation *is* a row: written with the estimate and `Pending`, updated
  with the truth. A process that dies mid-call leaves the estimate standing,
  which over-counts, and over-counting is the safe direction for a spend
  ceiling. Revisit if the aggregate ever shows up in a slow query log.
- Settling runs in a `finally`, so a failed, timed-out or cancelled call still
  costs what it cost. A failing loop is not free.
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

**Chat is not Pro-gated at the endpoint.** *(Departure.)* The table below gives
free users a small daily chat allowance, and `IAiQuotaService` already enforces
it by tier. A `RequirePro` attribute on the chat endpoint would be a second
copy of this table, in a different place, drifting. Food photo analysis stays
Pro-gated, because the table gives free users none of it.

AI is where Pro gets something worth paying for. Current gating is three
endpoints (§5), which is thin. Proposed split:

| | Free | Pro | Trainer |
|---|---|---|---|
| Chat | small daily cap | working allowance | own ceiling, scaled to client count |
| Food photo analysis | — | included | included |
| Onboarding agent | included, once | included | included |
| Weekly log insights | — | included | included, per consenting client |
| Client-context queries | — | — | read-only, bills the trainer (§11) |

Enforced in one place: an `IAiQuotaService` check ahead of every provider call,
reading `IEntitlementService` for the tier. Not per-controller attributes —
those drift, and a missed attribute is an unmetered call.

Widening Pro beyond AI is still a product decision and still out of scope.

---

## 11. Principals, consent and what the AI is allowed to see

Three principal types, and they are not variations of one another. Getting this
wrong is the failure mode that matters most, because it leaks somebody's body
weight to somebody else.

| Principal | Scope |
|---|---|
| **Admin** | developers and product owners. Operational access, not a super-user over personal data |
| **User** | belongs to *many* households, switches active one. Chooses, per axis, what the AI may see |
| **Trainer** | linked to *many* users. Gets what each client grants, per axis. Chat. The AI assists them but is never authoritative |

Three data axes, and they are already the right three in the schema:
**nutrition** (meals), **training** (workouts), **body** (measurements).

### What already exists and works

`TrainerClientRelationship` carries `CanViewNutrition`, `CanViewWorkouts`,
`CanViewMeasurements`, `CanMessage`, `CanAwardAchievements`. The client sets
them when accepting the request. Three are enforced:

- `GetClientNutritionQuery` checks `CanViewNutrition`
- `WorkoutFeatureQueries` checks `CanViewWorkouts`
- `SendChatMessageCommand` checks `CanMessage`

That is a real consent model, correctly shaped, and it is a reason to keep
trainers rather than cut them.

### Three defects found while checking this plan against the code

**1. `CanViewMeasurements` is never enforced.** It is declared, defaults to
false, and is settable by the client — and no code path reads it. It is not
currently exploitable, because no endpoint serves client measurements to a
trainer at all. It is a trap: the first person to add `GetClientMeasurements`
has no precedent to copy and will ship it ungated. Body data is the most
sensitive of the three axes and it is the one axis with no check.

**2. `HouseholdMember.CanViewNutrition` is never enforced either.** Same shape
— written by `CreateHouseholdCommand` and `RespondToHouseholdInvitationCommand`,
read by nothing. Household nutrition visibility is currently ungated.

**3. Grants cannot be revoked.** They are set once, at
`RespondToTrainerRequestCommand`. `TrainersController` exposes request, respond,
and the read paths — there is no update. A user can decide once and never change
their mind short of ending the relationship. "The user chooses to give their
metrics data" has to mean they can also take it back.

### AI consent does not exist yet, and §10 assumed it away

There is no `AiConsent` anywhere in the codebase, and §10 as first written said
to inject "the user's recent log summary as system context." Against this model
that is wrong: it exposes all three axes to the provider with no opt-in. That
was my error in the plan, not a defect in the code.

Correcting it:

- **`UserAiConsent`** — per user, one flag per axis plus a master off switch.
  **Default off, all axes.** The AI sees nothing until the user says so.
- Consent is **withholding, not instructing**. If `body` is off, the context
  builder never receives weight data. Never "the model has it but was told not
  to mention it" — that is not a control, it is a request.
- Revocable at any time, from the same place it is granted.

### One policy object, not scattered `if` statements

`CanViewMeasurements` was missed because enforcement lives in three ad-hoc
checks in three query handlers. Adding a fourth consumer — the AI — to that
pattern guarantees a fourth miss.

Introduce `IDataAccessPolicy` answering one question:

```csharp
// may `principal` read `axis` of `subject`, for this purpose?
Task<bool> CanRead(Guid principal, Guid subject, DataAxis axis, AccessPurpose purpose);
```

Every reader goes through it: the trainer HTTP paths, the household views, the
AI context builder, and the MCP tools. One place to audit, one place to fix.

It is **deliberately uncached**. Caching a permission means a revoked grant
keeps working for the length of the TTL, and the thing being saved is a single
indexed point lookup.

### The trainer × AI intersection

A trainer asking the AI about a client is governed by the **intersection** of
two independent grants:

```
trainer sees axis A of client C  ⟺  C granted A to that trainer
                                AND C consented to A for AI
```

Neither alone is sufficient. A client who shares workouts with their coach but
wants no AI involvement gets exactly that. The trainer's own AI consent governs
the trainer's own data and nothing else.

**"Not authoritative" becomes a technical constraint, not a disclaimer:**

- The tool allowlist for a trainer principal is **read-only over client data**.
  Mutating tools act on the caller's own records, never a client's.
- AI output in trainer surfaces is labelled advisory and is never written into
  a client's record as fact.
- The AI never adjusts a client's targets, plan or log. A trainer acts; the AI
  drafts.

### Multi-household and the AI

A user in several households has one active household
(`UserHouseholdPreference`). The AI's context is scoped to the **active
household only** — meal plans and shopping lists are household-scoped, so
without this the model would blend two households' plans, and worse, could
surface one household's data inside another. The active household is part of
the context key, and the quota ledger records it.

### Quota, when a trainer is involved

A trainer's AI call bills the **trainer's** quota, never the client's.
Otherwise one trainer with twenty clients drains twenty people's allowances and
those clients get rate-limited by activity that is not theirs. Trainer tiers
need their own ceiling in §10's gating table.

### Work this adds

| Fix | Phase |
|---|---|
| Grant-update endpoint so trainer grants are revocable | 3 |
| Enforce or delete `HouseholdMember.CanViewNutrition` | 3 |
| `IDataAccessPolicy`; move the two axis checks behind it (`CanMessage` is a capability, not an axis, and stays in `ITrainerAuthorizationService`) | 9 |
| Enforce `CanViewMeasurements` through the policy, before any client-measurement endpoint exists | 9 |
| `UserAiConsent` entity, settings UI, default-off | 9 |
| Intersection rule in the AI context builder | ~~10~~ 9 — it lives in `IDataAccessPolicy`, and building the policy without it would have meant writing the wrong rule first |
| Read-only trainer tool allowlist | 10 - shipped as *no* tools on that surface |
| Trainer quota tier | 10 |

---

## 12. Admin AI console

Prompts are configuration that changes product behaviour. Hardcoding them in C#
means every wording change is a deploy, and nobody can tell you which version
produced last Tuesday's bad answers. So: yes to an admin console with drafts,
evals, publishing and rollback.

One part of the usual design is wrong, though, and it is the part that would
undo §11.

### Guardrails split in two, and only one half is editable

**If the consent rules live in the system prompt, and an admin can edit the
system prompt, then the console is a supported path around the consent model.**
An admin editing copy would be one careless paste away from "you may discuss any
user's measurements." Prompts are not an enforcement mechanism — they are a
request, and a request can be argued with.

| Hard constraints — code, not editable | Soft policy — prompt, editable |
|---|---|
| axis filtering from `UserAiConsent` | tone and persona |
| the trainer × client intersection | refusal topics (medical, diagnostic) |
| the tool allowlist and its read-only rule for client data | how advice is framed and hedged |
| per-user, trainer and global quota | output verbosity, formatting |
| structured-output schema validation | how the onboarding agent introduces itself |

Hard constraints are enforced **before** the provider call, in
`IDataAccessPolicy` and the quota service. The context the model receives is
already filtered; there is nothing for a prompt to leak. Changing a hard
constraint is a code change and a deploy, deliberately.

The console shows the hard constraints **read-only, alongside the editable
prompt**, so an admin can see what is enforced without being able to edit it.
Invisible constraints get worked around by people who do not know they exist.

### Entities

```
AiPrompt          key ("chat.system", "food.analysis", "onboarding.agent"), description
AiPromptVersion   promptId, version, body, softPolicy(json), status(draft|published|archived),
                  authorId, notes, publishedAt
AiEvalCase        promptKey, name, synthetic input fixture, assertions, isAdversarial
AiEvalRun         versionId, caseId, outcome, schemaValid, tokens, cost, latencyMs
```

`AiUsageLog` (§10) gains `PromptVersionId`. Every answer traces to the exact
version that produced it, so a quality regression is bisectable instead of
mysterious.

Publishing goes through MediatR, so the existing `AuditBehavior` records who
published what, when, without new plumbing. One published version per key at a
time; previous versions stay immutable and revertible.

### Evals, or the draft flow is theatre

A draft an admin eyeballed once is not tested. The console runs a draft against
a fixed eval set and reports:

- **Schema conformance rate** for structured outputs. Objective, and the number
  that actually matters for food analysis.
- **Draft vs. live, side by side** on identical inputs. Nobody can judge a
  prompt change without seeing what it changed.
- **Token and cost delta per case.** A rewrite that doubles token count is a
  bill change, and it should be visible before publish rather than at
  month-end.
- **Adversarial cases, and they gate publish.** Prompt injection in a food
  description, requests for another user's data, medical-advice bait, attempts
  to talk the model past its framing. These are regression tests. A draft that
  fails one does not get a publish button.

### Two rules about test runs

**Synthetic fixtures only — never real user data.** §11 defines admin as
operational access, not super-user access over personal data. An admin reading
real logs to tune a prompt violates that, and it is the kind of thing that
happens by default unless the eval store is built to make it impossible.

**Eval spend bills a separate admin budget inside the global ceiling.** Prompt
iteration burns real tokens. If it draws from the shared pool, an afternoon of
tuning can trip the global circuit breaker and take AI down for every user —
the feature's own maintenance killing the feature. Give it its own line, and
show it in the usage tab next to user spend.

### What I would not build

**Percentage rollouts and canaries.** Wrong tool at this scale. The value of a
canary is catching a regression across a large population before it spreads;
here, an eval set plus one-click rollback covers the same risk with a fraction
of the machinery. Straight publish, instant revert. Revisit if the user base
ever makes it worth it.

### Where it lives

`/admin/ai` — prompts, versions, evals, and the AI slice of usage. §8 argued for
halving the admin panel by moving work to MCP tools; this is the counter-case
and the reason the panel survives at all. A prompt editor with a version diff,
an eval matrix and a cost delta is a genuine UI job — that is not a tool call.

### Work this adds

| Item | Phase |
|---|---|
| `AiPrompt` / `AiPromptVersion`, publish command, audit via `AuditBehavior` | 9 |
| Hard/soft split enforced in the provider call path | 9 |
| `AiEvalCase` / `AiEvalRun`, synthetic fixture store, adversarial set | 10 |
| `/admin/ai` console: editor, diff, eval matrix, rollback | 10 |
| `PromptVersionId` on `AiUsageLog`; admin eval budget line in the usage tab | 10 |

### What shipped, and where it differs from the sketch above

Built as described: the four entities, `PromptVersionId` on `AiUsageLog`,
`AiHardConstraints` rendered read-only next to the editor, one published
version per key (a filtered unique index, so the database enforces it rather
than the command hoping to be the only writer), and twelve synthetic cases
seeded by the migration, six of them adversarial.

Four decisions the sketch left open:

**Rollback is not its own command.** It is a publish of an archived version.
An archived version was published once, so it already cleared the gate;
re-running the suite to move a pointer backwards is exactly the friction that
stops people rolling back when production is misbehaving. A draft still has to
earn it.

**The gate lives in the handler, not only in the console.** A console is a
suggestion; a handler is a rule. `AiPublishGate` is pure and shared, so the
publish button and the refusal agree on why.

**An errored case is not a passed case.** A provider outage must not publish an
unproven prompt, so an unreachable provider blocks exactly like a failure does.
Editing a draft deletes its runs for the same reason: what the old text proved
says nothing about the new text.

**Assertions are three primitives** - `mustContain`, `mustNotContain`,
`requireSchema` - and nothing more. A richer assertion language is a second
query syntax nobody remembers.

Two things above are narrower than written. "Draft vs. live side by side on
identical inputs" shipped as a **body diff plus per-case outcomes**, not a
paired output comparison; running live again on every case to render the pair
doubles the eval bill for a view a diff mostly covers. And the cost delta is
the **whole-suite** figure against the published version's, not per case.
Both are worth revisiting once anyone has tuned a prompt in anger.

The eval budget line is `AiFeatures.Eval` with its own daily allowance, split
from the caller's personal allowance in `AiQuotaService` and still inside the
global ceiling - so tuning cannot spend an admin's chat quota, and a runaway
suite stops where a runaway user does.

Phase 10 finished with four more pieces, and each one moved a line that was
written here as prose into something a test can fail on.

**Chat is rows, not a blob.** `AiChatThread` held one jsonb column called
`ThreadData` that nothing wrote and nothing could query. Messages are rows now,
each answer carrying the version that produced it.

**Suggestions stopped scraping prose.** `data/suggestion.ts` asked the chat
endpoint for "a JSON array" and pulled it back out with a regex - the exact
failure §10 rules out. It is `nutrition.suggestions` with a declared schema, so
a malformed response is a failed call.

**Food analysis is a proposal.** Every recognised food is an editable row the
user drops or confirms; the endpoint had no caller at all before.

**The onboarding agent, and its allowlist.** `AiToolCatalogue` is five tools
onto commands that already exist, so a tool call runs the same validation and
lands in the same audit log as the HTTP path. The security property is
structural: a tool's factory takes arguments and identity as separate
parameters and no schema exposes a user id, so a model writing someone else's
id into its arguments changes nothing. The loop stops after three rounds and
the last round is offered no tools, so a runaway model runs out of the ability
to ask rather than being trusted to stop.

**Trainer surfaces are read-only by construction.** A coach asking about a
client gets the intersection §11 describes, and no tools - not a tool set
filtered to reads, but none at all, which is the version that cannot drift.

One thing this section did not anticipate. §10's gating table has a Trainer
column and §11 says a trainer's call bills the trainer; what neither says is
that *onboarding* needs the same treatment for a different reason. One setup
turn is up to four provider calls, so on the free tier a new user's first act
would empty their whole daily allowance - the surface the assistant justifies
itself on, priced so that using it costs you the rest of the day. Quota lines
are now `AiQuotaLine` (personal, eval, onboarding, trainer) rather than an eval
boolean. Every line lands in the same ledger under the same global ceiling;
only the per-user cap differs.

---

## 13. Telegram bot

A fourth service, `Mizan.Telegram`, alongside API, MCP and frontend. It puts
the AI and the three logs in Telegram.

This is a better fit for the spine than it first looks. §1 says logging should
take under ten seconds. Telegram is already on the home screen, already
authenticated, already the place people send themselves photos of dinner. For
a lot of entries it beats opening the app.

### Shape

Follow the precedent that already works: `Mizan.Mcp.Server` is a separate
ASP.NET service that talks to the API over the Docker network with a service
API key against `ApiKeyAuthenticationHandler`. The bot is the same pattern, so
none of it is new ground.

```
Telegram  ──webhook──▶  Mizan.Telegram  ──http──▶  mizan-backend:8080
                                          service API key, internal only
```

- Never exposed publicly except the webhook endpoint, behind nginx with
  Telegram's secret token header checked on every request.
- Long polling in development, webhook in production. One switch.
- No database of its own. It holds no logs, no nutrition data, no AI
  configuration — it is a client, and the API stays the single source of truth.

### Shared types

The ask is a shared data type, and it exposes a gap: the frontend gets DTOs via
OpenAPI codegen, but a second .NET consumer copying DTOs by hand is how the MCP
server and the API drift apart.

**`Mizan.Contracts` landed in phase 8** — see §7 for what it holds and why the
Application's commands inherit from it rather than mapping to it. The bot
references it and nothing else, so a wire shape it sends is the one the API
binds. Whatever the bot needs beyond the spine's writes gets typed when the bot
is built.

### Account linking, which is the part to get right

A Telegram chat id is not an identity. The bot must never be able to act as an
arbitrary user.

- In the app: `/more → Settings → Telegram` issues a short-lived, single-use
  code and a `t.me/<bot>?start=<code>` deep link.
- `/start <code>` binds that Telegram id to that user. The code is consumed
  immediately and expires in minutes.
- Binding is stored server-side as `TelegramLink` (userId, telegramUserId,
  linkedAt), unlinkable from either side.
- Every API call the bot makes carries the resolved user id. An unlinked chat
  can do exactly one thing: link.
- Group chats are refused outright in v1. Personal nutrition data in a group is
  a leak with extra steps.

### What it does

**Logging, in one message:**

- Photo of a meal → `IStorageService` → food analysis (§10) → inline card with
  the parsed items and a Confirm / Edit / Discard keyboard. Same rule as
  everywhere: **the result is a proposal, never a silent write.**
- `/weight 82.4` → logged, replies with the trend delta.
- Free text — "chicken and rice, about 200g" → the same confirm card.
- `/today` → the day's totals against targets.

**Interactivity is inline keyboards, not command syntax.** Nobody remembers
`/log_meal --protein=40`. Confirm/edit buttons, portion steppers, a "same as
yesterday" shortcut, quick-pick from recent foods and saved recipes (§4).

**Chat** — the same `AiChatThread` as the web app, so a conversation started in
Telegram continues in the browser. One thread per user, not per surface.

### Everything in §10 and §11 still applies, unchanged

This is the load-bearing constraint. The bot is another client of the AI
platform, not a second AI path:

- Consent is read from `UserAiConsent`. A user who has not consented for `body`
  gets no weight context in Telegram either.
- Quota is the same per-user and global ceiling, and `AiUsageLog` records the
  surface so per-channel cost is visible in the usage tab.
- Prompts come from the published `AiPromptVersion` (§12). No bot-specific
  prompt file.
- Tool calls go through the same allowlist, authorized as the linked user.

If the bot ends up with its own consent check, its own quota, or its own
prompt, that is the bug. There is one AI service and several front doors.

### Deliberately not in v1

Group chats. Voice notes — transcription is a second provider, a second cost
line, and a second consent question. Trainer-client chat over Telegram, which
would fork the SignalR conversation into a channel the web app cannot see.

### Work

| Item | Phase |
|---|---|
| `Mizan.Contracts`, referenced by Api, Mcp.Server, Telegram | 8 |
| `Mizan.Telegram` service, Dockerfile, compose entry, webhook + secret token | 13 |
| `TelegramLink` entity, deep-link issue/consume, settings UI, unlink | 13 |
| Logging flows: photo, `/weight`, free text, `/today`, inline keyboards | 13 |
| Chat over the shared `AiChatThread`, consent and quota via §10 and §11 | 13 |

---

## 13a. Streaks, achievements, and the cost of logging

*Added after the phase 10 review, because three of the six "must never break"
properties turned out to be broken or unwatched.*

### One decay rule, not three

`Streak.CurrentCount` is a record of the last write, not of what is true now.
Three readers read it, and **only one applied the decay**:

| Reader | Applied "has it lapsed?" |
|---|---|
| `GetStreakQuery` | yes |
| `GetUserQuery` | no — the header showed a dead streak at its old length |
| `GetAchievementsQuery` | no — **and it awards badges**, so a streak that died in March could still unlock a 30-day badge |

`StreakClock` is now the only thing that answers it, and it answers for the
writer too. Pure, in Domain, twenty tests. `UserActivityStats` replaces the two
separate stats builders — one in the evaluator, one in the query — that were
the reason the two could disagree in the first place.

### Days are the user's, not the server's

`DateOnly.FromDateTime(DateTime.UtcNow)`, and `User` had no timezone at all. On
UTC+3 a 01:00 snack is 22:00 UTC the previous day: it lands on yesterday, the
row already says yesterday, so nothing increments. Someone logging faithfully
every night watches their streak sit at one forever.

`User.TimeZoneId` is an IANA id, taken from the browser at signup and editable
in settings. An unknown zone falls back to UTC rather than throwing — a
restored database on a host with different tzdata must not take the logging
path down. The reset instant is computed from the zone's offset *at the
boundary*, so a day that crosses a DST change still ends at midnight.

The UI shows the deadline. A flame with no "resets in 6h 12m" beside it is a
number the user cannot act on.

### The cost of logging stopped growing

Achievement thresholds were checked with `COUNT(*)` over the user's entire
diary, on every single log. That is a cost that rises forever, on the one path
that must stay fast.

`user_activity_counters` is one row per user, adjusted by an upsert that does
the arithmetic in Postgres — so no read, and no lost update when two logs land
together. It is maintained by a `SaveChanges` interceptor reading the change
tracker rather than a line in each of the ten write handlers, because the
per-handler version fails silently the first time someone adds an eleventh.
Deletes decrement, because the `COUNT(*)` they replace did.

*Departure, taken while building it.* The interceptor was first written to
inject `IActivityCounters`. That is a DI cycle — the service needs
`MizanDbContext`, whose options need the interceptor — and EF resolves
interceptors while building options, so it deadlocks rather than throwing. It
is a stateless singleton now, keyed by context through a
`ConditionalWeakTable`, executing on `eventData.Context` directly.

### Something is watching it now

`LoggingBudgetTests` counts **database round trips**, not milliseconds. A
wall-clock assertion in CI is a flake generator that tells you nothing about
why it got slower; a round-trip count is deterministic and fails on the exact
commit that adds a query.

Logging a meal costs **8** round trips. The budget is exactly 8, with no slack,
because slack absorbs the first regression silently. A second test logs forty
more meals and asserts the cost is *unchanged* — that is the property the
counters bought, and the one worth defending.

---

## 14. MCP server

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

## 15. Execution order

Each phase is one commit and leaves the build green.

| # | Phase | Risk | Status | Notes |
|---|---|---|---|---|
| 0 | Docs + scratch purge | none | **done** | 47 files → 4. `AGENTS.md` merged into `CLAUDE.md`; dead `.fpf/` pointers removed |
| 1 | Route audit | none | **done** | `scripts/route-audit.mjs` + `docs/ROUTE-AUDIT.md`. 73 routes, 21 in nav, 9 orphaned |
| 2 | Nav tiering | low | **done** | spine + `( + )` sheet + `/more`; nav model extracted to `components/Layout/nav.ts`; 21 flat entries → 4; orphans 9 → 4 |
| 3 | Fix + delete per audit | low | **done** | delete `/community`, resolve the `TODO` routes, drop frontend OTel, halve the admin panel, **add the trainer grant-update endpoint and settle `HouseholdMember.CanViewNutrition`** (§11) |
| 4 | Recipe inversion + preparations | medium | **done** — promotion, preparations, food ownership, sub-table collapse. Promotion chip lands with the UI rebuild in 12 | promotion chip, unified picker, sub-table collapse, migration |
| 5 | Contextual surfaces | medium | **done** | tier 2: resume banner, trainer strip, household switcher, in-context Pro wall; standing upsell banners deleted |
| 6 | **v2 identity + schema** (absorbs 7) | **high** | **done** | backend owns `users`; opaque session cookies replace the JWT scheme; Drizzle, BetterAuth and `frontend/db/` deleted; migration history collapsed to one `InitialCreate` against a new database |
| 7 | *(folded into 6)* | | | rev 8 merged schema unification into the identity phase; later numbers are left alone so earlier commits still resolve |
| 8 | Storage + `Mizan.Contracts` | low | **done** | `IStorageService` over S3 - MinIO or R2, configuration only; `next-cloudinary` and the signing route deleted; `Mizan.Contracts` types the spine's writes so Api, Mcp.Server and Telegram cannot drift (§13) |
| 9 | AI platform + consent | medium | **done** | `IAiProvider`, `AiUsageLog`, `IAiQuotaService` with per-user and global ceilings, usage tab, `UserAiConsent` default-off, `IDataAccessPolicy` including the intersection rule. The existing unmetered call was brought under all of it; Semantic Kernel and its auto-invoking write tool are gone |
| 10 | AI surfaces + admin console | medium | **done** | `AiPromptVersion` + the hard/soft guardrail split, chat persisted on `AiChatThread`, onboarding agent over the allowlisted tool→command map shared with MCP; read-only client tools for trainers (§11); `/admin/ai` with evals, diff and rollback (§12) |
| 11 | **Streak + achievement correctness** | medium | **done** | `StreakClock` as the one decay rule, `User.TimeZoneId`, `user_activity_counters` replacing the `COUNT(*)`s, catalogue cached, a round-trip budget test on the logging path (§13a) |
| 11b | Billing feature split | low | | widen gating past the three endpoints, customer portal link, in-context upgrade chips; gate relationship *creation*, never existing consent (§5) |
| 12 | UI rebuild on the new tiers | medium | | `/today`, `/history`, `/progress`, sheet-based logging |
| 13 | Telegram bot | medium | | `Mizan.Telegram`, account linking, logging flows, chat on the shared thread. **Consumes §10's AI service; never its own** |
| 14 | Docs rewrite | none | | README, CLAUDE.md, ARCHITECTURE.md, MCP.md, AI.md, TELEGRAM.md |

Ordering constraints that actually bind:

- **9 before 10.** Metering, the global ceiling and consent all exist before
  anything can call the provider. Shipping the feature first and limiting it
  later is how you learn about the bill from the invoice; shipping it before
  consent is how you learn about the leak from a user.
- **8 before 10.** Food photo analysis needs storage behind an interface, or the
  v2 S3 swap has to touch the AI code too.
- **6 is atomic.** Rev 7 split identity from schema so the two high-risk halves
  could land separately against a live database. With a new database there is
  nothing to protect between them, and splitting would mean running two auth
  systems against one `users` table for the length of a phase.
- **2 and 3 before 12.** Rebuild the screens against the tier structure, not
  before it exists.
- **8 before 13.** `Mizan.Contracts` exists before a third .NET service starts
  copying DTOs by hand.
- **9 and 10 before 13.** The bot consumes the AI service. Building it first
  means building a second AI path, with its own consent and quota, which is
  precisely the thing §13 forbids.

Everything else can move. **If only one thing gets done: phase 2.** The audit in
§2 makes the case — 73 routes, 21 reachable.

## 16. What this costs

- **Auth downtime risk.** Phase 6 is the one place a mistake locks users out.
  Snapshot, rehearse on a copy, keep a rollback.
- **OAuth and magic-link login go away** until deliberately re-added.
- **Recipe authoring changes shape.** Existing recipes migrate fine
  (instructions collapse to text, nutrition becomes computed), but anyone who
  wrote recipes by hand loses that form.
- **Existing nested recipes need converting, not flattening.** Any recipe
  currently used via `SubRecipeId` becomes a preparation with a derived `Food`,
  and the referencing rows repoint to that `Food`. Count them before the
  migration runs; a recipe with no `YieldGrams` cannot be converted
  automatically and needs the user to supply one.
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
- **A prompt console is an ongoing responsibility, not a one-off build.** The
  eval set only protects you if it grows every time something goes wrong in
  production. An adversarial suite that is never added to decays into a
  formality that passes everything.
- **Consent defaults to off means the AI looks broken on day one.** A new user
  opens chat and it knows nothing about them until they opt in. That is the
  correct default and it will read as a bug. Onboarding has to ask for consent
  in context, per axis, explaining what each unlocks — not a single "enable AI"
  toggle buried in settings.
- **The name.** With recipes kept, "MacroChef" survives — but the product is now
  a logger that has recipes, not a recipe app that logs. Worth deciding which
  name leads before the UI rebuild in phase 9.
