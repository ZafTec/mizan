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
