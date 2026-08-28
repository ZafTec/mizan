# Telegram bot

`Mizan.Telegram` is a separate ASP.NET Core service (its own container,
`mizan-telegram`) that lets a user log meals, check totals, and log a
weigh-in from Telegram instead of the app. It never talks to the database
directly - every action goes through `MizanApiClient`, which calls the main
API over the internal Docker network using the same service-key auth the MCP
server uses (`X-Api-Key` + `X-Impersonate-User`, resolved to the linked
user's id).

## Linking flow

1. User opens `/profile/settings/telegram` in the app and requests a code.
2. Backend mints a single-use code, five-minute TTL (`TelegramLinkCommands`,
   `IssueTelegramLinkCodeCommand`), and returns a `t.me/<bot>?start=<code>`
   deep link built from `Telegram:BotUsername`.
3. User opens that link in Telegram, which sends `/start <code>` to the bot.
4. The bot calls `POST /api/Telegram/resolve` (service-authenticated) with
   the code and the caller's Telegram user id. The backend consumes the code
   and creates the `TelegramLink` row (`UserId` ↔ `ChatId`).
5. Bot replies with a greeting and the command list.

An unlinked chat that sends anything other than `/start <code>` gets a
"sign in first" reply with the settings-page URL. `/unlink` clears the link
(`DELETE /api/Telegram/resolve/{telegramUserId}`) without touching any
logged data - reconnecting later just creates a new link to the same
history.

## Bot commands

Registered with Telegram via `setMyCommands` on every startup
(`TelegramClient.SetCommandsAsync`), regardless of poll or webhook mode:

| Command | Behavior |
|---|---|
| `/today` | Daily totals against the user's targets |
| `/weight 82.4` | Logs a weigh-in via `LogBodyMeasurementCommand` |
| `/unlink` | Disconnects this chat; log is untouched |
| `/help` | Lists the above |
| *(a photo)* | Runs `api/Nutrition/ai/analyze-image`; replies with a proposal card the user confirms or edits - never a silent write |
| *(anything else)* | Falls through to the shared AI chat thread (`api/Ai/chat`, `api/Ai/threads`) - the same conversation as the website's AI coach, not a separate assistant |

## Deployment: webhook in production, long polling in development

One config switch, `TelegramBot:UseWebhook`
(`TELEGRAM_USE_WEBHOOK` env var), picks the mode. Both paths hand the same
`Update` to the same `UpdateHandler` - nothing downstream of that call cares
which one delivered it.

**Long polling** (`UseWebhook=false`, the local default): `LongPollWorker`
calls `getUpdates` in a loop. No public hostname or TLS needed, which is the
whole point for local dev.

**Webhook** (`UseWebhook=true`, production): on startup, the service calls
Telegram's `setWebhook` with `{PublicUrl}/telegram/webhook` and the
configured secret, then registers commands. Telegram POSTs updates to that
path; the handler checks the `X-Telegram-Bot-Api-Secret-Token` header
against `TelegramBot:WebhookSecret` with a constant-time comparison and
returns 404 to anything that doesn't match - the webhook path is the only
public surface this service has, so that check is its entire front door.
The request is acknowledged immediately and the update is handled on a
background task, since Telegram retries anything slower than a few seconds
and an AI turn is often slower than that.

`LongPollWorker` checks `UseWebhook` itself and no-ops when it's set -
running both modes at once isn't just redundant, Telegram's API rejects
`getUpdates` with a 409 while a webhook is registered. Startup handles the
reverse direction too: switching `UseWebhook` back to `false` calls
`deleteWebhook` automatically, so flipping the flag never needs a manual
Telegram API call to un-stick long polling. Enabling webhook mode with no
`WebhookSecret` set is refused outright (logged as an error, no webhook
registered) rather than silently pointing Telegram at an endpoint that
would 404 every call.

**Required for webhook mode in production:**
- `TELEGRAM_USE_WEBHOOK=true`
- `TELEGRAM_WEBHOOK_SECRET` - `openssl rand -hex 32`, matches nothing else
- `PUBLIC_APP_URL` reachable from the public internet over HTTPS, with the
  reverse proxy routing `/telegram/webhook` to the `mizan-telegram`
  container (path-routed on the main domain - see the "Network Topology"
  section of `CLAUDE.md`, no separate subdomain)
- `TELEGRAM_BOT_TOKEN`, `TELEGRAM_BOT_USERNAME`, `MCP_SERVICE_KEY` - same as
  long-poll mode

No manual `setWebhook` curl call is needed; deploying with those variables
set is sufficient; a redeploy re-registers the same webhook idempotently.

## Health check

`GET /health` on the service reports `{ status, configured, mode }`, where
`mode` is `"webhook"` or `"long-poll"` reflecting the resolved
`UseWebhook` setting - useful for confirming which mode actually started
without reading logs.

## Conversation memory

Free-text messages go to `api/Ai/chat`, the same endpoint and the same thread
the website uses, so everything below is shared rather than a Telegram-specific
implementation.

**Today: a rolling summary.** Only the last ten turns are replayed to the model
verbatim. Anything older is folded into a running summary stored on the thread
(`ai_chat_threads.summary`), refreshed after a turn once messages fall out of
that window. Without it a long conversation forgets its own beginning - someone
says they are vegetarian early on and gets offered chicken twenty turns later.
The refresh runs after the reply is saved and never fails the turn: a summary
that could not be rewritten leaves the previous one in place.

**Planned: retrieval instead.** A summary is lossy by construction, and the
loss is worst exactly where Telegram is used most - months of short, factual
messages, where any single detail matters but none of them is important enough
to survive summarising. The intended replacement is retrieval over the message
history: embed each turn, and select the handful genuinely relevant to the
current question rather than compressing everything that came before.

That needs three things this codebase does not have yet: an embedding model
behind `IAiProvider`, vector storage (pgvector on the existing PostgreSQL, not
a second datastore), and a retrieval step in the chat handler that replaces the
summary block. It is deliberately not built yet - the rolling summary is enough
while conversations are short, and retrieval is worth doing once rather than
twice.

## Photos are kept

A photo sent to the bot is analysed by `api/Nutrition/ai/analyze-image` and
stored in object storage under `meals/`, the same path the website's food-photo
upload uses. The analysis is a guess at a portion size; keeping the picture is
what lets someone check that guess later. Storage failing costs the picture,
never the answer.
