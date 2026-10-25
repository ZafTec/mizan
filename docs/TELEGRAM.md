# Telegram bot

`Mizan.Telegram` lets linked users log meals, check totals, and record weight. It calls the main API through `MizanApiClient` with internal service authentication (`X-Api-Key` and `X-Impersonate-User`); it has no direct database access or separate AI provider.

## Link an account

1. Open `/profile/settings/telegram` in the app and request a code.
2. Follow the generated `t.me/<bot>?start=<code>` link. Codes are single-use and expire after five minutes.
3. Telegram sends `/start <code>`; the bot resolves it through `/api/Telegram/resolve` and links the Telegram user to the signed-in Mizan account.

Unlinked chats receive a settings-page link. `/unlink` removes the connection without deleting logged data.

The bot is a Pro feature. Issuing a link code answers 402 for a Free account. A linked account that returns to Free keeps its link: the bot replies with the upgrade page and still accepts `/unlink` and `/help`, and it works again as soon as the account is Pro. `/api/Telegram/resolve` reports `isPro` for this.

## Commands

| Input | Behavior |
| --- | --- |
| `/today` | Daily totals against the user's targets |
| `/weight 82.4` | Records a weigh-in |
| `/plan`, `/plan tomorrow`, `/plan 2026-10-23` | The active plan's day: entries per slot with ✓ on logged ones, totals against the plan's rules, violations, the eating window. One **Log** button per unlogged recipe or food entry, a **Tomorrow** button, and **Open** to `{MiniAppUrl}/plan?planId=...&day=...`. No active plan points at `/meal-plan`. |
| `/shop` | The newest shopping list: up to ten unticked items grouped by prep session, with amounts and a "needs review" marker, "N left of M" at the end. One toggle button per item, **More** and **Back** to page, and **Open** to `{MiniAppUrl}/shopping-list?listId=...`. No list points at `/meal-plan`. |
| `/planme high protein, cheap` | Drafts a week from tomorrow with the assistant and replies with its summary, weekly cost and violations, and **Confirm** / **Discard** |
| `/unlink` | Disconnects this chat |
| `/help` | Lists commands |
| A photo | Proposes nutrition with **Log it**, **Split**, and **Discard** actions |
| Other text | Continues the shared AI chat used on the website |

The plan and shopping cards carry one id per button, because two guids do not fit in 64 bytes: **Log** is `pe:{entryId}`, **Tomorrow** is `pd:{yyyy-MM-dd}`, a shopping toggle is `sh:{itemId}`, and **More** or **Back** is `sp:{page}`. The bot reads the rest back from the API when the button is pressed. A **Log** press finds its plan by reading today's and tomorrow's day (`GET /api/MealPlans/day`), the days a card is sent for, then calls `POST /api/MealPlans/{planId}/entries/{entryId}/log` with an empty body and renders the day again; an entry it cannot find is answered with "That card is too old to use". A toggle re-resolves the newest list, notes which page the item sat on, calls `PATCH /api/ShoppingLists/items/{itemId}/toggle` with `{ "isChecked": true }`, reads the list again and renders that page. Without a date, `GET /api/MealPlans/day` dates the day in the user's time zone; `/plan tomorrow` adds a day to that date, and only when no plan covers today does the bot fall back to UTC, because neither the resolve call nor `/api/Users/me` exposes the time zone to a service caller.

**Log it** logs the combined nutrition totals. **Split** asks how many people share the meal (2 to 5) and sends the whole totals with `sharedBetween` on `POST /api/Meals`; the API divides them (calories to whole numbers, grams to a tenth) and names the entry "(1/N share)", so the bot computes nothing. Each button carries the meal's totals, because the bot keeps no state, so a button works whenever it is pressed. The meal name is cut by UTF-8 bytes to keep every button under Telegram's 64-byte limit. The card has no inline quantity editor. Shared API consent, entitlements, validation, and AI quotas apply.

## Mini App

The Mini App is the web app opened inside Telegram's WebView: Next.js serves `/tg/plan` and `/tg/shopping-list` without the shell, and `web_app` buttons in the chat open them (`/help` carries an **Open Mizan** button, and the `/plan` and `/shop` cards end with an **Open** button to the day or list they show, each in a row of its own). Telegram hands the page a signed `initData` string naming the Telegram user and the moment it was opened. The page turns it into an ordinary Mizan session in one call, and the bot token never leaves the bot service:

