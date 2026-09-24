# Mizan architecture

Mizan records meals, training, and body measurements. Web, MCP, and Telegram are interfaces to the same ASP.NET Core API. Identity and business data share one EF Core model in PostgreSQL; the frontend has no ORM or database credentials.

## Request boundaries

```text
Browser ── pages ──> Next.js
   │                   │ forwards session cookie for server renders
   └── /api, /hubs ─────┴──> ASP.NET Core API ──> PostgreSQL
                                  │                └── durable outbox
MCP client ──> MCP service ────────┤
Telegram ────> Telegram service ──┤
                                  ├── Redis: HybridCache and SignalR
                                  ├── S3-compatible object storage
                                  └── metered AI provider
```

Production routes one public host by path: `/api` and `/hubs` to the API, `/mcp` to MCP, `/telegram/webhook` to the bot, and pages to Next.js. Development uses ports 3000, 5000, 5001, and 5002. Browser API calls go directly to the configured API origin. Next.js does not own authentication endpoints.

## Backend layers

| Project | Responsibility |
| --- | --- |
| `Mizan.Domain` | Entities, identity records, and domain rules |
| `Mizan.Application` | Commands, queries, validation, interfaces |
| `Mizan.Infrastructure` | EF Core, sessions, storage, email, AI, caching, outbox |
| `Mizan.Contracts` | Request records shared by service clients |
| `Mizan.Api` | HTTP controllers, authentication, authorization, SignalR |
| `Mizan.Mcp.Server` | MCP transport and adapters calling the API |
| `Mizan.Telegram` | Account linking, Telegram transport, API adapters |

Writes use MediatR commands. Controllers and tools translate their transport into those commands; they do not get independent permission or nutrition rules. OpenAPI generates frontend TypeScript contracts. Form validators remain local Zod schemas.

## Identity

`POST /api/Auth/login` verifies a PBKDF2 password and creates a session. The browser receives an opaque, httpOnly `mizan_session` cookie. The backend resolves the token through its session service and checks account status. Sessions have a seven-day sliding lifetime; deleting one revokes it. Email confirmation and password reset use single-use, hashed tokens.

`GET /api/Auth/me` returns the current user. Next.js resolves it once per render pass and forwards the cookie for server API calls. Client components receive the user through `SessionProvider`; there is no browser JWT. OAuth callbacks, email, account deletion, and session management belong to the backend.

The backend sends email through MailKit and the durable outbox. Compose maps the `SMTP_*` credentials and sender settings into `Smtp:*`; credentials never belong in frontend variables. `Smtp__Security=Auto` requires STARTTLS except on port 465, where it uses implicit TLS. `StartTls` and `SslOnConnect` select those modes on other ports; use `None` only for an explicitly trusted plaintext relay. The legacy `Smtp__UseStartTls=false` setting retains opportunistic TLS in Auto mode; prefer explicit modes for new configurations. Authentication is optional when no SMTP username is supplied. The EHLO/HELO hostname defaults to the sender address's domain, with `Smtp__LocalDomain` available as an override. Certificate validation remains enabled.

Each newly issued verification or reset token queues its own message. Retrying that exact message preserves its deduplication key. Missing SMTP configuration fails delivery outside Development, and a connection-close error after SMTP acceptance does not retry an already accepted message. SMTP acceptance does not guarantee inbox delivery. Sign-in alerts and household invitation emails are separate notification features; the current household invitation path creates in-app notifications.

External MCP clients send a user token as `Authorization: Bearer <token>`. MCP validates it through the API, then calls internal endpoints with its service key and `X-Impersonate-User`. Internal credentials are distinct from user tokens. Backend policies determine accepted authentication methods, ownership, and roles.

## Data and migrations

