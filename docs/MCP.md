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
   - `admin` is offered to administrators only and is off by default. Discovery advertises the admin scopes so a client that asks for every advertised scope lets an administrator turn them on; for anyone else they are dropped.
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

A connection is granted areas: `nutrition`, `training`, `body`, `planning`, `social`, `coaching` and `admin` (administrators only), each `:read` or `:write`, and write includes read. A grant made with the older scopes (`recipes`, `households`, `notifications`, `trainer`, `ai:use`, `profile`) keeps working: Connected apps and the consent screen show it as the areas it covers (`ToolAreas.FromScopes`), and an area implies the older scopes its tools need, so the legacy tools still answer a grant made with areas. Saving a grant again stores areas.

The legacy MCP layer holds each tool to a scope (`McpToolScopes`; a test fails if a tool has none). A client sees only the tools, resources, and prompts its grant covers, and a direct call outside it is refused naming the missing permission. The API enforces household and data-axis limits itself, so a bug in the MCP layer cannot widen a user's choice.

Personal read endpoints are gated by scope only. Household-owned records are filtered by membership and by the grant's households. Household writes are refused when the grant cannot see the household: creating one needs household access set to all households, and inviting, removing, leaving, deleting, switching or answering an invitation needs the household inside the grant. The refusal names the permission and where to change it (`HouseholdAccess.EnsureGrantCovers`).

The free plan allows 15 successful tool calls and resource reads per month. Prompts, skills, and app pages do not count; failures do not count.

## Task tools