1. The page sends `POST /telegram/miniapp/session` to the bot service with `{ "initData": window.Telegram.WebApp.initData }`. The route lives on the app's public host next to `/telegram/webhook`.
2. The bot verifies the signature (`TelegramInitData`): the data-check string is every field but `hash`, sorted by key and joined with newlines; the key is HMAC-SHA256(`WebAppData`, bot token); the hash comparison is constant-time. It refuses an `auth_date` older than five minutes (`TelegramBot__MiniAppMaxAge`) or more than a minute in the future, a repeated field, and a `user` field it cannot read.
3. The bot calls `POST /api/Telegram/sessions` with its service key and `{ telegramUserId, ipAddress, userAgent }`. The API finds the linked account, applies the sign-in checks a password login makes (locked, banned, unverified), applies the same Pro gate as the rest of the bot, creates an ordinary `user_sessions` row with a 24-hour sliding life (`Telegram__MiniAppSessionLifetime`), audits the command, and returns `{ token, expiresAt, userId }`.
4. The bot answers `200 { token, expiresAt }` and sets `mizan_session=<token>; Path=/; HttpOnly; Secure; SameSite=Lax; Expires=<expiresAt>` (no `Secure` in Development), the same attributes as the API's own sign-in cookie. The page is signed in at once; `GET /api/Auth/me` and every other call carry the cookie.

In the web app (`frontend/app/tg`), the layout drops the navigation and footer, loads `telegram-web-app.js` with `next/script`, calls `WebApp.ready()` and `expand()`, and maps Telegram's `--tg-theme-*` colours onto the app's tokens where Telegram sends them. `useMiniAppSession` (`lib/tg`) posts `initData` once per opening and resolves to `signed_in` or a refusal; an empty `initData` (a page opened outside Telegram) is `not_in_telegram` and calls nothing. Only then do the pages fetch with `clientApi` and render `PlanDetail` (with **Log**) or `ShoppingListView` (with ticks). Without `planId`, `/tg/plan` reads `GET /api/MealPlans/day`; without `listId`, `/tg/shopping-list` opens the newest list. A refusal shows one sentence and one action: `not_linked` opens `linkUrl` and `upgrade_required` opens `upgradeUrl` with `WebApp.openLink`; the others show the bot's message. `proxy.ts` never sends `/tg` or `/telegram` to `/login`, and `/tg` pages answer with `frame-ancestors 'self' https://web.telegram.org` instead of `X-Frame-Options: DENY`, because Telegram Web shows a Mini App in an iframe.

The route handler `app/telegram/miniapp/session/route.ts` forwards the body to `${TELEGRAM_BOT_URL}/telegram/miniapp/session` (server-side; Compose defaults it to `http://telegram:5002`) and relays the status, the JSON body, and `Set-Cookie` unchanged. It never logs `initData`. Where the reverse proxy routes the path to the bot, as in production, the handler is not reached.

Refusals from the bot endpoint: `400 bad_request` (no `{ initData }` body), `401 invalid_init_data` (bad signature, stale, malformed), `404 not_linked` with `linkUrl` (the Telegram settings page), `402 upgrade_required` with `upgradeUrl`, `403 forbidden` or `429 account_locked` with the API's reason, and `502 api_unavailable`. The API endpoint itself answers `404 not_found`, `402 upgrade_required`, `403 forbidden` (banned) or `email_not_verified`, and `429 account_locked`. The frontend must call the bot endpoint on open, read the status, and show the link page on `not_linked`; it never sees or handles the bot token, and `Authorization: Bearer` is not a way to present a user session.

## Evening reminder

A person with an active plan whose rules set `EatingWindowEnd` gets one reminder per plan day, 30 minutes before the window closes in their time zone: "Your window closes at 20:00. Today: 1,640 of 1,800 kcal." Today's calories are the diary's total for that plan day; the target is the plan's `TargetCalories`, and without one the line ends at "Today: 1,640 kcal." A household plan reminds every member who follows it that day, each in their own time zone and with their own diary. A member follows the plan `GET /api/MealPlans/day` shows them: their own personal plan comes first, so a member whose own plan covers the day gets only that plan's reminder, or none when it has no window. It arrives as an in-app notification (type `evening_reminder`, linking to `/meal-plan/{planId}`), as a push where a phone is registered, and in Telegram when the account is linked and Pro, with an **Open** button to the plan day.

- `EveningReminderScheduler` scans every 15 minutes and queues the next reminder of each person who follows a plan (its owner and each household member) that falls within two hours as an `evening-reminder` outbox job, keyed `evening-reminder:{userId}:{day}:{HHmm}`. A repeat scan finds the same key and adds nothing; a moved window gets a new key.
- When the job runs, `EveningReminders` checks it again with `EveningReminderSchedule` (Application): the person still follows an active plan with a window on that day (the reminder names the plan they follow when it is sent), the window has not closed, the job is still the next reminder, and no reminder went out on that local calendar day. The last one sent is read from the notifications table. A window ending before 00:30 is reminded about late the evening before. The text is `EveningReminderText` (Domain).
- The API never holds the bot token. A `telegram-message` outbox job calls the bot service's `POST /internal/notify` with the service key in `X-Api-Key` and `{ telegramUserId, text, openPath }` (`TelegramNotifyRequest`). The bot escapes the text, adds the **Open** button when the Mini App is HTTPS, and answers 204, 401 for a wrong key, 400 for a bad body, or 502 when Telegram did not take it, which the outbox retries.

