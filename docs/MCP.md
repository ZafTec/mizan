# MCP interface

The MCP service exposes the same logs, recipes, consent, and account capabilities as the web app through HTTP at `/mcp`. The API remains the authority for ownership, roles, nutrition calculations, consent, and entitlements.

It speaks the stateless MCP revision (2026-07-28): there is no session to pin a client to one instance, and every request carries what the server needs. Clients that still send `initialize` are served the same way.

## Connect with OAuth

Clients connect through OAuth. There are no personal tokens.

1. Add `https://<your-app-host>/mcp` as a remote MCP server in the client. Local Compose uses `http://localhost:5001/mcp`.
2. The server answers `401` with a `WWW-Authenticate` header naming `/mcp/.well-known/oauth-protected-resource`. The client follows it to the authorization server at `/api`.
3. The client registers itself (RFC 7591), or presents a client ID that is an HTTPS URL to a metadata document. Metadata fetches refuse private addresses, redirects, non-443 ports, and large or slow answers.
4. Mizan shows a consent page at `/oauth/consent`. Sign in, then choose what the app may do:
   - **Areas**: profile, nutrition, training, body, recipes, planning, households, social, notifications, trainer, and AI use. Each is read or write; write includes read.
   - **Households**: none, selected ones, or all.
   - `admin` is offered to administrators only and is off by default.
5. The client receives a one-hour access token (`mza_`) and a rotating refresh token (`mzr_`, 30 days). PKCE S256 is required. Reusing a refresh token revokes the whole family.

Manage connections under **More → Connected apps** (`/profile/mcp`): last use, 30-day usage per app and per tool, **Edit access**, and **Disconnect**. Editing may only reduce access; raising it needs a new consent. A reduction or disconnect applies on the app's next request.

Tokens are opaque and issued for one audience (RFC 8707). A token for `/mcp` is refused by the API, and the API-audience token the Android app holds is refused by `/mcp`. `X-Api-Key`, `X-Impersonate-User`, and `X-Mcp-Grant` are internal service headers. The API loads the grant from its database and ignores any scopes in the header.

### Discovery

| Document | Address |
| --- | --- |
| Protected resource (RFC 9728) | `/mcp/.well-known/oauth-protected-resource` (named in the 401), `/.well-known/oauth-protected-resource/mcp`, `/.well-known/oauth-protected-resource` |
| Authorization server (RFC 8414) | `/.well-known/oauth-authorization-server/api`, `/.well-known/openid-configuration/api`, `/api/.well-known/oauth-authorization-server`, `/api/.well-known/openid-configuration` |

Each row answers one document at every address. The MCP service serves the first row and the API the second, so a client that derives the address from the resource or issuer URL, as the RFCs say, finds it. Paths under `/mcp` and `/api` reach their service already; the root `/.well-known` paths need their own proxy routes, ahead of the frontend's `location /`:

```nginx
location ~ ^/\.well-known/oauth-protected-resource(/mcp)?$ {
    proxy_pass http://$mizan_mcp$uri;
}
location ~ ^/\.well-known/(oauth-authorization-server|openid-configuration)/api$ {
    proxy_pass http://$mizan_backend;
}
```

## What a connection can see and call

The MCP layer holds each tool to a scope (`McpToolScopes`; a test fails if a tool has none). A client sees only the tools, resources, and prompts its grant covers, and a direct call outside it is refused naming the missing permission. The API enforces household and data-axis limits itself, so a bug in the MCP layer cannot widen a user's choice.

Personal read endpoints are gated by scope only. Household-owned records are filtered by membership and by the grant's households.

The free plan allows 15 successful tool calls and resource reads per month. Prompts, skills, and app pages do not count; failures do not count.

## Tools, resources, and prompts

Use `tools/list` for argument schemas. The source catalogue is [`backend/Mizan.Mcp.Server/Tools`](../backend/Mizan.Mcp.Server/Tools). Every tool has a title and is marked closed-world.

Planning tools follow [PLANNING](PLANNING.md). `planning:read` covers `list_meal_plans`, `get_meal_plan`, `get_plan_day` (plain text, not an error, when no active plan covers the date), `get_plan_targets` (the effective targets a macro mode gives), `list_templates`, `list_shopping_lists`, `get_shopping_list`. `planning:write` adds the plan tools (`create_meal_plan`, `update_meal_plan`, `delete_meal_plan`, `set_meal_plan_status`, `accept_meal_plan`, `decline_meal_plan`, `apply_template`), the entry tools (`add_plan_entry`, `update_plan_entry`, `remove_plan_entry`, `log_plan_entry`; they take the entry's own id), the prep session tools, `generate_shopping_list`, and the shopping list CRUD (`create_shopping_list`, `update_shopping_list`, `delete_shopping_list`, `add_shopping_list_item`, `update_shopping_list_item`, `delete_shopping_list_item`, `toggle_shopping_list_item`, `clear_checked_items`). `create_plan_for_client` and `apply_template_for_client` (a client's draft from a template) need `trainer:write`, as do `create_template`, `update_template` and `delete_template`, which keep a coach's own templates; `admin_create_template`, `admin_update_template` and `admin_delete_template` manage global templates under `admin`. All of them call the same routes as the web. JSON arguments (`rulesJson`, `entriesJson`, `prepSessionsJson`) carry the records from `Mizan.Contracts/Planning`, as `log_day` does for meals.

