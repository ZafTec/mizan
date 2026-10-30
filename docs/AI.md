# AI platform

Mizan's assistant, food-photo analysis, onboarding, trainer questions, and prompt evaluations use one backend AI platform. Web, MCP, and Telegram call that platform rather than calling a model provider independently.

## Logging a meal from words or a photo

`POST /api/Nutrition/ai/analyze-text` turns a sentence ("two eggs, toast with butter") into the same structured proposal as `analyze-image`: foods with portion grams and macros, a confidence, and a note naming any portions the model assumed. Both are Pro features, share the `food_analysis` prompt and the `FoodAnalysis` quota line, and write nothing. A description is limited to 600 characters, and a reply that does not match the declared schema is a failed call. A ```json fence or a sentence around the one JSON object is dropped before it is read (`StructuredReply`); nothing else is scraped from prose.

Each recognized food also carries `per100g`, derived from its portion. The logging sheet leads with this. The proposal lands in the meal as editable rows (name and grams), and only what the person confirms is saved, one `POST /api/Meals` per food with `amountGrams` and `per100g` under an `Idempotency-Key` minted when the row was proposed: the server computes the stored totals (rounded to two decimals, like a catalogue food), ignores any client totals sent alongside, and treats a retry as a replay. The MCP photo app sends the same figures through `log_meal_manual`. Free accounts see an inline upgrade note at the moment they try, and food search and recipes stay free.

## Drafting a meal plan

`POST /api/MealPlans/generate` runs `GenerateMealPlanCommand` (body `GenerateMealPlanRequest`: rules, `durationDays` 1 to 14, `startDate`, `preference`, up to three `budgetCeilings`, and an optional `dailyBudget` for every draft). Each budget ceiling is one call on the planning line, reserved and settled through `IAiQuotaService` like any other, and one draft. When code finds a problem in the draft (a day with no food, or a `PlanRuleCheck` violation, with any names the catalogue lacks), the findings go back to the model once as a repair call: a second request on the same line, reserved and settled on its own. The repaired draft is kept when it has fewer empty days, then fewer violations; when the repair is refused by the allowance or fails, the first draft stands, or is refused as below. Free's one planning request a day therefore leaves no room for a repair. The context comes through `IDataAccessPolicy`: the goal needs the nutrition axis, the person's recipes and own foods need the planning axis, and the public catalogue (the allow list, foods the preference names, foods with a price) and its prices per kilogram are always offered. The prompt is `meal.plan` and the answer must match the strict `meal_plan_v1` schema: days of entries with slot, kind (`food`, `recipe`, `text`), food name or recipe id, grams or servings, and a note, plus prep sessions. A fence or prose around the JSON object is dropped first; an answer that still does not match is a 503. A draft with a day that has no food or recipe entry is not saved: the reply is 422 `plan_needs_foods` (`PlanNeedsFoodsError`: `error`, `missingFoods`, `emptyDays` from 0). When the person has no recipes and the usable catalogue has no foods at all, the same refusal comes before any call, so no allowance is spent. MCP, Telegram and the chat say the message as it is.

Code turns the answer into a plan. A recipe must be one of the listed recipe ids. A food name resolves to a catalogue food by exact name (case, spacing and punctuation ignored), then by a whole-word search; a line that does not resolve, or has no grams, is saved as a text entry whose note says what to review, and is listed in `unresolved`. `PlanRuleCheck` and `PlanCostCalculator` then give the violations and the cost table in the person's price region; the weekly budget is checked against the weekly cost and a daily budget, which the prompt also names, against each day's cost. The model never writes nutrition or prices. Each draft is saved with status `Draft` and `generatedFrom` (prompt key and version, model, budget ceiling, currency). When the allowance runs out after the first ceiling, the drafts made stand and the rest are listed in `skipped`. The person accepts a draft with `POST /api/MealPlans/{id}/accept` (it becomes the active plan) or declines it (archived). `GET /api/MealPlans/day?planId=` reads a draft's day before that.

On the web, **Draft with the assistant** on `/meal-plan/new` sends the start date, the length, an optional weekly budget (the one budget ceiling), an optional daily budget and the person's words under an `Idempotency-Key` that is kept while the same request is tried again. The preview shows the API's days, rule breaks, lines to review and cost table; **Keep** opens the saved draft and **Discard** declines it (the plan is archived, as Telegram's **Discard** does). A 429 shows the API's message and the reset time, a 503 a hint to try again. Without AI consent the action is replaced by the assistant's link to choose what it can see.

## Configuration

The provider speaks the OpenAI-compatible chat-completions protocol. An administrator sets `Ai.BaseUrl`, `Ai.ApiKey` and `Ai.Model` under **Admin → Settings → AI provider** and checks the endpoint with **Test provider** ([runtime settings](ADMIN.md#runtime-settings)); a change applies to the next call without a restart. They are not read from the environment. An empty endpoint disables AI while basic logging remains available, and the page says so.

`Ai.SupportsTemperature=false` omits temperature for providers that reject it. Optional `Ai.ReasoningEffort` sets the provider's reasoning effort; Azure GPT-5.6 chat-completion tool calls require `none`. Leave it empty for providers that do not accept this field.

`Ai.GlobalDailyTokens` and `Ai.GlobalDailyCostMicros` (**AI limits**) cap provider use across the application, and each tier's daily requests and tokens sit beside them. Configure provider prices accurately when relying on the cost ceiling. Token and cost ceilings are enforced by code, not prompt text.

### Provider errors

A non-success status from the provider is logged at warning level with the status and the provider's error `type`, `code`, `param` and `message`; the message is cut to 300 characters, and the configured key and anything shaped like a key or a bearer token are replaced. The request (the prompt) is never logged, a body that is not an error object keeps no message, and the caller never receives the body. The client throws `AiProviderRejectedException`, answered as `503 ai_unavailable`:

- 400, 401, 403, 404 and 422 are configuration errors that retrying does not fix. The person reads "The assistant is misconfigured. An administrator has been told.", and every administrator gets one `ai_misconfigured` notification per provider message an hour, linking to **Admin → Settings → AI**, with the status and the provider's message.
- 429 and 5xx keep "Try again shortly"; timeouts and unreachable endpoints keep their own messages.

Reasoning models on Azure refuse function tools on `/chat/completions` together with their default reasoning ("Function tools with reasoning_effort are not supported … set reasoning_effort to 'none'"). Set `AI_REASONING_EFFORT=none` (or `Ai.ReasoningEffort` in the admin area) for such a deployment. When it is unset, a tool request that the provider refuses for `reasoning_effort` is sent again once, inside the same metered call, with `reasoning_effort: none`, and later tool requests to that endpoint and model start with it until the process restarts; a warning names the setting to change. Requests without tools are unchanged. The Responses API (`/v1/responses`) is not supported. **Test provider** checks a tool call as well as a plain reply ([administration](ADMIN.md#runtime-settings)).

## Consent

`GET` and `PUT /api/Ai/consent` expose the current user's consent. Three read axes start off: nutrition, training, and body measurements. Write permission is separately controlled by the global `allowWrites` switch plus per-axis write flags. A fourth axis, `DataAxis.Planning` (meal plans and shopping lists), has no flag of its own: reading it follows the nutrition share flag and writing it the nutrition write flag. A trainer grant never covers it, and a connected app needs `planning:read` or `planning:write`.

`IDataAccessPolicy` determines which axes may be fetched into AI context. A trainer must have both an active client grant for the axis and that client's AI consent. Missing permission means the data is absent from context. Access is re-evaluated at execution, including allowlisted AI tools, so a revoked grant is not extended by an old conversation.

Normal chat and food analysis produce responses or proposals for review. Onboarding is a distinct tool-enabled flow: it can perform allowlisted setup writes when the user granted the relevant write permission. Its tools run existing MediatR commands with normal validation and audit behavior, and cannot supply arbitrary user IDs or call arbitrary endpoints. Do not describe all AI as read-only, and do not broaden the allowlist through editable prompts.

### Consent refusals

A chat tool call the person's consent does not allow is refused by `AiToolRunner` before it runs. The model still reads a one-line reason, so its reply can explain, and the turn carries the typed refusal in `AiChatTurnDto.consentRequired`: one `AiConsentRequirement` `{ errorCode: "ai_consent_required", axis, access }` per switch, where `axis` is `nutrition`, `training` or `body` and `access` is `read` or `write`. Planning has no switch of its own, so a plan or shopping list refusal names `nutrition` (`UserAiConsent.SwitchFor`). The list is empty when nothing was refused. A connected app's grant refusal is a different thing and stays text. The web panel shows an **Allow** button under the reply that turns on that one switch and offers to send the message again; MCP `ask_ai` returns the turn as `structuredContent` with a text line naming the setting (`/profile/settings#assistant`); the Telegram chat ends the reply with the same link. `/planme` drafts through `POST /api/MealPlans/generate`, which reads under `IDataAccessPolicy` and has no tool call to refuse.