## Configure and run

Set `TELEGRAM_BOT_TOKEN`, `TELEGRAM_BOT_USERNAME`, `MCP_SERVICE_KEY`, and `PUBLIC_APP_URL` in the Compose environment. Keep the bot username consistent with the main API's `Telegram:BotUsername`, which builds account-link URLs. `TELEGRAM_MINIAPP_URL` (`TelegramBot__MiniAppUrl`) is optional and defaults to `{PUBLIC_APP_URL}/tg`; Telegram accepts `web_app` buttons over HTTPS only, so the bot leaves every `web_app` button out when the URL is plain HTTP, as in local development.

The API reaches the bot at `Telegram__BotServiceUrl` (`TELEGRAM_BOT_SERVICE_URL`) for the evening reminder; empty, the reminder goes out in the app only. Compose defaults it to `http://telegram:5002` in development; in production set it to `http://mizan-telegram:5002`. Do not route `/internal/notify` through the reverse proxy.

The production Compose file enables the optional bot with `COMPOSE_PROFILES=telegram`. It uses `Mcp__ServiceApiKey` for internal API authentication and `App__PublicUrl` (falling back to `PUBLIC_APP_URL`) for public links. The backend receives only the bot username through its Telegram configuration; the bot service receives the token and webhook secret. The image build workflow includes Telegram, and a manual run builds only that image from the default branch.

`TELEGRAM_USE_WEBHOOK=false` selects local long polling without a public hostname or TLS. Startup removes any previous webhook before polling. Commands are registered automatically in either mode.

For production webhooks:

- Set `TELEGRAM_USE_WEBHOOK=true` and a random `TELEGRAM_WEBHOOK_SECRET` (for example, `openssl rand -hex 32`).
- Make `PUBLIC_APP_URL` publicly reachable over HTTPS.
- Route `/telegram/webhook` and `/telegram/miniapp/session` to the Telegram container on the app's public host. See [request boundaries](ARCHITECTURE.md#request-boundaries).

Startup requests registration of `{PublicUrl}/telegram/webhook` and disables polling. A missing webhook secret prevents registration. Verify Telegram's `getWebhookInfo` after setup; the service health check alone does not prove successful registration. Requests must present the matching `X-Telegram-Bot-Api-Secret-Token`; comparison is constant-time and invalid requests receive 404. Updates are acknowledged immediately and processed in the background. Redeployment requests webhook registration again.

`GET /health` on the service returns `{ status, configured, mode }`, with mode `webhook` or `long-poll`.

## Conversation and photos

Chat replays the last ten turns and a rolling summary stored on the shared thread. Summary refresh happens after a saved reply; a failed refresh preserves the previous summary without failing the turn. The reply ends with one line per performed action (`✓` or `✗` with the error), so a request to create a list shows that it happened.

The bot reads and writes the records in `Mizan.Contracts` (`LogMealRequest`, `DailyNutritionTotals`, `FoodAnalysisResult`, `AiChatTurnDto`) and formats `/today` with `Mizan.Domain.Nutrition.NutritionText`, the same wording the assistant's tools and context use.

`/planme` sends the words as the preference to `POST /api/MealPlans/generate` and spends the planning allowance. **Confirm** (`pm:ok:{planId}`) accepts the draft and **Discard** (`pm:no:{planId}`) declines it; the handler is `Bot/PlanMeHandler.cs`.

Photo analysis uses `/api/Nutrition/ai/analyze-image` and stores the image under `meals/` in object storage. A storage failure loses the retained picture but does not discard the analysis. See [AI](AI.md) for consent and metering.

## Verify

Backend integration tests cover account linking, the Mini App session endpoint, the evening reminder through the outbox, service authentication, and shared API behavior. `TelegramInitData` has unit tests over a fixture signed with a known fake token, and the bot's Mini App endpoint is tested through the fake HTTP world. Verify a configured bot separately in its selected mode, including linking, a read, a confirmed write, and opening the Mini App from the `/help` button. Browser fixture tests do not contact Telegram: `e2e/tg.spec.ts` injects a fake `window.Telegram.WebApp`, answers the script URL with nothing, and `e2e/fixture-tg.ts` plays the bot endpoint with one outcome per opening.
