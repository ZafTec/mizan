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

Production routes one public host by path: `/api` and `/hubs` to the API, `/mcp` to MCP, `/telegram/webhook` and `/telegram/miniapp/session` to the bot, and pages to Next.js. Development uses ports 3000, 5000, 5001, and 5002. Browser API calls go directly to the configured API origin. Next.js does not own authentication endpoints.

## Backend layers

| Project | Responsibility |
| --- | --- |
| `Mizan.Domain` | Entities, identity records, and domain rules |
| `Mizan.Application` | Commands, queries, validation, interfaces |
| `Mizan.Infrastructure` | EF Core, sessions, storage, email, AI, caching, outbox |
| `Mizan.Contracts` | Request and response records shared by the API and its service clients; no adapter declares its own copy |
| `Mizan.Api` | HTTP controllers, authentication, authorization, SignalR |
| `Mizan.Mcp.Server` | MCP transport and adapters calling the API |
| `Mizan.Telegram` | Account linking, Telegram transport, API adapters |

SMTP, the AI provider and limits, the Telegram bot service URL, push and Paddle are read at use time through `IRuntimeOptions<T>`, which lays values an administrator stored in `system_settings` (secrets encrypted with Data Protection) over configuration ([runtime settings](ADMIN.md#runtime-settings)).

Writes use MediatR commands. Controllers and tools translate their transport into those commands; they do not get independent permission or nutrition rules. OpenAPI generates frontend TypeScript contracts. Form validators remain local Zod schemas.

## Identity

`POST /api/Auth/login` verifies a PBKDF2 password and creates a session. The browser receives an opaque, httpOnly `mizan_session` cookie. The backend resolves the token through its session service and checks account status. A browser session has a seven-day lifetime and a Telegram Mini App session ([Telegram](TELEGRAM.md#mini-app)) a 24-hour one; each slides by its own lifetime when used, and deleting one revokes it. Email confirmation and password reset use single-use, hashed tokens.

`GET /api/Auth/me` returns the current user. Next.js resolves it once per render pass and forwards the cookie for server API calls. Client components receive the user through `SessionProvider`; there is no browser JWT. OAuth callbacks, email, account deletion, and session management belong to the backend.

The backend sends email through MailKit and the durable outbox. The server, credentials and sender are runtime settings (`Smtp.*`) an administrator sets in **Admin → Settings** ([runtime settings](ADMIN.md#runtime-settings)); they never reach the frontend. `Smtp.Security=Auto` requires STARTTLS except on port 465, where it uses implicit TLS. `StartTls` and `SslOnConnect` select those modes on other ports; use `None` only for an explicitly trusted plaintext relay. Authentication is optional when no SMTP username is supplied. The EHLO/HELO hostname defaults to the sender address's domain, with `Smtp.LocalDomain` available as an override. Certificate validation remains enabled.

Every email goes through `IEmailQueue`, which writes an `email_messages` row and the outbox job in the caller's unit of work; the row records recipient, subject, kind and delivery status but never the body, and administrators read it at `/admin/mail` ([admin](ADMIN.md#mail)). Each newly issued verification or reset token queues its own message. Retrying that exact message preserves its deduplication key. Missing SMTP configuration fails delivery outside Development, and a connection-close error after SMTP acceptance does not retry an already accepted message. SMTP acceptance does not guarantee inbox delivery. Sign-in alerts and household invitation emails are separate notification features; the current household invitation path creates in-app notifications.

The API is also an OAuth 2.1 authorization server (`/api/oauth/*`, [MCP](MCP.md#connect-with-oauth)). It issues opaque tokens, `mza_` access tokens and rotating `mzr_` refresh tokens, each tied to a grant and an audience. An MCP client's token is validated by the MCP service through introspection; MCP then calls internal endpoints with its service key, `X-Impersonate-User`, and `X-Mcp-Grant`, and the API loads the grant from its database. The regular service key, which the MCP service, the Telegram bot and the prices worker share, acts for an administrator as an ordinary user: the request carries the role `user`, no admin claims and no admin scope, so every adapter works for an admin account and no adapter reaches an admin endpoint with it. Only the admin service key, held by the MCP service for administrators' connections, carries the `admin` role through. Rules that depend on the role read it from the request (`ICurrentUserService.IsInRole`), never from the account row. `IHouseholdAccess` makes household access the intersection of membership and the grant, and `IDataAccessPolicy` intersects readable axes with it. Internal credentials are distinct from user tokens. Backend policies determine accepted authentication methods, ownership, and roles.

Audit entries for commands that carry passwords, codes, or tokens are redacted before they are stored (`IRedactedAudit`).

### Viewing the site as a user

An administrator can open a one-hour session as another user (`POST /api/admin/users/{id}/impersonate`, from the user's admin page). It is refused for another administrator, for oneself, for an account that cannot sign in, and from inside another view. The new session is an ordinary `user_sessions` row with `impersonator_id` set and a fixed one-hour life that never slides. The administrator's own session cookie is kept aside in `mizan_admin_session`, and `POST /api/Auth/impersonation/stop` revokes the view and puts it back, but only if it is still valid and belongs to the administrator who opened the view. Otherwise the person is signed out.

The user the server sees is the target, with the target's role, so the admin area is closed while viewing. `ImpersonationGuardMiddleware` also refuses changes that would last or take the account over: credentials and sessions (`/api/Auth`), billing (`/api/Subscriptions`), Telegram links, connected apps (`/api/oauth`, `/api/McpConnections`) and push devices. Every audited action in the view records the administrator in `audit_logs.impersonator_id`, and starting a view is itself audited. `GET /api/Auth/me` carries `impersonation`, which the app uses to show a banner on every screen with the time left and one way back.

## Data and migrations

EF Core owns identity and application tables in one model. Keep `InitialCreate` and every subsequent additive migration; later changes get a new migration. Check model/migration agreement after entity or DbContext edits using the command in [README](../README.md#maintain-the-database).

Migrations ship inside the backend image. Development applies them on startup. In production, `docker-compose.prod.yml` runs two one-shot steps before any application container starts. `mizan-db-backup` writes a verified `pg_dump` to `backups/pre-deploy/` and keeps the newest 14 (`MIZAN_KEEP_BACKUPS`). `mizan-db-migrate` then runs the new backend image with `--migrate`, which exits 0 when the schema is current. A production API whose database is missing migrations refuses to start and names them, so a skipped step fails the deploy instead of answering 500. Restore with `pg_restore --clean --if-exists -d <db> <dump>` from the matching backup.

## Navigation and logging

Permanent navigation is Today, History, Progress, More, and Log entry. `/today?date=YYYY-MM-DD` reviews a day. `/history` lists days with nutrition, training, and measurements. `/progress` combines nutrition, bodyweight, exercise volume, and estimated one-rep max.

Meal and measurement forms open over the current route. The browser adds up no nutrition: `GET /api/Meals` returns each meal type's and each logged recipe's totals (`meals`, `groups`, rounded like plan subtotals by `EntryNutrition.Sum`), and the meal form shows what `POST /api/Meals/preview` says its lines come to, by the same `MealScale` rule that stores an estimate logged by weight. The form logs what the person typed, grams of a food (`amountGrams` on `POST /api/Nutrition/log`) or servings of a recipe, and the server converts grams into servings. Workouts open `/workout/active`, with drafts saved locally and through `/api/Workouts/draft`. A resume banner appears when a draft exists. Trainer, household, and notification surfaces depend on the user's data. Secondary features remain under `/more`.

A meal plan holds day-indexed entries, each a recipe with servings, a food with grams, or a line of text, under rules that Domain code checks and every surface reports the same way. `/today` shows the active plan's day; logging an entry writes ordinary diary rows that record their origin in `FoodDiaryEntry.PlanEntryId`. A shopping list is generated from a plan by the Domain aggregator and edited by its owner or a household member allowed to edit lists. Coaches draft plans that clients accept, and templates are copied into plans. See [planning](PLANNING.md).

A household's owner is the member with the `owner` role or, when none has it, its creator while still a member. Only the owner can delete a household, and only after every other member is gone. Deletion previews its household shopping lists and meal plans, with their entries, on the server; the owner confirms them explicitly, and a confirmation made before members, lists, or plans changed is refused. Deletion locks the household row, removes those plans, memberships, and invitations in one transaction, and clears household links from recipes and active-household preferences without deleting them.

Foods carry per-100g nutrition and a nullable owner: no owner means public catalogue; an owner means personal food. Recipes derive nutrition from ingredient foods. An unmeasured line with no food, such as "salt to taste", is left out of the totals and listed as excluded; a measured line without a linked food or a weight in grams withholds nutrition and blocks logging, and the recipe lists it. When the ingredients cannot be summed, a retained per-serving value in `recipe_nutrition_snapshots` stands in, labelled as coming from the original recipe; see [retained nutrition](#recipes-and-retained-nutrition). A preparation is a derived food with snapshotted nutrition and source recipe, enabling reuse without a recursive graph. Preparation conversion requires the finished yield in grams. Diary snapshots preserve past intake when foods or recipes change.

### Recipes and retained nutrition

v1 stored per-serving nutrition for imported recipes; v2 calculates it instead. A snapshot keeps such a value only as a fallback: it is used when a measured line cannot be resolved, never over a possible calculation, and only while the recipe's ingredient lines and serving count match the fingerprint stored with it (`RecipeFingerprint`). Any edit to a line or to servings retires it without further code. Logging a recipe through a snapshot writes one diary row with the shown values; no ingredient links or weights are invented.

`Data/Legacy/ImportLegacyRecipeNutrition.sql` loads v1 values from the pre-upgrade backup. Restore that backup's `recipes`, `recipe_ingredients`, and `recipe_nutrition` tables into a `legacy_v1` schema, back up the database, run the script, and drop the schema. It imports a value only when the recipe's lines and servings are unchanged since v1 and the value is plausible (macro energy within 25% of stated calories). It is idempotent. Recipe detail responses are cached for up to an hour, so imported values can take that long to appear.

Date-only logs use the user's IANA timezone. Missing observations remain gaps in trends. `StreakClock` shares the decay rule, and activity counters avoid recounting all history on each write. A meal or workout on a day more than one day from the user's today is saved and leaves the streak alone. `DiaryDates` refuses a meal or workout date more than ten years back or a year ahead with a 400 that names the date. A meal logged without a date, on either `POST /api/Meals` or `POST /api/Nutrition/log`, lands on today in the user's zone, the day the streak counts.

## Consent and AI

`IDataAccessPolicy` governs nutrition, training, and body axes. Reading one's own product data and sending it to AI are separate permissions. A trainer's AI access is the intersection of client grants and client AI consent. Omitted axes are never fetched into model context.

`IAiQuotaService` reserves usage before provider calls and settles afterward. Personal, onboarding, trainer, and evaluation allowances share a global daily ceiling. Structured outputs are validated before use. Chat, photos, onboarding, and prompt evaluations share this platform; neither MCP nor Telegram has a separate provider path. See [AI](AI.md). Administrators can override allowances per user, household or billing plan and switch features per household; both are decided in the API ([administration](ADMIN.md)).

## Storage, caching, and jobs

`IStorageService` supports S3-compatible MinIO and R2. Its endpoint, keys and bucket are runtime settings (`Storage.*`); the service builds its S3 client when it first needs one and again after those settings change, and refuses uploads while they are empty. A public media base URL defines upload URLs and Next.js's image hostname allowlist. Assets that ship with a release, such as the default achievement badges under `frontend/public/badges`, are stored as site-relative paths and are not uploaded; `IAppUrls.Asset` turns them into absolute URLs on the app origin when the API returns them, so every client fetches a default and an upload the same way.

`HybridCache` caches session lookups and read models including nutrition ranges, recipes, and meal plans. Writes invalidate associated tags. Viewer-specific keys include the identity or household context needed to keep cached personal results private.

Background work uses a transactional PostgreSQL outbox. Workers claim jobs with `FOR UPDATE SKIP LOCKED`, limit per-type concurrency, retry failures, and expose exhausted work through administrator jobs. Email (identity mail, announcements, contact notices and replies), push, prompt evaluations, the plan's evening reminder, and messages to a linked Telegram chat use the outbox; a job can wait for a `run_after` time. A handler can record that its job gave up through `OnDeadLetteredAsync`, which is how the email log marks a message Failed. `INotificationWriter` is the one place a notification fans out: it stages the row, a push job, a Telegram message and an email for each channel the person allows, all in the caller's unit of work. Redis is not its durable store.

### Notifications

A notification is one `NotificationMessage` (`Mizan.Contracts/Notifications`): a kind, a title, an optional body and image, up to two actions (`label`, app `path`) and optional facts (`label`, `value`). `NotificationMessages` (Application) builds each one; the row keeps all of it and every channel renders the same message: the web panel shows the image, facts and action buttons, Telegram sends a card ([Telegram](TELEGRAM.md#notifications-in-telegram)), push carries the title and body, and email uses one template.

- **Kinds and preferences**: `NotificationKinds` (Domain) lists the kinds (achievements, reminders, planning, coaching, household, social, account, marketing) and each one's default channels. A person overrides them per kind and channel in `notification_preferences`; marketing starts off everywhere. Quiet hours (`users.quiet_hours_start/end`, in the person's time zone) hold Telegram and push until they end; the in-app row is written at once and email is not delayed. Telegram still needs the chat linked and `notifyByTelegram` on; email needs a verified address. `GET`/`PUT /api/Notifications/preferences`, the Settings **Notifications** section, the MCP tools `get_notification_preferences` and `update_notification_preferences`, and `/notifications` in the bot all use the same command.
- **Schedules**: `notification_schedules` holds who sends (`owner`), to whom (`audience`), what (`message`, the template), the channels, the trigger (`once`, `daily`, `weekdays` at a local time, `interval` every N minutes, or `event` with an offset), `enabled`, `next_run_at` and `last_run_at`. `NotificationSchedulerService` runs `NotificationScheduler` every 15 minutes: each due occurrence within two hours becomes one `notification-delivery` outbox job per recipient and channel, keyed `schedule:{scheduleId}:{occurrence}:{userId}:{channel}`, so a repeated scan adds nothing. Event triggers ask an `INotificationEventSource`; the evening reminder is the built-in schedule `evening-reminder` on the event `plan.eating_window_end` ([Telegram](TELEGRAM.md#evening-reminder)), created on the first scan. Disabling it stops reminders, including ones already queued.
- **Campaigns**: an administrator's campaign ([admin](ADMIN.md#notification-campaigns)) is a platform schedule with a `campaign` jsonb column (purpose, status, time zone, confirmed audience, who and when) and a segment audience. `CampaignAudiences` narrows the candidates in SQL and `CampaignAudienceRules` (Domain) decides each one, so the preview count and the send agree. The scheduler stages a campaign's occurrence through `CampaignStager`, spreading each channel's jobs by `CampaignRules.PerMinute`; event campaigns use the `user.signed_up`, `user.inactive` and `subscription.pro_ends` event sources. A delivery checks the campaign is still active when it runs, and its notification row carries `schedule_id` for the report.
- **Achievement pictures**: a card uses the achievement's badge. An achievement without an icon gets a 512 px PNG drawn once (`AchievementCardImage`, no image library), stored through `IStorageService` and cached in `achievements.card_image_url`; renaming the achievement clears it. Nothing is drawn unless storage is configured and `Storage.PublicBaseUrl` is HTTPS, since Telegram must fetch it.

### Reminders

A reminder is a person's own notification schedule: `owner` user, type `reminder`, kind `reminders`, and a `reminder` JSON column (`ReminderDetails`) with its kind (`workout`, `measurement`, `pre_cook`, `meal_log`, `custom`) and what it is tied to (a workout template, a plan's prep session, a meal). The scheduler above fires it; there is no second scheduler.

- **Rules** live in `ReminderRules` and `ReminderKinds` (Domain): a request's repeat (`once`, `daily`, `days`, `every`) and local time become a trigger in the person's time zone; anything more often than hourly is refused; a pre-cook reminder fires once, `leadMinutes` (default 60) before the prep time (default 18:00) on the session's plan day. Free keeps 3 reminders on and Pro 30 (on, with a firing still to come); over the cap Free gets `402 upgrade_required`. Quiet hours and the reminders preferences apply as to any notification.
- **Commands** (`Mizan.Application/Notifications/Reminders.cs`): create, update, delete, list, snooze, skip today and done, through `/api/Reminders` (`POST` takes an `Idempotency-Key`; `PUT`/`DELETE /{id}`; `POST /{id}/snooze|skip|done`). Snooze (30 minutes by default) queues the reminder again on its channels under the usual delivery key, so a second press in the same minute adds nothing. Skip today sets `skip_until` to the person's midnight and deliveries before it are dropped; done does the same and ends a one-off reminder. Summaries ("Weekdays at 07:00") and next times are worded by the API.
- **Surfaces** call the same commands and hold no rules: Settings **Reminders** and **Remind me** on a workout template, the measurements page and a plan's cooking session; MCP `list_reminders` and `create_`/`update_`/`delete_reminder`; `/remind` and `/reminders` in the bot, and **Done**, **Snooze 30 min** and **Skip today** on a reminder's Telegram card ([Telegram](TELEGRAM.md#reminders)); the assistant's `create_reminder` and `list_reminders` tools ([AI](AI.md#reminder-tools)). Any of them could be replaced without touching the reminder rules.

## Native clients

The Android app signs in as the first-party OAuth client `mizan-android` (Authorization Code with PKCE, `resource` set to the API issuer), through a browser tab so the web consent and sign-in screens are reused. Its access token is sent as `Authorization: Bearer`; SignalR may also pass it as `access_token` on `/hubs`. A token issued for MCP is refused here. Disconnecting the app under Connected apps ends its access at once.

- **OpenAPI**: `/swagger/v1/swagger.json` is served in development, and in production only when `OpenApi__Enabled=true`. Generate client types from it.
- **Retrying writes**: send an `Idempotency-Key` header (up to 128 printable characters) on a write. A retry with the same key and request gets the first answer back with `Idempotent-Replayed: true`; the same key with a different request is `422 idempotency_key_reused`; a duplicate arriving while the first runs is `409 idempotency_in_progress`. Server errors are not remembered, and keys expire after 24 hours.
- **Staying current**: `GET /api/Sync/changes?since=` returns diary entries, workouts, body measurements, and notifications changed since a cursor, plus tombstones for deleted diary entries, workouts, and measurements. Omit `since` the first time, send back `nextSince`, and repeat while `hasMore`. Items are upserts, so redelivery is harmless. An app away longer than 90 days gets `resyncRequired` and starts again. Recipes, meal plans, and shopping lists are not in the feed yet.
- **Push**: `POST /api/Devices` registers an FCM token (an upsert; a token moves to whoever registers it last) and `DELETE /api/Devices/{id}` removes it on sign-out. Creating a notification queues a `push` outbox job when Firebase is configured (`Push.FcmProjectId`, `Push.FcmServiceAccountJson` in **Admin → Settings**); a device the provider no longer knows is forgotten. Unconfigured, nothing is queued.
- **3D models**: administrators upload `.glb` files to `POST /api/Uploads/model` (glTF 2 binary, checked by its bytes, up to `Storage.MaxModelBytes`, default 30 MB) into the `models/` folder, then attach one with `PUT /api/Exercises/{id}/model`. Exercises expose `modelUrl`. In the app, an administrator does both from **Admin → Exercises**: the edit form (and **New exercise**, which creates a system exercise in one step) uploads the picture and the `.glb` and attaches them on save, so assets made later can be added to any exercise without a deploy.

## Errors

Every error from our code has the body `{ "errorCode": "...", "error": "..." }`, with extra fields where useful (`errors` for field messages). That includes unreadable request bodies (`invalid_request`), failed validation (`validation_failed`), and the framework's own `401`, `403`, `404`, and `405`. Result objects such as `{ success: false, message }` keep their fields and gain the two standard ones. The OAuth endpoints are the exception: they use `error` and `error_description` as RFC 6749 requires.

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

- Backend, in **Admin → Settings → Billing**: `Paddle.Environment=production`, `Paddle.ApiKey` (products, prices, discounts, subscriptions and transactions read/write, customer portal sessions), and `Paddle.WebhookSecret` from the live notification destination, and `Paddle.ClientToken`, the live client-side token the browser opens checkout with (served by `GET /api/Subscriptions/checkout-settings`).
- Cloudflare: a WAF custom rule with action **Skip** for `http.request.uri.path eq "/api/webhooks/paddle"` and `ip.src in {34.232.58.13 34.195.105.136 34.237.3.244 35.155.119.135 52.11.166.252 34.212.5.7}` (Paddle's live webhook addresses, listed in its "respond to webhooks" guide), skipping managed rules, Bot Fight Mode, and rate limiting. Keep it scoped to those IPs; the signature check still rejects anything unsigned.
- After deploying, open `/admin/billing`, run **Import from Paddle**, then set what is on sale.

## Verification

Backend integration tests exercise PostgreSQL and reset data between tests. The database name guard rejects connections without `test`. An InMemory provider cannot prove PostgreSQL lock, SQL, migration, or transaction behavior.

Component tests cover draft logic and forms. Playwright runs real Next.js against a synthetic API to exercise navigation, keyboard interaction, rendering, and browser writes. These complement backend integration tests. Live Paddle, OAuth, SMTP, AI, and Telegram verification requires their external services.
