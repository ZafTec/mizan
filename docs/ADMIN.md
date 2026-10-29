# Administration

The admin area at `/admin` is for administrators only: every page and every `/api/admin` route requires the `admin` role, and an administrator viewing the site as a user cannot reach it. Its MCP tools are named `admin_*` and need the `admin` scope. Each section below names its page, its API, and its MCP tools; admin features have no Telegram commands, because the bot serves end users.

## Overview and health

`/admin` shows the account counts and a system health grid from `GET /api/admin/overview/health` (MCP `admin_get_system_health`). Each check is `ok`, `degraded`, `down` or `notConfigured`, with a detail line, and the result is cached for 30 seconds.

| Check | What is measured | Degraded or down when |
| --- | --- | --- |
| `database` | A `SELECT 1` round trip | Slower than one second, or no answer |
| `redis` | A ping, when `ConnectionStrings:Redis` is set | Slower than one second, or no answer |
| `outbox` | Pending and dead-lettered jobs, oldest due job | Any dead-lettered job or work waiting over 10 minutes; down after an hour |
| `email` | `Smtp.Host` set, failed messages in 24 hours | Any failure in the last day |
| `ai` | Provider set, today's tokens and cost | 80% of a global daily ceiling; down at the ceiling |
| `telegram` | The bot service's own `GET /health`, when `Telegram.BotServiceUrl` is set | No answer |
| `push` | Firebase credentials set | |
| `prices` | `Prices:WorkerUrl` set, last successful source run | No success yet, or none in 48 hours |
| `storage` | `Storage.ServiceUrl` set | |

`notConfigured` means there is nothing to measure. The tile says "Not configured" and links to the page where it is set up, never a number. The thresholds live in `SystemHealthRules` (Domain).

## Mail

Every email the system sends goes through `IEmailQueue`, which writes an `email_messages` row (recipient, subject, kind, status, attempts, last error, outbox job) and the outbox job in the caller's unit of work. The body stays in the job only, because it can hold a sign-in link. The email handler marks the row `Sent`, or records the attempt and the redacted error and leaves it `Queued` while it retries; when the job gives up the row turns `Failed`. Kinds include `auth.verify`, `auth.reset`, `auth.signin`, `admin.announcement`, `contact.received` and `contact.reply`.

`/admin/mail` lists the log with recipient search and status, kind and date filters (`GET /api/admin/mail`, `GET /api/admin/mail/stats`, `GET /api/admin/mail/{id}`). A row opens its detail with the error. **Retry** (`POST /api/admin/mail/{id}/retry`) resets the message's outbox job; it is refused with 409 for a sent message, one already queued, and one whose job was deleted with its body.

**Compose announcement** takes a subject, a plain-text body and an audience: `all`, `pro`, `free`, `household:{id}` or `user:{id}`. Unconfirmed addresses and suspended accounts are always left out. The confirmation step shows the recipient count from `GET /api/admin/mail/announcements/audience` before anything is queued. `POST /api/admin/mail/announcements` queues one email per recipient with the dedupe key `announcement:{announcementId}:{userId}`; sending the same announcement id again answers with the first count and queues nothing. An announcement to `all` is limited to one an hour (429 with `Retry-After`). MCP: `admin_list_mail`, `admin_retry_mail`, `admin_send_announcement`.

## Contact

The public page `/contact`, linked from the footer, posts to `POST /api/Contact`. Signed out is fine; a signed-in sender is linked to their account from the session, never from the form. Fields are capped (name 120, email 320, subject 160, message 5,000 characters), one address may send five messages an hour (counted by a hash of the IP address, which is never stored), and a filled hidden `website` field is answered as usual and dropped. The submission and one `contact.received` email to every administrator are saved in one transaction.

`/admin/contact` lists messages with a status filter (`New`, `Handled`) and search (`GET /api/admin/contact`, `GET /api/admin/contact/{id}`). **Send reply** (`POST /api/admin/contact/{id}/reply`) queues a `contact.reply` email to the sender from the configured sender address and marks the message handled; a message takes one reply. **Mark handled** (`POST /api/admin/contact/{id}/handled`) closes it without one. MCP: `admin_list_contact`, `admin_reply_contact`, `admin_mark_contact_handled`.

## Households, users and support notes

`/admin/households` searches by household name or any member's name or email (`GET /api/Households/admin/all?search=`, with paging and sorting by name, members or creation). The detail page reads `GET /api/Households/admin/{id}`: the owner (by the same rule as deletion), members with roles and join dates, pending invitations, and meal plan and shopping list counts.

