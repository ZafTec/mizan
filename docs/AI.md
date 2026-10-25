# AI platform

Mizan's assistant, food-photo analysis, onboarding, trainer questions, and prompt evaluations use one backend AI platform. Web, MCP, and Telegram call that platform rather than calling a model provider independently.

## Logging a meal from words or a photo

`POST /api/Nutrition/ai/analyze-text` turns a sentence ("two eggs, toast with butter") into the same structured proposal as `analyze-image`: foods with portion grams and macros, a confidence, and a note naming any portions the model assumed. Both are Pro features, share the `food_analysis` prompt and the `FoodAnalysis` quota line, and write nothing. A description is limited to 600 characters, and a reply that does not match the declared schema is a failed call, never scraped from prose.

Each recognized food also carries `per100g`, derived from its portion. The logging sheet leads with this. The proposal lands in the meal as editable rows (name and grams), and only what the person confirms is saved, one `POST /api/Meals` per food with `amountGrams` and `per100g` under an `Idempotency-Key` minted when the row was proposed: the server computes the stored totals (rounded to two decimals, like a catalogue food), ignores any client totals sent alongside, and treats a retry as a replay. The MCP photo app sends the same figures through `log_meal_manual`. Free accounts see an inline upgrade note at the moment they try, and food search and recipes stay free.

## Drafting a meal plan

`POST /api/MealPlans/generate` runs `GenerateMealPlanCommand` (body `GenerateMealPlanRequest`: rules, `durationDays` 1 to 14, `startDate`, `preference`, and up to three `budgetCeilings`). Each budget ceiling is one call on the planning line, reserved and settled through `IAiQuotaService` like any other, and one draft. The context comes through `IDataAccessPolicy`: the goal needs the nutrition axis, the person's recipes and own foods need the planning axis, and the public catalogue (the allow list, foods the preference names, foods with a price) and its prices per kilogram are always offered. The prompt is `meal.plan` and the answer must match the strict `meal_plan_v1` schema: days of entries with slot, kind (`food`, `recipe`, `text`), food name or recipe id, grams or servings, and a note, plus prep sessions. An answer that does not match is a 503, never scraped.

Code turns the answer into a plan. A recipe must be one of the listed recipe ids. A food name resolves to a catalogue food by exact name (case, spacing and punctuation ignored), then by a whole-word search; a line that does not resolve, or has no grams, is saved as a text entry whose note says what to review, and is listed in `unresolved`. `PlanRuleCheck` and `PlanCostCalculator` then give the violations and the cost table in the person's price region; the budget rule is checked against the weekly cost. The model never writes nutrition or prices. Each draft is saved with status `Draft` and `generatedFrom` (prompt key and version, model, budget ceiling, currency). When the allowance runs out after the first ceiling, the drafts made stand and the rest are listed in `skipped`. The person accepts a draft with `POST /api/MealPlans/{id}/accept` (it becomes the active plan) or declines it (archived). `GET /api/MealPlans/day?planId=` reads a draft's day before that.

