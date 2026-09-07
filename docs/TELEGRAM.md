# Telegram bot

`Mizan.Telegram` lets linked users log meals, check totals, and record weight. It calls the main API through `MizanApiClient` with internal service authentication (`X-Api-Key` and `X-Impersonate-User`); it has no direct database access or separate AI provider.

## Link an account

1. Open `/profile/settings/telegram` in the app and request a code.
2. Follow the generated `t.me/<bot>?start=<code>` link. Codes are single-use and expire after five minutes.
3. Telegram sends `/start <code>`; the bot resolves it through `/api/Telegram/resolve` and links the Telegram user to the signed-in Mizan account.

Unlinked chats receive a settings-page link. `/unlink` removes the connection without deleting logged data.

## Commands

| Input | Behavior |
| --- | --- |
| `/today` | Daily totals against the user's targets |
| `/weight 82.4` | Records a weigh-in |
| `/unlink` | Disconnects this chat |
| `/help` | Lists commands |
| A photo | Proposes nutrition with **Log it** and **Discard** actions |
| Other text | Continues the shared AI chat used on the website |

Photo confirmation logs the combined nutrition totals; the Telegram card has no inline quantity editor. Shared API consent, entitlements, validation, and AI quotas apply.

## Configure and run

Set `TELEGRAM_BOT_TOKEN`, `TELEGRAM_BOT_USERNAME`, `MCP_SERVICE_KEY`, and `PUBLIC_APP_URL` in the Compose environment. Keep the bot username consistent with the main API's `Telegram:BotUsername`, which builds account-link URLs.

`TELEGRAM_USE_WEBHOOK=false` selects local long polling without a public hostname or TLS. Startup removes any previous webhook before polling. Commands are registered automatically in either mode.

For production webhooks:

- Set `TELEGRAM_USE_WEBHOOK=true` and a random `TELEGRAM_WEBHOOK_SECRET` (for example, `openssl rand -hex 32`).
- Make `PUBLIC_APP_URL` publicly reachable over HTTPS.
- Route `/telegram/webhook` to the Telegram container on the app's public host. See [request boundaries](ARCHITECTURE.md#request-boundaries).

Startup registers `{PublicUrl}/telegram/webhook` and disables polling. A missing webhook secret prevents registration. Requests must present the matching `X-Telegram-Bot-Api-Secret-Token`; comparison is constant-time and invalid requests receive 404. Updates are acknowledged immediately and processed in the background. Redeployment re-registers the webhook; no manual Telegram API call is needed.

`GET /health` on the service returns `{ status, configured, mode }`, with mode `webhook` or `long-poll`.

## Conversation and photos

Chat replays the last ten turns and a rolling summary stored on the shared thread. Summary refresh happens after a saved reply; a failed refresh preserves the previous summary without failing the turn.

Photo analysis uses `/api/Nutrition/ai/analyze-image` and stores the image under `meals/` in object storage. A storage failure loses the retained picture but does not discard the analysis. See [AI](AI.md) for consent and metering.

## Verify

Backend integration tests cover account linking, service authentication, and shared API behavior. Verify a configured bot separately in its selected mode, including linking, a read, and a confirmed write. Browser fixture tests do not contact Telegram.