The task tools are defined once, in `Mizan.Application/Tools` (`TaskToolCatalogue`), and serve the MCP connector, outside agents calling the API, and the in-app assistant ([AI](AI.md#consent)). A person sees 26: 24 personal (`find_food`, `get_nutrition`, `log_food`, `estimate_food`, `save_food`, `set_goal`, `get_training`, `log_workout`, `save_workout_template`, `get_body_trend`, `log_body`, `get_meal_plan`, `plan_meals`, `edit_meal_plan`, `get_shopping_list`, `update_shopping_list`, `set_reminder`, `get_my_account`, `update_my_settings`, `get_social`, `social_action`, `get_my_coach`, `manage_my_coach`, `delete`) and 2 household (`get_household`, `manage_household`). A trainer also sees 5 coaching tools (`get_clients`, `manage_clients`, `draft_client_plan`, `save_plan_template`, `ask_about_client`), 31 in all, and `delete` gains kind `meal_plan_template`. An administrator sees the 26 and 6 admin tools, 32 in all; an account holds one role, so no one sees both sets. An administrator who is viewing Mizan as someone else is refused every admin tool with `needs: not_impersonating` and sees that person's 26.

Each tool has modes picked by one argument (`view`, `kind`, `action` or `section`). A mode builds an existing MediatR command with the caller's id from the session, never from the arguments, and is held to a least role, a surface (`mcp`, `assistant` or both) and one area. `GET /api/tools?surface=mcp|assistant` lists what the caller may use: tools are filtered by role, then by the grant's areas, and each tool's mode enum is narrowed to the modes left. `POST /api/tools/{name}/call` with `{surface, arguments}` runs one through `ITaskToolRunner`, which checks role, surface and area again on every call and refuses with `errorCode` `not_allowed` and `needs` (the missing area) rather than guessing. A grant made with older scopes reaches a mode through the scopes of the legacy tools it replaces (`ToolMode.Replaces`), no further. Every call is audited as `CallTaskToolCommand` with tool, mode, surface and outcome.

Batches are one call: `log_food` takes `items[]` (foods, recipes, plan entries or known nutrition), `edit_meal_plan` takes `changes[]`, and `update_shopping_list` `add_items` takes `items[]`. Each checks every item first, then returns a receipt of what completed, failed and remains, so a retry repeats only what remains. `log_workout` takes exercises by id or by name and saves nothing while a name is unknown or ambiguous. Outside agents pass `householdId` on plans and shopping lists; the assistant always uses the active household.

`GET /api/tools/reference` (MCP resources `mizan://reference` and `mizan://reference/{price-regions,goal-types,meal-slots,macro-modes,reminder-kinds}`, open to every connection and not counted) lists the enabled price regions, goal types, meal slots, macro modes and reminder kinds the tools accept.

`Mcp:Toolset` chooses what the MCP service offers: `legacy` (the code default) keeps the tools below; `tasks` (the production Compose default) passes `tools/list` and `tools/call` through to the API manifest and call endpoints (`TaskToolProxy`), with the same sign-in, free-plan cap and usage log. The manifest is cached 60 seconds per grant and user, so a changed grant shows within a minute; the API checks every call regardless. Each tool carries the annotations the API sends with it ([tool hints](#tool-hints)), an output schema that promises an object, and its app sets `_meta.ui.resourceUri`. A result's `data` becomes `structuredContent`, always an object (an array as `{items}`, nothing as `{}`), a confirmation is added as `structuredContent.confirmation` `{token, expiresAt, preview}`, and an error carries `{errorCode, needs}`. `estimate_food` and `plan_meals` may run as tasks. The server instructions, prompts (`MizanTaskPrompts`) and skills (`SkillsTasks`, with `mizan-admin` beside the six shared names) name the task tools in this mode.

### Coaching tools

A connected app reads a client's area only when the client shares it with the coach and has also allowed AI to read it, because the data leaves Mizan for an outside model. `ClientReadAccess` checks both in Application; the web app needs only the first. A missing consent is refused with `client_ai_consent_required`, an unshared area with `forbidden`. On the connection side `coaching:read` is enough: its nutrition, training and body areas cover the coach's own data, not a client's ([AI](AI.md#consent)). `ask_about_client` spends the coach's allowance, never the client's.

The coaching chat (`get_clients` and `get_my_coach` view `chat`, web `GET /api/Chat/{relationshipId}`) reads a page at a time: the latest `take` messages (50 by default, at most 200), oldest first, or those before the message `before`. `hasOlder` says whether an earlier page remains; the first message's id is the next `before`.

### Admin tools

`admin_overview` (`section`), `admin_lookup` (`subject`) and `admin_inbox` (`queue`) read. Every other admin capability is an action in `AdminActionRegistry`, which holds each legacy admin tool once under its legacy name with a kind, a summary and the command it builds: 40 read-only, 26 that run at once, 26 destructive and 3 safety stops. `find_action` searches it, describes an action's arguments (JSON Schema made from the request type) and runs read-only actions. `run_action` runs the rest, one mode per action, with `arguments` as `describe` lists them. A destructive action returns a preview from `GetAdminActionPreviewQuery` (what is deleted, who a campaign or announcement reaches, a user's role and sessions, a setting's current value) and a token, and `confirm_action` carries it out under the rules below. Pausing a campaign, revoking a session and revoking a user's sessions are safety stops: they run at once and are audited like every command. A setting that holds a secret is refused at preview, so no secret is stored with a pending action; it is changed on the settings page. Reads need `admin:read`, actions and `confirm_action` need `admin:write`.

### Confirming a high-impact action

Deleting a meal plan, a recipe, a household membership (leaving, or removing a member), the social profile, or a shopping list's ticked items does not happen on the first call. `delete` returns a preview of what goes and a `confirmation` token; the model shows the preview, and only when the person agrees calls `delete` again with `kind: "confirm"` and the token. The token is stored hashed in `pending_tool_actions` with the arguments and a hash of the preview's facts. It works once, for ten minutes, for the same person and connection, and only while that connection still allows the action. Confirming rebuilds the command from the stored arguments, runs the preview again and refuses with `facts_changed` if what the person saw has changed, `token_used` or `token_expired` otherwise. The audit entry records the confirmed action.

### Tool hints

`ToolFilter.Hints` works out a tool's annotations for its worst mode, and `GET /api/tools` sends them as `hints`: `readOnlyHint` when every mode reads; `destructiveHint` when a mode deletes or changes what is there (a write tool is destructive unless every mode only adds, such as `log_food`); `idempotentHint` for reads and for writes that set a value (`update_my_settings`, `log_body`); `openWorldHint` where a mode reaches past Mizan's data (`estimate_food` asks a model, `save_food` `image` fetches a picture from a public address). A client that trusts the hints asks before a destructive tool, so a tool with one destructive mode is marked destructive as a whole. These tools mix modes, and their hint follows the worst: `update_my_settings`, `set_goal`, `plan_meals`, `edit_meal_plan`, `update_shopping_list`, `set_reminder`, `social_action`, `log_workout`, `save_workout_template`, `save_food`, `manage_my_coach`, `save_plan_template`, `run_action`, `delete` (a preview, then `confirm`), `find_action` (its `run` executes read-only actions only, so it stays read-only). `manage_household` creates, invites and switches the active household (`set_active`) and is marked additive; leaving or removing a member goes through `delete`.

### Reply size

A host may drop or cut a tool reply over 32 KB, text and `structuredContent` together. Every list is paged and capped so a busy account's largest reply stays under 24 KB, which a test measures: `find_food` returns at most 40 foods, `get_body_trend` 25 measurements, `get_training` `history` 10 workouts with each exercise's set count and top set (every set comes with `workoutId`), and `get_meal_plan` `plan` four days from `fromDay`, with the violations of those days and the plan's own; the text names the next `fromDay`. A mode that narrows its result does so in `ToolMode.Window`, before the result is shaped and described.

## Served to a hosted assistant through the gateway

A hosted assistant can reach the task tools through a gateway that signs its users in itself. The gateway authenticates as one client with a shared key, and names the person on every `tools/call` with a short-lived signed token in `_meta`. It is off unless configured, and needs `Mcp:Toolset` `tasks`.

The settings live under **Admin → Settings → Assistant gateway**. The MCP server reads them from the API (`GET /api/internal/settings/mcp-gateway`, with the service key) at startup and every minute, so a change applies within a minute without a restart. Until the first reading the gateway path is closed, and when a reading fails the last good one stays.

| Setting | Meaning |
| --- | --- |
| `Gateway.ClientKey` | The gateway's key, at least 32 characters, stored encrypted. |
| `Gateway.ClientKeyHeader` | The header that carries the key. Empty means `Authorization: Bearer <key>`. |
| `Gateway.MetaKey` | The `_meta` key that holds the identity token, for example `example/end_user`. |
| `Gateway.JwksUrl` | The gateway's signing keys, an `https://<gateway-host>/...` JWKS address. |
| `Gateway.Issuer`, `Gateway.Audience`, `Gateway.TokenType` | The token's `iss`, `aud` and header `typ`, as the gateway documents them. |
| `EndUser.Issuer` | Mizan's own end-user issuer, which the token must carry as `end_user_issuer` ([behind a gateway](AI.md#behind-a-gateway)). |

Set all of the gateway values or none: a partial set, a missing end-user issuer or a toolset other than `tasks` keeps the gateway path closed and logs which setting is missing. A deployment that set the old `Mcp__Gateway__*` variables copies them in by running `--import-env-settings` on the API with those variables set ([moving settings](ADMIN.md#moving-settings-out-of-the-environment)).

The gateway sees the tool list one person without a role of their own would see, cached once for every person, and it never sees admin tools: the API refuses an admin mode on this path with `needs: not_gateway`, whoever the person is. Resources are limited to skills, app pages and the reference lists. On each call the server checks the token before anything else and refuses with a code, never echoing the token:

- `gateway_token_missing`, `gateway_token_malformed`: no token under the meta key, or not a compact JWT with `exp` and `jti`.
- `gateway_token_algorithm`, `gateway_token_type`: the header is not `alg: EdDSA` with the configured `typ`.
- `gateway_token_unknown_key`, `gateway_token_signature`: the `kid` is not an Ed25519 key in the JWKS, or the signature does not verify. Keys are cached for an hour and fetched again for an unknown `kid`, at most once a minute.
- `gateway_token_expired`, `gateway_token_not_yet_valid`: `exp` or `nbf` is outside 60 seconds of skew.
- `gateway_token_issuer`, `gateway_token_audience`, `gateway_end_user_issuer`: a claim does not match its setting.
- `gateway_token_anonymous`, `gateway_token_subject`: `anonymous` is not an explicit `false`, or `sub` is not a Mizan user id.
- `gateway_token_replayed`: the `jti` was used before. Used ids are kept until the token expires, in Redis when `ConnectionStrings__Redis` is set and in memory otherwise, which only protects a single instance.
- `gateway_subject_unknown`: the user does not exist, is disabled or is deleted.

The call then runs as that person through the regular service key, never the admin key. The token only says who the person is; it grants no access. Every call on this path is held to the person's assistant consent ([AI](AI.md#consent)) by `AssistantConsent`, the rule the in-app assistant uses: a read needs that area's AI read consent and a write its write consent, and a refusal carries `errorCode` `ai_consent_required` with the area and access and says where to change it (Settings, Assistant). A trainer reading a client through the gateway also needs the client's AI consent (`ClientReadAccess`, [coaching tools](#coaching-tools)). OAuth connected apps are not held to these switches: the grant the person made is their consent. Usage rows from the gateway have no grant and record `{"path":"gateway"}`, and the audit entry of every call records `"Path":"gateway"`.

## Tools, resources, and prompts

Use `tools/list` for argument schemas. The source catalogue is [`backend/Mizan.Mcp.Server/Tools`](../backend/Mizan.Mcp.Server/Tools). Every tool has a title and is marked closed-world.

### Coverage

Every API endpoint is reached by a tool or is excluded with a stated reason. `McpParityCoverageTests` reads the controllers by reflection and the routes each tool calls from its source; a new endpoint with neither fails it, as does a stale exclusion. The same test holds each tool's scope to the API's check: a tool that reaches a `RequireAdmin` endpoint needs `admin`, and one that reaches a `RequireTrainer` endpoint needs a `trainer` scope or `ai:use`. Tools call the same routes as the web.

| Area (API) | Read | Write |
| --- | --- | --- |
| Food, diary, goals, prices (`Foods`, `Meals`, `Nutrition`, `Goals`, `Prices`) | `nutrition:read`: `search_foods`, `get_food`, `get_food_diary`, `get_daily_nutrition`, `rank_recipes_for_today` (reads the saved recipes too), `get_nutrition_range`, `get_nutrition_summary`, `get_current_goal`, `get_goal_history`, `get_goal_progress`, `get_food_price`, `list_price_regions` | `nutrition:write`: `create_food`, `update_food`, `delete_food`, `log_food`, `log_meal`, `log_meal_manual`, `log_day`, `delete_meal`, `delete_meal_group` (a whole logged recipe), `create_goal`, `record_goal_progress`, `delete_goal`, `delete_goal_progress`, `report_food_price` |
| Training (`Workouts`, `WorkoutTemplates`, `Exercises`) | `training:read`: workouts, stats, drafts, templates, exercises, next session | `training:write`: the same, created, changed and deleted |
| Body (`BodyMeasurements`) | `body:read`: `list_body_measurements` | `body:write`: `log_body_measurement` (one a day; a later write updates it), `delete_body_measurement` |
| Recipes (`Recipes`) | `recipes:read`: `search_recipes`, `get_recipe` | `recipes:write`: `promote_to_recipe`, `promote_recipe_to_preparation`, `update_recipe`, `delete_recipe`, `toggle_favorite_recipe`, `upload_image` |
| Plans and lists (`MealPlans`, `ShoppingLists`) | `planning:read`: plans, a day, targets, templates, lists | `planning:write`: plans, entries, prep sessions, status, `generate_meal_plan`, `generate_shopping_list`, list CRUD ([PLANNING](PLANNING.md)) |
| Households (`Households`) | `households:read`: `list_my_households`, `get_household`, `list_household_invitations` | `households:write`: create, switch, invite, respond, leave, remove a member |
| Profile (`Users`, `Achievements`, `Ai/consent`) | `profile:read`: profile, export, consent, achievements, streak, subscription | `profile:write`: `update_my_profile`, `set_ai_consent` |
| Notifications and reminders (`Notifications`, `Reminders`) | `notifications:read`: notifications, count, preferences, `list_reminders` | `notifications:write`: mark read, preferences, reminder CRUD, and the card buttons `complete_reminder`, `snooze_reminder`, `skip_reminder` |
| Social (`Social`) | `social:read`: profile, follows, feed | `social:write`: profile, follows, feed items, reactions, comments, reports |
| Coaching (`Trainers`, `Chat`) | `trainer:read`: trainers, requests, clients, `get_client_nutrition`, `get_client_workouts`, `get_coach_chat` | `trainer:write`: requests, `set_trainer_permissions`, `send_coach_message`, client drafts, a coach's templates |
| Assistant (`Ai`, `Nutrition/ai`) | `ai:use` (spends the allowance): `ask_ai`, threads, usage, `suggest_meals`, `analyze_food_image`, `analyze_food_text`, `delete_food_photo` (an analysis or chat photo, before its removal 30 days on), `ask_ai_about_client` (coaches) | |
| Administration (`admin/*`, `AuditLogs`, `Households/admin`, `Admin/Ai`) | `admin` (only an administrator can grant it): users, sessions, relationships, audit log, jobs, prompts, flags and overrides, allowances, settings, mail, contact, support notes, households, price regions, sources, review and reports, campaigns, overview, health | |

Excluded on purpose, each with its reason in the test: sign-in, account credentials and account deletion; OAuth and discovery; managing connected apps; service-to-service routes (MCP tasks, Telegram resolve and sessions, the prices worker, internal settings, the Paddle webhook, health); linking Telegram or a device and offline sync; payments and Paddle catalogue changes; public pages and signed links; impersonation; creating or deleting accounts as an administrator; deleting a household (the web previews what other members lose); console file downloads and 3D model uploads; the web form helpers `Meals/preview`, `Nutrition/log`, the chat photo upload and onboarding; and the AI settings test, which spends a provider call.
JSON arguments (`rulesJson`, `entriesJson`, `prepSessionsJson`) carry the records from `Mizan.Contracts/Planning`, as `log_day` does for meals. `get_plan_day` answers in plain text, not an error, when no active plan covers the date.

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

In tasks mode the same pages open from `get_nutrition` (day view), `estimate_food`, `get_body_trend` and `get_meal_plan`. The served page carries `const toolset = "tasks"`, and its own calls use the task tools: the day view calls `get_nutrition`, the photo review logs ticked items with one `log_food` batch and marks what the receipt completed, and the meal plan logs with `log_food` `items: [{planEntryId, planId}]` and builds the list with `update_shopping_list` `action: "from_plan"`.

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

Results that report on a write or a read say what they found:

- `log_meal` returns the `groupId` and every row's id in `entryIds`. `log_day` reports `includesWeight` when a workout or measurement carried a weight.
- `get_current_goal` returns `{"goal": null, "message": ...}` without an active goal, and `get_workout_draft` returns `{"draft": null, "message": ...}` without a draft.
- `update_workout` takes the id from its argument; the body need not repeat it. `save_workout_draft` refuses a payload that is not `exercises` with an `exerciseId` and `sets` each.
- `record_goal_progress` keeps one check-in a day; the same day again replaces it.
- `generate_meal_plan` returns each draft with `unmet` (each rule it does not meet, with the amount and the target) and a `note` written by code, never by the model (see [Planning](PLANNING.md#ai-planning)).
- Training progression is per template: `get_next_workout_session` starts from the last workout logged with that template, and before one exists from each exercise's `targetWeightKg`. Workouts without the template and personal records do not set it.

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

### Evaluating the toolsets

`ToolsetEvalTests` runs a live model against both toolsets with the same seeded people, the same requests and the same MCP service in process, and scores each run by the database state or the answer. It is skipped unless `MIZAN_TOOL_EVAL=1`, and it needs `TEST_DB_CONNECTION` like the other integration tests and an OpenAI-compatible chat-completions endpoint in `EVAL_BASE_URL`, `EVAL_KEY` and `EVAL_MODEL` (`EVAL_MAX_TOKENS` defaults to 4000). Keep the key out of the repo and the shell history; the harness never writes it or the address.

```bash
MIZAN_TOOL_EVAL=1 dotnet test backend/Mizan.Tests --filter "FullyQualifiedName~ToolsetEvalTests"
```

`MIZAN_TOOL_EVAL_RUNS` (3 per request), `MIZAN_TOOL_EVAL_PARALLEL` (3), `MIZAN_TOOL_EVAL_ARMS` (`legacy,tasks`) and `MIZAN_TOOL_EVAL_CASES` (case ids or groups) narrow a run. Each run is a line in `runs-*.jsonl` with every call, and `summary.md` holds success, wrong calls, calls and tokens per arm and group; both go to `MIZAN_TOOL_EVAL_OUT` (by default the test's `TestResults/tool-eval`) and are not committed. A call is wrong when it errors or is not one the request needs. Compare arms from the same run: the model and the backend change between runs.