The user detail page shows a suspension with its reason and end. **Suspend account** takes a reason and an optional end; **Lift suspension** clears it. Both use `PATCH /api/admin/users/{id}`.

Both detail pages carry support notes: notes administrators leave for each other about a user or a household (`GET /api/admin/support-notes?subjectType=User|Household&subjectId=`, `POST`, `PUT /{id}`, `DELETE /{id}`). Only administrators can read or write them, any administrator may edit or delete one, and every write is audited. MCP: `admin_list_support_notes`, `admin_add_support_note`.

## Notification schedules

Scheduled notifications live in `notification_schedules` ([architecture](ARCHITECTURE.md#notifications)). The built-in `evening-reminder` is created by the scheduler's first scan; setting its `enabled` to false stops every evening reminder, including those already queued. Each delivery is a `notification-delivery` job on `/admin/jobs`. Achievements without an icon get a drawn card picture in Telegram only when storage is configured and `Storage.PublicBaseUrl` is HTTPS; otherwise their cards are sent as text.

## Notification campaigns

`/admin/notifications` lists campaigns and lifecycle messages with their phase (draft, scheduled, live, sent, paused, cancelled) and delivery totals; **New campaign** opens the editor. API: `api/admin/notification-campaigns` (`GET`, `GET /{id}`, `POST`, `PUT /{id}`, `POST /audience`, `POST /preview`, `POST /{id}/test|activate|pause|resume|cancel`). MCP: `admin_list_notification_campaigns`, `admin_get_notification_campaign`, `admin_preview_audience`, `admin_create_notification_campaign`, `admin_send_test_notification`, `admin_activate_notification_campaign`, `admin_pause_campaign`, `admin_resume_campaign`, `admin_cancel_campaign`. The rules are in `Mizan.Domain/Notifications/Campaigns.cs`.

- **Purpose**: *lifecycle* messages are account notifications; *news and offers* use the `marketing` kind and reach only people who turned it on for that channel. Marketing email carries a one-tap opt-out link to `/notifications/opt-out?token=`, which turns marketing off on every channel after one press (the page does nothing on load, so mail scanners cannot trigger it); the in-app item and the Telegram card carry a **Turn off news and offers** button.
- **Audience**: filters on plan (free, pro, lifetime), sign-up date range, inactive for at least N days (no visit and nothing logged), a linked Telegram chat, household membership and time zones; no filter means everyone. Suspended accounts are always left out. The editor shows the matched and reachable counts from `POST /audience` as it changes; reachable means the kind's preferences and the channel allow it.
- **Message**: title (150 characters), body (500), optional image and up to two actions, with the placeholders `{firstName}` (first word of the name, or "there") and `{appLink}`; any other placeholder is refused. `POST /preview` renders it per channel as each will send it (in-app, push, Telegram, email), with warnings.
- **Timing** (`send`): `now` (when started), `once` at a local date and time, `daily`, `days` (chosen weekdays), `every` N minutes, or `event` with an offset in days: after sign-up (`user.signed_up`), after N quiet days (`user.inactive`), or before a non-renewing Pro subscription ends (`subscription.pro_ends`). Times are in the campaign's time zone. Quiet hours hold Telegram and push as for every notification.
- **Test send**: sends the message to the administrator on each selected channel, bypassing preferences and quiet hours, and reports channels it could not use (no linked chat, no phone, Telegram not configured). Tests are not counted in the report.
- **Start**: the confirmation names the reachable count, and `activate` sends it back; when the audience changed the API answers 409 and nothing is queued. A timed campaign with nobody reachable cannot start. Event campaigns reach anyone whose event falls after activation.
- **Pacing**: deliveries are spread per channel: in-app 1,000 a minute, push 600, Telegram 1,200, email 120.
- **Pause, resume, cancel**: pause stops staging, and deliveries already queued are dropped when their time comes; resume plans from the next occurrence after now, so occurrences due while paused are not sent; cancel is final. Only a draft or paused campaign can be edited.
- **Report**: per channel, *queued* (pending, running or failed jobs that will retry), *sent*, *failed* (dead-lettered), *skipped* (the delivery ran but the channel sent nothing: preference off, no address or chat, or the campaign was paused) and *opened* (read in the app; in-app only).
- Every create, change, test and state change is in the audit log under its command name.

## Runtime settings

`/admin/settings` holds every setting that is not needed before the API can reach its database, and a change applies without a redeploy. `RuntimeSettingDefinitions` (Domain) lists each key with its type, rules, built-in default, page section and whether it is a secret; the API, the page, the MCP tools and `IRuntimeOptions<T>` all read that one catalog. A key is the options path with dots: `Smtp.Host` fills `SmtpOptions.Host`.

| Section | Keys |
| --- | --- |
| Email | `Smtp.Host`, `Port`, `Security`, `Username`, `Password`, `FromAddress`, `FromName`, `LocalDomain` |
| AI provider | `Ai.BaseUrl`, `ApiKey`, `Model`, `MaxOutputTokens`, `TimeoutSeconds`, `SupportsTemperature`, `ReasoningEffort`, prompt and completion prices |
| AI limits | `Ai.GlobalDailyTokens`, `Ai.GlobalDailyCostMicros`, and `DailyRequests` and `DailyTokens` for `Free`, `Pro`, `PlanningFree`, `PlanningPro`, `Onboarding`, `Trainer`, `Eval` |
| Storage | `Storage.ServiceUrl`, `Region`, `AccessKeyId`, `SecretAccessKey`, `Bucket`, `ForcePathStyle`, `PublicBaseUrl`, `PresignedUrlMinutes`, `MaxUploadBytes`, `MaxModelBytes` |
| Telegram | `Telegram.BotToken`, `BotUsername`, `UseWebhook`, `WebhookSecret`, `MiniAppUrl`, `MiniAppMaxAgeSeconds`, `MiniAppSessionHours`, `BotServiceUrl` |
| Push | `Push.FcmProjectId`, `Push.FcmServiceAccountJson` |
| Billing | `Paddle.Environment`, `ApiKey`, `WebhookSecret`, `ClientToken`, `GraceDays`, `LifetimePriceId` |
| Price crawler | `Prices.UserAgent`, `RequestDelayMs`, `MaxProductsPerRun` |
| Sign-in | `Authentication.Google.ClientId`, `Authentication.Google.ClientSecret`, `Authentication.GitHub.ClientId`, `Authentication.GitHub.ClientSecret` |
| Connected apps | `OAuth.AccessTokenMinutes`, `RefreshTokenDays`, `RequestMinutes`, `CodeSeconds`, `AllowMetadataClients` |
| Background jobs | `Outbox.PollSeconds`, `MaxAttempts`, `BackoffSeconds`, `StaleAfterMinutes` |

A section whose feature is off says so at its top ("Storage is not configured. Image uploads are refused."), from `RuntimeSettingGroupDto.Notice`, until the keys it names are set. The OAuth issuer and MCP resource follow `App__PublicUrl`; `Outbox__Enabled` stays in the environment.

**Precedence.** A value stored in `system_settings` wins; without one, the catalog's built-in default applies. The environment and `appsettings*.json` are never read for these keys. `GET /api/admin/settings` reports each key's value and its source (`database`, `default` or `unset`). `PUT /api/admin/settings/{key}` stores a value after the catalog checks it (port range, URL or host shape, token characters, enum, number range) and answers 400 `validation_failed` with the catalog's message otherwise. `DELETE` removes the stored value, so the default applies again. Each set and clear is audited with the key as the entity id; a secret's value is stored in the audit row as `[redacted]`.

**Use time.** Consumers read `IRuntimeOptions<T>` when they act: the SMTP sender, the AI provider, `IAiQuotaService` (ceilings and tier limits), the eval runner, the storage service (it builds a new S3 client when the storage settings change), OAuth token issue, the Mini App session, the outbox dispatcher, the Telegram notifier, the push sender, the Paddle client, entitlements and webhook verification, and Google and GitHub sign-in. Both sign-in schemes are always registered; before each sign-in request (`/api/Auth/external/*` and the provider callback `/api/auth/callback/*`) `ExternalSignInSettings` reads the settings and drops the handler's cached options when they changed, so a new client id or secret applies to the next sign-in without a restart. A provider without both values is off: its button returns to `/login?error=external_unavailable` and its callback answers 404. The browser reads the Paddle client token and environment from `GET /api/Subscriptions/checkout-settings` when checkout first opens. Each instance keeps the table in memory; a write drops that copy and publishes on the Redis channel `mizan:runtime-settings:changed`, so the other instances drop theirs within moments. The copy also expires after 30 seconds while subscribed, and after one second when Redis is absent.

**Other services.** The Telegram bot reads its token, webhook switch and secret, and Mini App settings from `GET /api/internal/settings/telegram-bot` with the service key, at startup and every five minutes. A change to a `Telegram.*` key also calls the bot's `POST /internal/settings-changed` when `Telegram.BotServiceUrl` is set, so the bot reads them again and registers its webhook at once ([Telegram](TELEGRAM.md#configure-and-run)). The prices worker reads `GET /api/internal/settings/prices` before each run, and **Run now** sends them with the request. Both routes refuse a call that acts for a user. Anyone holding the shared service key (`Mcp__ServiceApiKey`) can read the bot token through the first route; that key already lets its holder act as any linked user, so keep it on the internal network like the token itself.

**Secrets.** `Smtp.Password`, `Ai.ApiKey`, `Storage.SecretAccessKey`, `Telegram.BotToken`, `Telegram.WebhookSecret`, `Push.FcmServiceAccountJson`, `Paddle.ApiKey`, `Paddle.WebhookSecret`, `Authentication.Google.ClientSecret` and `Authentication.GitHub.ClientSecret` are encrypted with ASP.NET Core Data Protection before they are stored. The API returns only `hasValue` for them, and `IRuntimeSettings` refuses to hand one to Application code as text; only the options overlay in Infrastructure and the internal settings routes decrypt them. The key ring lives in the `data_protection_keys` table, so it survives restarts and every instance shares it. A database backup therefore carries the keys with the secrets: the encryption keeps secrets out of API responses, logs, the audit log and casual queries, not away from someone holding the whole database. To keep the ring elsewhere, set `DataProtection__KeysPath` (`DATA_PROTECTION_KEYS_PATH` in production Compose) to a directory that every API instance mounts. Losing the ring makes stored secrets unreadable; each must then be entered again.

**Stays in the environment.** Only what is needed before the database is reachable, or what a service needs to reach the API; [README](../README.md#environment) lists each with its reason. `ConfigurationAllowlistTests` fails when the API, the bot or the prices worker binds an options section from configuration that is not on its allowlist.

**Tests.** **Send test email** (`POST /api/admin/settings/smtp/test`, MCP `admin_test_smtp`) queues one message to the signed-in administrator through the outbox with the settings as they stand and returns the job id; **Jobs** shows whether it was delivered. **Test provider** (`POST /api/admin/settings/ai/test`) makes one small call on the evaluation line, reserved and settled through `IAiQuotaService` like any other, and reports `reachable` with the model and latency, or an error class (`not_configured`, `quota_exceeded`, `timeout`, `unreachable`, `http_401` and so on) with `providerMessage`, the provider's reason cut to 300 characters with secrets removed. When the plain reply works it sends a second request with a trivial `ping` tool and reports it as `toolCall` (`passed`, `errorClass`, `providerMessage`, `latencyMs`), because the chat sends tools and some configurations refuse them ([provider errors](AI.md#provider-errors)).

MCP has `admin_list_settings`, `admin_set_setting`, `admin_clear_setting` and `admin_test_smtp`. Telegram has no admin surface; the bot serves end users.

### First administrator

There is no default administrator. On a new deployment, after the migration, run:

```sh
docker compose -f docker-compose.prod.yml run --rm --no-deps mizan-backend --create-admin you@example.com
```

`Mizan.Api --create-admin <email>` creates a verified account with the `admin` role, or promotes an existing one, and prints a link that sets its password. The link works once, for 24 hours, and goes to the terminal only, never to the log. It needs no email settings, so the administrator can then sign in and fill in the rest of this page.

### Moving settings out of the environment

Earlier releases read email, storage, AI, Telegram, push, billing (including the frontend build's `NEXT_PUBLIC_PADDLE_*`) and Google and GitHub sign-in from environment variables. Those variables are now ignored. To keep a running deployment's values, copy them into the settings once, before the new release serves traffic:

```sh
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml run --rm --no-deps mizan-backend --import-env-settings
docker compose -f docker-compose.prod.yml up -d
```

`Mizan.Api --import-env-settings` reads each key from its old configuration path (`Smtp__Host`) and from the `.env` names the Compose files used to map (`SMTP_HOST`, `STORAGE_SECRET_ACCESS_KEY`, `AI_API_KEY`, `TELEGRAM_BOT_TOKEN`, `PADDLE_API_KEY`, `NEXT_PUBLIC_PADDLE_CLIENT_TOKEN`, `GOOGLE_CLIENT_ID`, `GITHUB_CLIENT_SECRET`, `TelegramBot__BotToken` and the rest), so the container must still see them. The production backend loads `.env`, so keep the old lines there until the import has run. The command encrypts secrets, never prints a value, leaves a key that already has a stored value alone, and may be run again. A value the catalog refuses is skipped and named, and the command then exits with 1. Afterwards remove the old variables from `.env`.
## Feature flags

A flag switches one feature for everyone or for one household. The flags are listed in code (`Mizan.Domain/Features/FeatureFlags.cs`) and any other key is refused with 404:

| Key | What it switches |
| --- | --- |
| `planning.generate` | Drafting meal plans with the assistant (`GenerateMealPlanCommand`: the web, MCP `generate_meal_plan`, Telegram `/planme`, the chat tool) |
| `prices` | Food price lookups and price reports (`GetFoodPriceQuery`, `CreatePriceReportCommand`) |
| `telegram.bot` | Every write the bot makes as a linked user, and the Mini App sign-in |
| `ai.assistant` | Assistant chat (`SendAiChatMessageCommand`) and food analysis from a photo or a sentence |
| `social.feed` | Reading the feed, posting, reactions and comments |

A household gets its own override when it has one, then the default an administrator stored, then the default in the catalog. The table holds only what an administrator changed, so a new flag needs no seed. The household of a request is the caller's active household (or their first by name); someone in no household gets the default. Answers are cached for up to a minute and every flag write clears them.

The check lives in one place, `FeatureGate`. A MediatR request marked `[RequiresFeature]` is checked by `FeatureGateBehavior`; the food analysis actions carry the same attribute and are checked by the API's `FeatureGateFilter`, which also holds every non-GET call that carries `X-Mizan-Surface: telegram` (the bot sends it on every call as a user) to `telegram.bot`. A feature that is off answers `403` with `errorCode: "feature_disabled"`, `feature`, and the message "This feature is turned off for your household." The web and the bot show that message as it is.

| Route | Purpose |
| --- | --- |
| `GET /api/admin/feature-flags` | Every flag with its default, the catalog default and its override counts |
| `PUT /api/admin/feature-flags/{key}` | Set the default (`{ enabled }`) |
| `GET /api/admin/feature-flags/{key}/overrides` | The households with their own answer |
| `PUT`, `DELETE /api/admin/feature-flags/{key}/overrides/{householdId}` | Set or remove one household's override |

MCP: `admin_list_feature_flags`, `admin_set_feature_flag`, `admin_set_feature_flag_override`, `admin_clear_feature_flag_override`. The page `/admin/feature-flags` lists the flags with a default switch, the override count, and an override list per flag with a search by household name to add one. Telegram has no admin commands; the bot only shows the API's message.

## Usage limits

An override changes the AI allowance of one user, one household, or every subscriber of one billing plan. It sets any of four caps: requests a day, tokens a day, cost a day and cost a month (costs in millionths of the provider's currency unit). A cap left empty inherits. An override is for one allowance line (`Personal`, `Planning`, `Onboarding`, `TrainerClient`, `Eval`) or, with no line, for every line; one subject has at most one override per line.

Each cap is composed field by field: the user's override, then their household's, then their plan's, then the tier default from `AiOptions` (see [AI](AI.md#metering)). Within one scope the override for the line wins over the one for every line. A plan override applies while the subscriber is Pro on that catalogue plan (matched by Paddle price). `IUsageLimits` does the composition and `IAiQuotaService` enforces the result in the same reservation every provider call makes; there is no second enforcement path.

| Route | Purpose |
| --- | --- |
| `GET /api/admin/usage-limits?scope=Plan\|Household\|User` | Every plan, or every household or user with an override, with the limits in force per line and the scope each value came from |
| `GET /api/admin/usage-limits/{scope}/{subjectId}` | One subject, with or without overrides |
| `PUT /api/admin/usage-limits/{scope}/{subjectId}` | Create or replace the override for the line in the body |
| `DELETE /api/admin/usage-limits/{scope}/{subjectId}?line=` | Remove one override (no line: the every-line row) |

MCP: `admin_list_usage_limits`, `admin_set_usage_limit`, `admin_clear_usage_limit`. The page `/admin/usage-limits` has tabs Plans, Households and Users; a plan is shown against the Pro defaults, a household against the Free defaults, and a user against their own tier, household and plan.

## Usage

`GET /api/admin/usage` reads the AI usage ledger between two UTC dates, inclusive (default: this month), grouped by `household`, `user`, `plan` or `feature`, and optionally narrowed with `householdId` or `userId`. Each group has requests, tokens, cost and refusals; a refusal is a call an allowance or the global ceiling turned away, stored as a zero-cost `Refused` row that never counts as a request. The plan is the user's current one ("Free", a catalogue plan, or "Pro (other)"). At most 366 days are read at a time.

`GET /api/admin/usage/export` takes the same filters and streams RFC 4180 CSV in UTF-8: the header `group,key,label,requests,tokens,cost_micros,refusals`, then one row per group. A label a spreadsheet would read as a formula is prefixed with an apostrophe.

MCP: `admin_get_platform_usage`. The page `/admin/usage` has the date range, the grouping, the table and **Export CSV**, which downloads through the API with the session cookie. The household and user admin pages show a "Usage this month" block that links here with their filter set.
