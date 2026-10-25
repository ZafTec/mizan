# Administration

The admin area (`/admin`, administrators only) runs the service: users, content, jobs, prices, billing, and the settings below. Its API lives under `/api/admin` and its MCP tools are named `admin_*` and need the `admin` scope.

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