Weight writes require body-axis permission and belong to the measurement tool; goal tools cannot bypass that consent. Render model-supplied Markdown images as links so a reply cannot trigger automatic requests to external image hosts.

## Metering

Every provider call reserves capacity through `IAiQuotaService`, then settles actual usage. The ledger counts failures and administrative evaluations appropriately. There are separate daily allowance lines:

| Line | Default requests | Default tokens |
| --- | ---: | ---: |
| Free personal | 5 | 20,000 |
| Pro personal | 200 | 500,000 |
| Onboarding | 60 | 60,000 |
| Trainer-client | 300 | 600,000 |
| Free planning | 1 | 30,000 |
| Pro planning | 20 | 400,000 |
| Evaluation | 400 | 400,000 |

Defaults come from `AiOptions` and are configurable. An administrator can override them per user, household or billing plan ([administration](ADMIN.md#usage-limits)): each cap is taken from the user's override, then the household's, then the plan's, then the tier default, field by field, and `IAiQuotaService` enforces the result. Overrides may add a daily and a monthly cost cap, checked next to the request and token caps. A refusal answers `429 ai_quota_exceeded` with `cap` (`daily_requests`, `daily_tokens`, `daily_cost`, `monthly_cost`) and a message naming it, and is written to the ledger as a zero-cost `Refused` row that counts toward no allowance. Every line shares the global default ceilings of 5,000,000 tokens and 20,000,000 cost micros per UTC day. Trainer usage belongs to the trainer, so a coach cannot consume a client's allowance. Chat is quota-limited on Free; it is not uniformly Pro-only. Image analysis has its own entitlement gate.

A planning request is one meal plan draft for one budget ceiling, so Free gets one plan a day; planning and personal chat do not spend each other's allowance. `GET /api/Ai/usage` reports personal usage, with the planning line under `planning`; the usage panel in profile settings shows it as **Meal plans drafted**. Administrators use `GET /api/Ai/usage/global`. Reservation, provider call, and settlement belong together; adding a direct unmetered provider call is a billing defect.

## Surfaces

| Endpoint | Purpose |
| --- | --- |
| `POST /api/Ai/chat` | Shared persistent chat |
| `POST /api/Ai/chat/image` | Image in a chat turn |
| `GET /api/Ai/threads`, `/threads/{id}` | Conversation history |
| `GET /api/Ai/conversation`, `POST /api/Ai/conversation/context` | The current conversation and app context ([app context](#app-context)) |
| `POST /api/Ai/suggestions` | Structured meal suggestions |
| `GET/POST /api/Ai/onboarding` | Setup conversation and state |
| `GET /api/Ai/onboarding/tools` | Setup allowlist |
| `POST /api/Ai/clients/{clientId}/ask` | Trainer question under intersected grants |
| `POST /api/Nutrition/ai/analyze-image` | Food-photo proposal |
| `POST /api/Nutrition/ai/analyze-image-url` | The same, for a photo at a public https address the API downloads ([MCP](MCP.md#images)) |
| `POST /api/MealPlans/generate` | Meal plan drafts, one per budget ceiling |

Structured responses must pass their declared schema before use.

### The web panel

On the web the assistant is a side panel in the app shell (`components/ai/Assistant.tsx` on the shared host `components/panels/SidePanel.tsx`), opened by **Ask Mizan** in the top bar on every signed-in screen. It shares its place with the notifications panel: opening one closes the other. From 768px it docks on the right and leaves the page usable; below that it is a full-height sheet that holds focus. Escape closes it and focus returns to the launcher. The shell keeps the panel and its conversation across client navigation, and remembers both per person in the browser. `/ai` redirects to `/today?panel=assistant`, and `/ai?thread=<id>` opens that conversation. Until the person has chosen what it may use (both master switches off), the panel asks in place of the suggestions with the same step onboarding uses (`AiPermissionGate`); **Not now** leaves everything off and keeps chat open with a line saying it cannot see the log. A chat turn may write, so each attempt sends a new `Idempotency-Key`.

### Planning tools and typed cards

Chat has six planning tools, each an existing command on `DataAxis.Planning`: `generate_meal_plan`, `get_plan_day` (the active plan's day, or a named plan's, drafts included), `add_plan_entry`, `update_plan_entry`, `remove_plan_entry`, and `generate_shopping_list`. Update and remove refuse a plan that is not a draft. The tool result the model reads is plain text with the ids it needs for the next call.

### App context

A client can tell the conversation where the person is without saying it for them. `POST /api/Ai/conversation/context` (`AiContextNoteRequest`: `note`, optional `holdMinutes` up to 60) stores a fact such as "the /remind buttons chose Log a meal and now ask Which days?" as a context message in the current thread, with no model call. A positive hold keeps it waiting: `GET /api/Ai/conversation` (`AiConversationDto`) returns the current thread and the waiting note, and the client sends that note as `clientContext` on `POST /api/Ai/chat` (`AiChatRequest`) with the person's words. A chat turn with context clears the wait; the note stays in the thread and is replayed with later turns. A thread's history (`GET /api/Ai/threads/{id}`) shows only what was said.

The provider adapter (`NutritionAiService`) is the one place a note becomes model input: a system message that marks it as facts the app reported, not the person's words. Nothing else knows it is for a model, so an external agent reads and sends the same fields through MCP (`get_ai_conversation`, `ask_ai` with `clientContext`).

The current conversation is the newest chat thread unless it has been quiet longer than `Ai.ConversationGapHours` (3 by default, a runtime setting); then `threadId` is null and the next message or note opens a new thread. A note still waiting outlives the gap and goes with the first message of the new thread.

### Reminder tools

`create_reminder` and `list_reminders` (`AiReminderTools`) build the same commands as `/api/Reminders` ([reminders](ARCHITECTURE.md#reminders)), so the kinds, the hourly floor and the plan's cap hold without anything in a prompt. How to call it is in the tool's description, `ReminderToolGuidance.CreateReminder` in `Mizan.Contracts`, which MCP `create_reminder` uses too: use only this request and app context, ask one question when the kind, days, time or count is missing or contradictory, never substitute another repeat, and keep the schedule out of the title. They sit on `DataAxis.Planning`: creating needs the nutrition write switch, and a refusal is the usual `ai_consent_required` for `nutrition`/`write`. Changing or deleting a reminder stays with the person, in Settings, MCP or the bot.

`AiChatTurnDto.cards` carries what the person sees: `{ type, data }` with `type` one of `plan_preview`, `day_plan`, `cost_table`, `shopping_list`, and `data` exactly that type's record in `Mizan.Contracts/Ai/AiCards.cs` (`PlanPreviewCardData`, `DayPlanCardData`, `CostTableCardData`, `ShoppingListCardData`, all in the OpenAPI document). Cards are built by code from the commands' results and checked by `AiCardValidator`: an unknown type, a missing or unknown member, or a null the record does not allow is dropped. Nothing the model writes becomes a card, and no surface renders model-written markup. The web renders each type with one component under `frontend/components/ai/cards/`, linking to the plan or list it names, and leaves out a type it does not know. A malformed provider result is an error, not something to recover by scraping prose. Photo proposals show quantities and nutrition for confirmation before logging.

## Prompt administration

The administrator console at `/admin/ai` manages the prompts listed in `AiPromptKeys`, `meal.plan` among them, with draft versions, synthetic evaluation cases, comparison, publishing, and rollback. A published version remains selected until another eligible version is published. The publish gate requires the current draft's evaluation evidence; editing a draft invalidates older passing evidence.

A `chat.system` case is offered the chat's tools with their real descriptions, and nothing is run: each call the model asks for is written into the output as `[tool call] name {compact JSON}`, so a case asserts behaviour against the tool contract, such as asking instead of calling `create_reminder` for "once a week" in a meal reminder flow, or never `"repeat":"days"` for a monthly request.

Editable instructions control tone and task guidance. Code-owned constraints control data access, write tools, output validation, and quota. The console displays these constraints read-only. Evaluations run synthetic cases, consume the evaluation allowance and global ceiling, and execute through the durable outbox. Administrator access does not make arbitrary users' private data available for prompt testing.

## Verification

PostgreSQL integration tests cover consent, trainer intersections, quota concurrency, chat/onboarding, and publish gates. Provider fakes in those tests allow deterministic error and quota scenarios; they do not prove a deployment's external endpoint or model compatibility. A configured provider requires a separate live request, with usage inspected afterward. Browser fixture tests likewise establish interface behavior without spending provider quota.