On the web, **Draft with the assistant** on `/meal-plan/new` sends the start date, the length, an optional weekly budget (the one budget ceiling) and the person's words under an `Idempotency-Key` that is kept while the same request is tried again. The preview shows the API's days, rule breaks, lines to review and cost table; **Keep** opens the saved draft and **Discard** declines it (the plan is archived, as Telegram's **Discard** does). A 429 shows the API's message and the reset time, a 503 a hint to try again. Without AI consent the action is replaced by the assistant's link to choose what it can see.

## Configuration

The provider speaks the OpenAI-compatible chat-completions protocol. Set the Compose variables `AI_BASE_URL`, `AI_API_KEY`, and `AI_MODEL`, or the equivalent backend `Ai:BaseUrl`, `Ai:ApiKey`, and `Ai:Model` configuration. An empty endpoint disables AI while basic logging remains available.

`AI_SUPPORTS_TEMPERATURE=false` omits temperature for providers that reject it. Optional `AI_REASONING_EFFORT` sets the provider's reasoning effort; Azure GPT-5.6 chat-completion tool calls require `none`. Leave it empty for providers that do not accept this field.

`AI_GLOBAL_DAILY_TOKENS` and `AI_GLOBAL_DAILY_COST_MICROS` cap provider use across the application. Configure provider prices accurately when relying on the cost ceiling. Token and cost ceilings are enforced by code, not prompt text.

## Consent

`GET` and `PUT /api/Ai/consent` expose the current user's consent. Three read axes start off: nutrition, training, and body measurements. Write permission is separately controlled by the global `allowWrites` switch plus per-axis write flags. A fourth axis, `DataAxis.Planning` (meal plans and shopping lists), has no flag of its own: reading it follows the nutrition share flag and writing it the nutrition write flag. A trainer grant never covers it, and a connected app needs `planning:read` or `planning:write`.

`IDataAccessPolicy` determines which axes may be fetched into AI context. A trainer must have both an active client grant for the axis and that client's AI consent. Missing permission means the data is absent from context. Access is re-evaluated at execution, including allowlisted AI tools, so a revoked grant is not extended by an old conversation.

Normal chat and food analysis produce responses or proposals for review. Onboarding is a distinct tool-enabled flow: it can perform allowlisted setup writes when the user granted the relevant write permission. Its tools run existing MediatR commands with normal validation and audit behavior, and cannot supply arbitrary user IDs or call arbitrary endpoints. Do not describe all AI as read-only, and do not broaden the allowlist through editable prompts.

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

Defaults come from `AiOptions` and are configurable. Every line shares the global default ceilings of 5,000,000 tokens and 20,000,000 cost micros per UTC day. Trainer usage belongs to the trainer, so a coach cannot consume a client's allowance. Chat is quota-limited on Free; it is not uniformly Pro-only. Image analysis has its own entitlement gate.

A planning request is one meal plan draft for one budget ceiling, so Free gets one plan a day; planning and personal chat do not spend each other's allowance. `GET /api/Ai/usage` reports personal usage, with the planning line under `planning`; the usage panel in profile settings shows it as **Meal plans drafted**. Administrators use `GET /api/Ai/usage/global`. Reservation, provider call, and settlement belong together; adding a direct unmetered provider call is a billing defect.

## Surfaces

| Endpoint | Purpose |
| --- | --- |
| `POST /api/Ai/chat` | Shared persistent chat |
| `POST /api/Ai/chat/image` | Image in a chat turn |
| `GET /api/Ai/threads`, `/threads/{id}` | Conversation history |
| `POST /api/Ai/suggestions` | Structured meal suggestions |
| `GET/POST /api/Ai/onboarding` | Setup conversation and state |
| `GET /api/Ai/onboarding/tools` | Setup allowlist |
| `POST /api/Ai/clients/{clientId}/ask` | Trainer question under intersected grants |
| `POST /api/Nutrition/ai/analyze-image` | Food-photo proposal |
| `POST /api/MealPlans/generate` | Meal plan drafts, one per budget ceiling |

Structured responses must pass their declared schema before use.

### Planning tools and typed cards

Chat has six planning tools, each an existing command on `DataAxis.Planning`: `generate_meal_plan`, `get_plan_day` (the active plan's day, or a named plan's, drafts included), `add_plan_entry`, `update_plan_entry`, `remove_plan_entry`, and `generate_shopping_list`. Update and remove refuse a plan that is not a draft. The tool result the model reads is plain text with the ids it needs for the next call.

`AiChatTurnDto.cards` carries what the person sees: `{ type, data }` with `type` one of `plan_preview`, `day_plan`, `cost_table`, `shopping_list`, and `data` exactly that type's record in `Mizan.Contracts/Ai/AiCards.cs` (`PlanPreviewCardData`, `DayPlanCardData`, `CostTableCardData`, `ShoppingListCardData`, all in the OpenAPI document). Cards are built by code from the commands' results and checked by `AiCardValidator`: an unknown type, a missing or unknown member, or a null the record does not allow is dropped. Nothing the model writes becomes a card, and no surface renders model-written markup. The web renders each type with one component under `frontend/components/ai/cards/`, linking to the plan or list it names, and leaves out a type it does not know. A malformed provider result is an error, not something to recover by scraping prose. Photo proposals show quantities and nutrition for confirmation before logging.

## Prompt administration

The administrator console at `/admin/ai` manages the prompts listed in `AiPromptKeys`, `meal.plan` among them, with draft versions, synthetic evaluation cases, comparison, publishing, and rollback. A published version remains selected until another eligible version is published. The publish gate requires the current draft's evaluation evidence; editing a draft invalidates older passing evidence.

Editable instructions control tone and task guidance. Code-owned constraints control data access, write tools, output validation, and quota. The console displays these constraints read-only. Evaluations run synthetic cases, consume the evaluation allowance and global ceiling, and execute through the durable outbox. Administrator access does not make arbitrary users' private data available for prompt testing.

## Verification

PostgreSQL integration tests cover consent, trainer intersections, quota concurrency, chat/onboarding, and publish gates. Provider fakes in those tests allow deterministic error and quota scenarios; they do not prove a deployment's external endpoint or model compatibility. A configured provider requires a separate live request, with usage inspected afterward. Browser fixture tests likewise establish interface behavior without spending provider quota.