Notification tools: `notifications:read` covers `list_notifications`, `get_unread_notification_count` and `get_notification_preferences` (each kind's channels, quiet hours, and whether Telegram is linked); `notifications:write` adds `mark_notification_read`, `mark_all_notifications_read` and `update_notification_preferences`, which changes only the kinds and channels it names and sets or clears quiet hours ([notifications](ARCHITECTURE.md#notifications)).

Notification campaign tools ([admin](ADMIN.md#notification-campaigns)) need `admin`: `admin_list_notification_campaigns`, `admin_get_notification_campaign`, `admin_preview_audience`, `admin_create_notification_campaign`, `admin_send_test_notification`, `admin_activate_notification_campaign` (takes the confirmed audience count; a changed audience is refused), `admin_pause_campaign`, `admin_resume_campaign`, `admin_cancel_campaign`. They call `/api/admin/notification-campaigns`.

Reminder tools ([reminders](ARCHITECTURE.md#reminders)) use the same scopes: `list_reminders` needs `notifications:read`; `create_reminder`, `update_reminder` and `delete_reminder` need `notifications:write`. They call `/api/Reminders`, which holds the kinds, the hourly floor and the plan's cap.

**Resources**: `mizan://profile`, `mizan://goals/current`, `mizan://nutrition/today`, `mizan://streak`, `mizan://meal-plans/today`, and templates for `diary/{date}`, `recipes/{id}`, `workouts/{id}`, `meal-plans/{id}`, `shopping-lists/{id}`. Each needs the scope of the matching tool.

**Prompts**: `log_my_day`, `plan_my_week`, `grocery_run`, `weekly_review`, `workout_planner`, `macro_rescue`, `recipe_from_leftovers`, `trainer_checkin`. `plan_my_week` builds the plan with `create_meal_plan` and `add_plan_entry`, passing the goal and stated limits as rules, and reads the violations back from `get_meal_plan`; `grocery_run` calls `generate_shopping_list` and reads the result with `get_shopping_list`. Neither asks the model to add up nutrition or amounts.

## Extensions

**Skills** (`io.modelcontextprotocol/skills`): `skills/list` and `skills/get` serve six skills, and their files are readable as `skill://` resources. The source is `backend/Mizan.Mcp.Server/Skills`, embedded in the server. The Claude plugin carries the same files; see [Plugin](#claude-plugin).

**Apps**: four small views render inside a host that supports MCP Apps, each a single self-contained HTML page served as `ui://mizan/<name>.html` (the host blocks network requests, so a page carries everything). The pages share the host bridge `Apps/_bridge.js`, inlined at each page's `<!-- mizan:bridge -->` marker when it is served. A page reads through tools via its host and holds no credential. A tool behind an app answers with `structuredContent` next to its JSON text (`WidgetResults`; a JSON array arrives as `{ "items": [...] }`), and a tool without a host that shows apps still returns its text. Hosts add text blocks of their own to a result, so the bridge's `dataOf` reads `structuredContent` first, then the first text block that parses as a JSON object or array, never the joined text; `textOf` is the first text block. A page shows its error state only for `isError` and its empty state for a result with no data. `frontend/lib/mcp-app-bridge.test.ts` holds the parser to a table of results.

| Tool | App | Needs |
| --- | --- | --- |
| `get_nutrition_summary` | `nutrition-day` | `nutrition:read` |
| `analyze_food_image` | `food-photo` | `ai:use` |
| `list_body_measurements` | `body-trend` | `body:read` |
| `get_meal_plan`, `get_plan_day` | `meal-plan` | `planning:read` |

The photo review logs ticked items one at a time and stops at the first failure, so a retry never logs anything twice. The meal plan page shows a plan's days (or one day) with entries, nutrition, totals, prep sessions and violations; its Log button calls `log_plan_entry` through the host and disables itself once the entry is logged, and its Shopping list button calls `generate_shopping_list`. Those writes need `planning:write` on the connection; the page itself needs only `planning:read`.

**Tasks**: `ask_ai`, `suggest_meals`, `analyze_food_image`, `generate_meal_plan`, and `export_profile` may run as tasks for clients that declare `io.modelcontextprotocol/tasks`. The call returns a task id at once; the client polls `tasks/get`. State lives in the API's `mcp_tasks` table, so any instance can answer a poll and a restart does not lose it. A task is visible only to the user who started it, ends once, expires after an hour, and a task nobody has touched for 15 minutes is reported as interrupted. A call the grant does not cover, or one over the monthly cap, is refused immediately rather than becoming a task. The task's tool runs in the background as the same user under the same grant, and usage is settled on success and failure.

Apps and Tasks packages are marked experimental by the SDK and may change.

## Logging examples

Search before logging catalogue food so its real nutrition is used:

```text
search_foods(search: "Greek yogurt")
log_food(foodId: <returned id>, date: "2026-09-07", mealType: "BREAKFAST", servings: 1.5)
get_food_diary(date: "2026-09-07")
```

`servings` is a count of the food's declared serving size, not grams. Read the search result and convert the amount eaten before sending it. `log_meal` takes a recipe ID and recipe servings. Use `log_meal_manual` only when entering known nutrition without a catalogue item.

`log_day` accepts one date, optional `mealsJson`, optional `exercisesJson`, and optional `weightKg`. The JSON arguments contain arrays; workout exercises contain individual sets. It uses the existing write endpoints and returns a receipt identifying completed, failed, and remaining items. It is not an atomic database transaction. Retry only remaining entries; inspect an unconfirmed write before retrying it.

To reuse a logged meal:

```text
promote_to_recipe(date: "2026-09-07", mealType: "BREAKFAST", title: "Morning bowl")
```

The selected meal must contain at least two items. If the API requests missing weights, ask the user and supply `recipeYieldsJson` or `entryWeightsGramsJson`; do not infer a batch yield. `promote_recipe_to_preparation` turns an owned recipe into a reusable ingredient using its finished batch weight.

`generate_meal_plan` (scope `planning:write`) drafts a plan through `POST /api/MealPlans/generate`: one draft per budget in `budgetCeilings`, each saved as Draft with its violations and cost table; `dailyBudget` caps each day's cost in every draft. It spends the planning allowance, one plan a day on Free. Read drafts with `list_meal_plans(status: "Draft")` and `get_meal_plan`; the person accepts one in the app.

AI tools consume the caller's applicable AI quota and enforce consent. Trainer AI calls cannot access axes the client withheld. See [AI](AI.md).

### Images

`upload_image` and `analyze_food_image` take exactly one of `imageUrl` or `imageBase64`, capped at 4 MB either way. A client model that has a photo cannot hand over the attachment's bytes; it has to write base64 out as output tokens, which is slow, breaks on one wrong character, and tempts it to shrink the photo until the estimate is useless. So the tools prefer `imageUrl`, and their descriptions tell the model never to shrink or re-encode the photo.

With `imageUrl`, the MCP service passes the address to the API (`POST /api/Nutrition/ai/analyze-image-url`, `POST /api/Uploads/image-url`, body `ImageUrlRequest`), and the API downloads it through `IRemoteImageFetcher`. The address is caller-controlled, so the fetcher refuses anything but `https` on the default port without credentials, refuses hosts given as private, loopback or link-local addresses before connecting, connects only to addresses it checked after DNS resolution (the guard OAuth client metadata uses), never follows a redirect, stops at 4 MB or 10 seconds, and lets the bytes, not the `Content-Type`, decide that it is a JPEG, PNG, WebP or GIF. Nothing reaches the AI provider until the image passed. A presigned two-step upload (`imageId`) and a file input in the food-photo widget are not built yet.

## Claude plugin

[`plugin/`](../plugin) packages the connector and the skills for Claude. Its `.mcp.json` points at `https://mizan.zaftech.co/mcp`, and it holds no credential. `plugin/skills` is a copy of the server's skills; a test fails if they differ, and `scripts/sync-plugin-skills.sh` copies them again. The [plugin guide](../plugin/README.md) covers installing it in Claude Code, claude.ai and Claude Desktop, pointing it at another server, updating and validating it.

Each deployment serves the plugin as a zip at `GET /mcp/plugin.zip`, with `.mcp.json` pointing at that deployment's own `/mcp`.

Run `claude plugin validate --strict ./plugin` before submitting. Submit from the developer portal at `claude.ai/directory/manage`. Before submitting, confirm the license holder in `plugin/LICENSE`, publish a privacy policy, and prepare a test account; if the directory asks for a callback allowlist, give it the redirect URIs of the clients you expect.

## Operate and verify

Compose starts `mcp` with the backend URL, the internal service keys, `Mcp__PublicUrl`, and `Mcp__AuthorizationServer`. `App__PublicUrl` names the web app for the billing link in limit and upgrade messages; without it the service uses `Mcp__PublicUrl` minus its trailing `/mcp`. `/health` reports transport availability. A healthy check alone does not prove token validation, permissions, or a write path; integration tests cover those separately.

The advertised metadata comes from `Mcp__PublicUrl`, not from request headers. Check it after a deploy: a wrong host in the 401 challenge is the usual reason a client cannot start sign-in. The service trusts `X-Forwarded-For` and `X-Forwarded-Proto` only from the loopback address, or from the one proxy named in `ReverseProxy__KnownProxy` (an IP address or a resolvable hostname such as a Compose service name), the same rule the API applies. Set it when the proxy runs on another host so the recorded client address is the caller's and not the proxy's.

Usage is recorded per connection, tool, resource, and prompt. A failed write should be checked in the log before blindly resubmitting, especially when a connection drops after the API may have committed it.