EF Core owns identity and application tables in one model. Keep `InitialCreate` and every subsequent additive migration; later changes get a new migration. Check model/migration agreement after entity or DbContext edits using the command in [README](../README.md#maintain-the-database).

Migrations ship inside the backend image. Development applies them on startup. In production, `docker-compose.prod.yml` runs two one-shot steps before any application container starts. `mizan-db-backup` writes a verified `pg_dump` to `backups/pre-deploy/` and keeps the newest 14 (`MIZAN_KEEP_BACKUPS`). `mizan-db-migrate` then runs the new backend image with `--migrate`, which exits 0 when the schema is current. A production API whose database is missing migrations refuses to start and names them, so a skipped step fails the deploy instead of answering 500. Restore with `pg_restore --clean --if-exists -d <db> <dump>` from the matching backup.

## Navigation and logging

Permanent navigation is Today, History, Progress, More, and Log entry. `/today?date=YYYY-MM-DD` reviews a day. `/history` lists days with nutrition, training, and measurements. `/progress` combines nutrition, bodyweight, exercise volume, and estimated one-rep max.

Meal and measurement forms open over the current route. Workouts open `/workout/active`, with drafts saved locally and through `/api/Workouts/draft`. A resume banner appears when a draft exists. Trainer, household, and notification surfaces depend on the user's data. Secondary features remain under `/more`.

A household's owner is the member with the `owner` role or, when none has it, its creator while still a member. Only the owner can delete a household, and only after every other member is gone. Deletion previews its household shopping lists and meal plans, with their entries, on the server; the owner confirms them explicitly, and a confirmation made before members, lists, or plans changed is refused. Deletion locks the household row, removes those plans, memberships, and invitations in one transaction, and clears household links from recipes and active-household preferences without deleting them.

Foods carry per-100g nutrition and a nullable owner: no owner means public catalogue; an owner means personal food. Recipes derive nutrition from ingredient foods. An unmeasured line with no food, such as "salt to taste", is left out of the totals and listed as excluded; a measured line without a linked food or a weight in grams withholds nutrition and blocks logging, and the recipe lists it. When the ingredients cannot be summed, a retained per-serving value in `recipe_nutrition_snapshots` stands in, labelled as coming from the original recipe; see [retained nutrition](#recipes-and-retained-nutrition). A preparation is a derived food with snapshotted nutrition and source recipe, enabling reuse without a recursive graph. Preparation conversion requires the finished yield in grams. Diary snapshots preserve past intake when foods or recipes change.

### Recipes and retained nutrition

v1 stored per-serving nutrition for imported recipes; v2 calculates it instead. A snapshot keeps such a value only as a fallback: it is used when a measured line cannot be resolved, never over a possible calculation, and only while the recipe's ingredient lines and serving count match the fingerprint stored with it (`RecipeFingerprint`). Any edit to a line or to servings retires it without further code. Logging a recipe through a snapshot writes one diary row with the shown values; no ingredient links or weights are invented.

`Data/Legacy/ImportLegacyRecipeNutrition.sql` loads v1 values from the pre-upgrade backup. Restore that backup's `recipes`, `recipe_ingredients`, and `recipe_nutrition` tables into a `legacy_v1` schema, back up the database, run the script, and drop the schema. It imports a value only when the recipe's lines and servings are unchanged since v1 and the value is plausible (macro energy within 25% of stated calories). It is idempotent. Recipe detail responses are cached for up to an hour, so imported values can take that long to appear.

Date-only logs use the user's IANA timezone. Missing observations remain gaps in trends. `StreakClock` shares the decay rule, and activity counters avoid recounting all history on each write.

## Consent and AI

`IDataAccessPolicy` governs nutrition, training, and body axes. Reading one's own product data and sending it to AI are separate permissions. A trainer's AI access is the intersection of client grants and client AI consent. Omitted axes are never fetched into model context.

`IAiQuotaService` reserves usage before provider calls and settles afterward. Personal, onboarding, trainer, and evaluation allowances share a global daily ceiling. Structured outputs are validated before use. Chat, photos, onboarding, and prompt evaluations share this platform; neither MCP nor Telegram has a separate provider path. See [AI](AI.md).

## Storage, caching, and jobs

`IStorageService` supports S3-compatible MinIO and R2 configuration. A public media base URL defines upload URLs and Next.js's image hostname allowlist.

`HybridCache` caches session lookups and read models including nutrition ranges, recipes, and meal plans. Writes invalidate associated tags. Viewer-specific keys include the identity or household context needed to keep cached personal results private.

Background work uses a transactional PostgreSQL outbox. Workers claim jobs with `FOR UPDATE SKIP LOCKED`, limit per-type concurrency, retry failures, and expose exhausted work through administrator jobs. Email and prompt evaluations use the outbox. Redis is not its durable store.

## Telemetry

The frontend exposes Prometheus-format process metrics (memory, event loop, GC) at `/metrics`, scraped over the internal Docker network; the reverse proxy must not route this path from the public internet. Structured JSON server logs go to stdout, already collected by the host's log pipeline.

Client-side RUM (errors, traces, web vitals) ships via Grafana Faro when `NEXT_PUBLIC_FARO_URL` is set; empty disables it entirely. Faro has no server-side SDK, so server observability stays on Prometheus metrics and logs rather than Faro.

## Billing

Paddle is the merchant of record and the authority on billing state; Mizan mirrors it.

- **Catalogue.** Plans are Paddle prices on the product tagged `custom_data.plan = "pro"`; `billing_plans` lists them. Admins manage them at `/admin/billing` (`/api/admin/billing`). Every write reaches Paddle before Mizan stores it. A price never changes in place: a new amount creates a new price and archives the old one, whose subscribers keep renewing at it. **Import from Paddle** adopts prices made in the Paddle dashboard. Discounts without a code are deals: checkout applies them by id and the pricing surfaces advertise the best one per plan. Discounts with a code apply only when entered at checkout.
- **Selling.** `GET /api/Subscriptions/plans` is public and cached under `billing-plans`, cleared by every catalogue write. The landing page, `/billing`, and checkout read it; no price id is built into the frontend. Checkout sends `custom_data.user_id`.
- **Subscribers.** `/billing` shows the plan, its price, and the next charge. Switching interval previews Paddle's proration first (`prorated_immediately`, `on_payment_failure: prevent_change`). Cancelling schedules it for the end of the period, and Pro continues until then; **Keep my subscription** removes the scheduled change. Invoices come from Paddle transactions. Card updates use the hosted portal, whose links are minted per request. Each command stores the subscription Paddle returns, so the page is current before the webhook arrives.
- **Webhooks.** `/api/webhooks/paddle` verifies the signature, deduplicates by event id, and applies the subscription entity. An event whose `updated_at` is older than the stored state is ignored, because Paddle does not guarantee delivery order. At the edge, the path must bypass bot challenges (see [deployment](#paddle-in-production)).
- **Entitlements.** `EntitlementService` resolves Free or Pro, including legacy lifetime grants. Pro covers photo analysis, the larger assistant allowance, the Telegram bot, and new coach requests. Logging, recipes, meal plans, shopping lists, and households are free, as the pricing page states. A lapsed account keeps its Telegram link and existing coach, but the bot answers with the upgrade path. Pro gates answer **402** with `errorCode: "upgrade_required"`, from commands and from the `RequirePro` policy alike.

### Paddle in production

- Backend: `PADDLE_ENVIRONMENT=production`, `PADDLE_API_KEY` (products, prices, discounts, subscriptions and transactions read/write, customer portal sessions), and `PADDLE_WEBHOOK_SECRET` from the live notification destination. Frontend build: `NEXT_PUBLIC_PADDLE_ENV=production` and the live client token.
- Cloudflare: a WAF custom rule with action **Skip** for `http.request.uri.path eq "/api/webhooks/paddle"` and `ip.src in {34.232.58.13 34.195.105.136 34.237.3.244 35.155.119.135 52.11.166.252 34.212.5.7}` (Paddle's live webhook addresses, listed in its "respond to webhooks" guide), skipping managed rules, Bot Fight Mode, and rate limiting. Keep it scoped to those IPs; the signature check still rejects anything unsigned.
- After deploying, open `/admin/billing`, run **Import from Paddle**, then set what is on sale.

## Verification

Backend integration tests exercise PostgreSQL and reset data between tests. The database name guard rejects connections without `test`. An InMemory provider cannot prove PostgreSQL lock, SQL, migration, or transaction behavior.

Component tests cover draft logic and forms. Playwright runs real Next.js against a synthetic API to exercise navigation, keyboard interaction, rendering, and browser writes. These complement backend integration tests. Live Paddle, OAuth, SMTP, AI, and Telegram verification requires their external services.
