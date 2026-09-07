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

## Navigation and logging

Permanent navigation is Today, History, Progress, More, and Log entry. `/today?date=YYYY-MM-DD` reviews a day. `/history` lists days with nutrition, training, and measurements. `/progress` combines nutrition, bodyweight, exercise volume, and estimated one-rep max.

Meal and measurement forms open over the current route. Workouts open `/workout/active`, with drafts saved locally and through `/api/Workouts/draft`. A resume banner appears when a draft exists. Trainer, household, and notification surfaces depend on the user's data. Secondary features remain under `/more`.

Foods carry per-100g nutrition and a nullable owner: no owner means public catalogue; an owner means personal food. Recipes derive nutrition from ingredient foods. A preparation is a derived food with snapshotted nutrition and source recipe, enabling reuse without a recursive graph. Preparation conversion requires the finished yield in grams. Diary snapshots preserve past intake when foods or recipes change.

Date-only logs use the user's IANA timezone. Missing observations remain gaps in trends. `StreakClock` shares the decay rule, and activity counters avoid recounting all history on each write.

## Consent and AI

`IDataAccessPolicy` governs nutrition, training, and body axes. Reading one's own product data and sending it to AI are separate permissions. A trainer's AI access is the intersection of client grants and client AI consent. Omitted axes are never fetched into model context.

`IAiQuotaService` reserves usage before provider calls and settles afterward. Personal, onboarding, trainer, and evaluation allowances share a global daily ceiling. Structured outputs are validated before use. Chat, photos, onboarding, and prompt evaluations share this platform; neither MCP nor Telegram has a separate provider path. See [AI](AI.md).

## Storage, caching, and jobs

`IStorageService` supports S3-compatible MinIO and R2 configuration. A public media base URL defines upload URLs and Next.js's image hostname allowlist.

`HybridCache` caches session lookups and read models including nutrition ranges, recipes, and meal plans. Writes invalidate associated tags. Viewer-specific keys include the identity or household context needed to keep cached personal results private.

Background work uses a transactional PostgreSQL outbox. Workers claim jobs with `FOR UPDATE SKIP LOCKED`, limit per-type concurrency, retry failures, and expose exhausted work through administrator jobs. Email and prompt evaluations use the outbox. Redis is not its durable store.

## Billing

Paddle webhooks at `/api/webhooks/paddle` verify signatures, deduplicate events, and update subscription state. `EntitlementService` determines Free or Pro access, including legacy lifetime grants. `POST /api/Subscriptions/portal` mints uncached customer portal links. Current sales offer Free and monthly Pro; existing lifetime records remain supported.

Preserve current entitlements; the proposed Free allowance of two household members and one trainer relationship remains an open product decision.

## Verification

Backend integration tests exercise PostgreSQL and reset data between tests. The database name guard rejects connections without `test`. An InMemory provider cannot prove PostgreSQL lock, SQL, migration, or transaction behavior.

Component tests cover draft logic and forms. Playwright runs real Next.js against a synthetic API to exercise navigation, keyboard interaction, rendering, and browser writes. These complement backend integration tests. Live Paddle, OAuth, SMTP, AI, and Telegram verification requires their external services.
