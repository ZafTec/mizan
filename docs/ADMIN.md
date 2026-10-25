# Administration

The admin area at `/admin` is for administrators only: every page and every `/api/admin` route requires the `admin` role, and an administrator viewing the site as a user cannot reach it. Its MCP tools are named `admin_*` and need the `admin` scope. Each section below names its page, its API, and its MCP tools; admin features have no Telegram commands, because the bot serves end users.

## Overview and health

`/admin` shows the account counts and a system health grid from `GET /api/admin/overview/health` (MCP `admin_get_system_health`). Each check is `ok`, `degraded`, `down` or `notConfigured`, with a detail line, and the result is cached for 30 seconds.

| Check | What is measured | Degraded or down when |
| --- | --- | --- |
| `database` | A `SELECT 1` round trip | Slower than one second, or no answer |
| `redis` | A ping, when `ConnectionStrings:Redis` is set | Slower than one second, or no answer |
| `outbox` | Pending and dead-lettered jobs, oldest due job | Any dead-lettered job or work waiting over 10 minutes; down after an hour |
| `email` | `Smtp:Host` set, failed messages in 24 hours | Any failure in the last day |
| `ai` | Provider set, today's tokens and cost | 80% of a global daily ceiling; down at the ceiling |
| `telegram` | The bot service's own `GET /health`, when `Telegram:BotServiceUrl` is set | No answer |
| `push` | Firebase credentials set | |
| `prices` | `Prices:WorkerUrl` set, last successful source run | No success yet, or none in 48 hours |
| `storage` | `Storage:ServiceUrl` set | |

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

## Runtime settings

`/admin/settings` changes some configuration without a redeploy. `RuntimeSettingDefinitions` (Domain) lists every key that can move, with its type and rules; the API, the page, the MCP tools and the overlay all read that one catalog. A key is the configuration path with dots: `Smtp.Host` overrides `Smtp:Host` (`Smtp__Host`).

| Section | Keys | Environment they override |
| --- | --- | --- |
| Email | `Smtp.Host`, `Port`, `Security`, `Username`, `Password`, `FromAddress`, `FromName`, `LocalDomain` | `SMTP_*`, `Smtp__*` |
| AI provider | `Ai.BaseUrl`, `ApiKey`, `Model`, `MaxOutputTokens`, `TimeoutSeconds`, `SupportsTemperature`, `ReasoningEffort`, prompt and completion prices | `AI_*`, `Ai__*` |
| AI limits | `Ai.GlobalDailyTokens`, `Ai.GlobalDailyCostMicros`, and `DailyRequests` and `DailyTokens` for `Free`, `Pro`, `PlanningFree`, `PlanningPro`, `Onboarding`, `Trainer`, `Eval` | `AI_GLOBAL_*`, `Ai__*` |
| Telegram | `Telegram.BotServiceUrl` | `TELEGRAM_BOT_SERVICE_URL` |
| Push | `Push.FcmProjectId`, `Push.FcmServiceAccountJson` | `FCM_*`, `Push__*` |
| Billing | `Paddle.Environment`, `ApiKey`, `WebhookSecret`, `GraceDays`, `LifetimePriceId` | `PADDLE_*`, `Paddle__*` |

**Precedence.** A value stored in `system_settings` wins; without one, configuration applies, and without that, the built-in default. `GET /api/admin/settings` reports each key's value and its source (`database`, `configuration` or `unset`). `PUT /api/admin/settings/{key}` stores a value after the catalog checks it (port range, URL or host shape, enum, number range) and answers 400 `validation_failed` with the catalog's message otherwise. `DELETE` removes the stored value, so configuration applies again. Each set and clear is audited with the key as the entity id; a secret's value is stored in the audit row as `[redacted]`.

**Use time.** Consumers read `IRuntimeOptions<T>` on every call: the SMTP sender, the AI provider, `IAiQuotaService` (ceilings and tier limits), the eval runner, the Telegram notifier, the push sender, the Paddle client, entitlements (grace days) and webhook verification. Each instance keeps the table in memory; a write drops that copy and publishes on the Redis channel `mizan:runtime-settings:changed`, so the other instances drop theirs within moments. The copy also expires after 30 seconds while subscribed, and after one second when Redis is absent.

**Secrets.** `Smtp.Password`, `Ai.ApiKey`, `Push.FcmServiceAccountJson`, `Paddle.ApiKey` and `Paddle.WebhookSecret` are encrypted with ASP.NET Core Data Protection before they are stored. The API returns only `hasValue` for them, and `IRuntimeSettings` refuses to hand one to Application code as text; only the options overlay in Infrastructure decrypts them. The key ring lives in the `data_protection_keys` table, so it survives restarts and every instance shares it. A database backup therefore carries the keys with the secrets: the encryption keeps secrets out of API responses, logs, the audit log and casual queries, not away from someone holding the whole database. To keep the ring elsewhere, set `DataProtection__KeysPath` (`DATA_PROTECTION_KEYS_PATH` in production Compose) to a directory that every API instance mounts. Losing the ring makes stored secrets unreadable; each then falls back to configuration until it is entered again.

**Stays in configuration.** Anything needed before the database is reachable, built once at startup, or shared with another service: connection strings, `App__PublicUrl`, cookie and CORS settings, OAuth issuer and clients, external sign-in providers, `Storage__*` (the S3 client is built once), the MCP service keys (shared with the MCP and Telegram services), `Telegram__BotUsername` (must match the bot service) and the bot token (the API never holds it), the prices worker, the outbox, rate limits and OpenAPI exposure.

**Tests.** **Send test email** (`POST /api/admin/settings/smtp/test`, MCP `admin_test_smtp`) queues one message to the signed-in administrator through the outbox with the settings as they stand and returns the job id; **Jobs** shows whether it was delivered. **Test provider** (`POST /api/admin/settings/ai/test`) makes one small call on the evaluation line, reserved and settled through `IAiQuotaService` like any other, and reports `reachable` with the model and latency, or an error class (`not_configured`, `quota_exceeded`, `timeout`, `unreachable`, `http_401` and so on) without the provider's body.

MCP has `admin_list_settings`, `admin_set_setting`, `admin_clear_setting` and `admin_test_smtp`. Telegram has no admin surface; the bot serves end users.
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
