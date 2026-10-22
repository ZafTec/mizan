# Meal planning

This document is the design and the hand-off plan for meal planning in Mizan. It names what exists, what to build, in which order, and what is deferred. Read [architecture](ARCHITECTURE.md) first. Read [AI](AI.md) before the AI steps and [Telegram](TELEGRAM.md) before the bot steps.

## Goal

A person gets a plan they can follow. The shopping list and the cost come from that plan. The plan is strict when the person asks for strict. The same plan is visible on the web, through MCP, and in Telegram. Administrators publish templates. A coach drafts a plan for a client, and the client accepts it.

## Branch and delivery rules

- Work on branches from `c/sleepy-fermi-771dlv`, one branch per step. Open each pull request against `c/sleepy-fermi-771dlv`, never against `master`.
- Each step is one vertical slice in one pull request. The slice holds the command, API, MCP tool, Telegram command, web page, documentation, and tests.
- The branch carries one EF migration. Make model changes, delete that migration with the EF tools, and generate it again. Do not edit a migration file by hand. The `master` chain stays as it is.
- Run the checks in [README](../README.md#verify) before a push.

## Domain model

### Plan

`MealPlan` gets these fields:

| Field | Type | Meaning |
| --- | --- | --- |
| `Kind` | `Personal`, `Template` | A template has no dates and belongs to an administrator (`UserId` null) or a coach. |
| `Status` | `Draft`, `Active`, `Archived` | A plan from a coach starts as `Draft`. The client accepts it to make it `Active`. |
| `AuthoredByUserId` | Guid, nullable | The coach who drafted the plan. Null when the owner wrote it. |
| `DurationDays` | int | Number of days. A personal plan also has `StartDate`. `EndDate` is derived. |
| `Rules` | jsonb | See below. |

Entries use a day index, not a date. `Date = StartDate + DayIndex`. A template copies into a plan without a date change.

### Entry

`PlanEntry` replaces `MealPlanRecipe` and the `meal_plan_recipes` table.

| Field | Type | Meaning |
| --- | --- | --- |
| `DayIndex` | int | 0 is the first day. Must be below `DurationDays`. |
| `Slot` | `MealTypes` constant | `BREAKFAST`, `LUNCH`, `DINNER`, `SNACK`. One vocabulary with the diary. |
| `Kind` | `Recipe`, `Food`, `Text` | What the entry points at. |
| `RecipeId`, `Servings` | Recipe entries | Nutrition is per serving times servings. |
| `FoodId`, `Grams` | Food entries | Nutrition is per 100 g times grams / 100. |
| `Text` | Text entries, max 255 | A line with no nutrition. Listed as unresolved. |
| `Notes` | nullable | Free text for the person. |
| `SortOrder` | int | Order inside a slot. |
| `PrepSessionId` | nullable | The cook session that makes this entry. |

### Rules

`PlanRules` is one owned object stored as jsonb on the plan. Code validates it. The model never does.

| Rule | Type | Effect |
| --- | --- | --- |
| `MacroMode` | `None`, `Keto`, `HighProtein`, `Custom` | Sets default targets. `Custom` uses the explicit targets. |
| `TargetCalories`, `TargetProtein`, `MaxCarbs`, `TargetFat` | nullable numbers | Daily targets. `Keto` sets `MaxCarbs` to 30 g when empty. |
| `EatingWindowStart`, `EatingWindowEnd` | time, nullable | Shown on the plan. Drives the evening reminder in Telegram. |
| `AllowedFoodIds`, `ForbiddenFoodIds` | Guid lists | A forbidden food in an entry is a violation. An allow list makes every other food a violation. |
| `WeeklyBudget`, `Currency` | decimal, 3-letter code | The ceiling for costing. |
| `PrepStyle`, `BatchDays` | `Daily`, `Batch`, plus day indexes | `Batch` groups entries into prep sessions on the given days. |

`PlanRuleCheck` in Domain evaluates a plan and returns violations: day, slot, rule, message. The web page, the MCP tool result, and Telegram show the same list.

### Prep session

`PrepSession` has `MealPlanId`, `DayIndex`, `Title`, `Notes`. Entries reference it. The shopping list groups by session when sessions exist. A session can also become a preparation (`Recipe.IsPreparation`) when the person logs the cooked batch.

### Shopping list

`ShoppingList` gets `MealPlanId` (nullable) and `GeneratedAt`. `ShoppingListItem` gets `Grams` (nullable, normalized), `NeedsReview` (bool), and `SortOrder`. `FoodId` is set when aggregation resolves the food.

`ShoppingAggregator` in Domain builds the items:

1. For each recipe entry, scale each ingredient line by `Servings / Recipe.Servings`.
2. Convert each line to grams with the existing unit converter when a food is linked.
3. Group by `FoodId`, or by normalized text when no food is linked. Sum the grams.
4. A line that cannot convert keeps its amount and unit as text and is marked `NeedsReview`.
5. A text entry becomes one item marked `NeedsReview`.

Lists have full CRUD: create, rename, delete, add item, update item, delete item, toggle, clear checked. Household members with `CanEditShoppingList` may write to a household list.

### Diary link

`FoodDiaryEntry.PlanEntryId` (nullable) records that a diary row came from a plan entry. Adherence for a day is the share of entries with a diary row. Logging a plan entry uses the existing diary commands. A household plan may log a household recipe.

### Trainer drafts

`TrainerClientRelationship` gets `CanPlanMeals`. The client controls it like the other grants. A coach with the grant creates a plan for the client with `Status = Draft` and `AuthoredByUserId` set. The client gets a notification, opens the plan, and accepts or declines. Accepting makes it `Active` and archives the current active plan of the client. The coach can edit a plan only while it is a draft.

### Templates

A template is a plan with `Kind = Template`. Administrators manage global templates under `/admin/meal-plans`. A coach manages their own under `/trainer/templates`. `ApplyTemplateCommand(templateId, startDate, householdId?)` copies entries, prep sessions and rules into a new personal plan. Global templates are visible to everyone. Templates of a coach are visible to the coach and, when applied, to the client.

### API (step 2, as built)

Request and response records live in `Mizan.Contracts/Planning`. Enumerations travel as names (`Kind`, `Status`, entry `Kind`, `MacroMode`, `PrepStyle`); slots are the diary's `MealTypes`.

| Route | Command or query |
| --- | --- |
| `GET /api/MealPlans?status=&page=&pageSize=` | `GetMealPlansQuery`: owned plans, household plans, drafts the viewer authored. Templates are not listed here. |
| `GET /api/MealPlans/day?date=` | `GetPlanDayQuery`: the active plan's day, today by default. `404 no_active_plan` when none covers the date. |
| `GET /api/MealPlans/templates`, `POST`, `PUT|DELETE /templates/{id}`, `POST /templates/{id}/apply` | `GetTemplatesQuery`, `CreateTemplateCommand`, `UpdateTemplateCommand`, `DeleteTemplateCommand`, `ApplyTemplateCommand`. |
| `GET|PUT|DELETE /api/MealPlans/{id}`, `POST /api/MealPlans` | `GetMealPlanByIdQuery`, `UpdateMealPlanCommand`, `DeleteMealPlanCommand`, `CreateMealPlanCommand`. |
| `POST /api/MealPlans/{id}/status`, `/accept`, `/decline` | `SetMealPlanStatusCommand`, `AcceptPlanCommand`, `DeclinePlanCommand`. |
| `POST /api/MealPlans/{id}/entries`, `PUT|DELETE /entries/{entryId}`, `POST /entries/{entryId}/log` | `AddPlanEntryCommand`, `UpdatePlanEntryCommand`, `RemovePlanEntryCommand`, `LogPlanEntryCommand`. |
| `POST /api/MealPlans/{id}/prep-sessions`, `PUT|DELETE /prep-sessions/{sessionId}` | `AddPrepSessionCommand`, `UpdatePrepSessionCommand`, `RemovePrepSessionCommand`. |
| `POST /api/MealPlans/{id}/shopping-list` | `GenerateShoppingListCommand`, creating a list or replacing one's items. |
| `POST /api/Trainers/clients/{clientId}/meal-plans` | `CreatePlanForClientCommand`: a draft for a client who granted `canPlanMeals`. |
| `GET /api/ShoppingLists`, `GET|PUT|DELETE /{id}`, `POST`, `POST /{id}/items`, `PUT|DELETE /items/{itemId}`, `PATCH /items/{itemId}/toggle`, `POST /{id}/clear-checked` | The shopping list commands and queries in `Application/Planning`. |

Where the implementation settled details the design left open:

- Entries on a create or a template carry `prepSessionIndex` into the request's `prepSessions`; on add or update they carry `prepSessionId`. Templates reuse the entry and prep session routes with the template's id.
- A plan's owner and the members of its household edit it. A coach edits a draft only while it is a draft, and the client cannot change a draft until accepting it. `canEdit` on the plan says which applies to the viewer.
- Creating a plan leaves other plans alone. Setting a plan active and accepting a draft archive the owner's other active personal plan; household plans are never archived that way. Today's plan prefers the viewer's own plan over a household plan, then the most recently changed.
- `logged` is the viewer's own diary: a row with the entry's `PlanEntryId` on the entry's date. Logging a day more than a day away from today fills the diary but does not move the streak.
- A household plan may log a recipe shared in that household; a coach's draft may log the coach's own recipe.
- Cost fields (`cost`, `weeklyCost`, `totalCost`) are present and null until the prices step.
- Only administrators and coaches keep templates; a global one needs the admin role.
- `PlanStatus.Active` is the enumeration's first member because it is the column's database default; a value equal to the CLR default is left out of an insert.

## Prices

### Data (API owns the schema)

| Table | Fields |
| --- | --- |
| `price_regions` | `Code` (`ET-AA` for Addis Ababa), `Name`, `Currency`, `Enabled`. |
| `price_sources` | `Name`, `Kind` (`Manual`, `Scraper`, `Report`), `Url`, `RegionCode`, `Enabled`, `Schedule` (cron), `LastRunAt`, `LastStatus`, `LastError`. |
| `price_source_aliases` | `SourceId`, `ProductName` (as the source writes it), `FoodId`, `Quantity`, `Unit`. Administrators curate these. A scraped product without an alias lands in a review queue. |
| `food_prices` | `FoodId`, `RegionCode`, `SourceId`, `Price`, `Quantity`, `Unit` (`g`, `kg`, `ml`, `l`, `piece`), `ObservedAt`, `Confidence` (`Observed`, `Reported`, `Estimated`), `ReportedByUserId`. |
| `price_reports` | `FoodId`, `RegionCode`, `SuggestedPrice`, `Quantity`, `Unit`, `Note`, `Status` (`Open`, `Accepted`, `Rejected`), `UserId`, `ResolvedByUserId`. |

`UserProfile` gets `PriceRegionCode` (default `ET-AA`). Currency comes from the region.

`PriceBook` in Domain picks the price for a food and region. It takes the newest `Observed` price, else the newest `Reported`, else the newest `Estimated`. It returns price per gram, using `Food.ServingSize` for `piece`. `PlanCostCalculator` multiplies the aggregated grams by price per gram, sums per day and per week, and lists unpriced items. The total is shown with its confidence and the share of items that are priced.

### Service

`prices/` at the repository root holds `Mizan.Prices`, a .NET worker like `Mizan.Telegram`. It has no database access. It calls the API with the service key:

- `GET /api/internal/prices/sources` for enabled scraper sources and their aliases.
- `POST /api/internal/prices/observations` with a batch of `{ sourceId, productName, price, quantity, unit, observedAt, url }`. The API resolves aliases, writes `food_prices`, and queues unknown product names for review.
- `POST /api/internal/prices/sources/{id}/run` to record a run result.

Redis holds the schedule lock and the per-source dedupe key. Two worker instances then never scrape the same source in the same window. Observations are durable only once the API stores them. Each scraper implements `IPriceScraper` with `SourceKey`, `CanHandle(url)`, and `ScrapeAsync`. Scrapers read `robots.txt` and send one request per second per host. Their `User-Agent` names Mizan and a contact address.

First targets, Addis Ababa, found in October 2026. Check the terms of each site before you enable it.

| Source | What it has | Note |
| --- | --- | --- |
| asbeza.net | Online grocery with shelf prices from several Addis Ababa stores | Best first target. HTML product pages. |
| aradamart.net | Online grocery with product pages and prices | Second HTML target. |
| numbeo.com food prices for Addis Ababa | Crowd averages for about 20 staples | Mark as `Estimated`. Good for a gap fill. |
| ecx.com.et | Ethiopia Commodity Exchange, grain prices | Wholesale, per quintal. Useful for teff and beans. |
| beU, Zmall | App only | No HTML to scrape. Manual prices only. |

Administrators manage regions, sources, aliases, the review queue, and manual prices under `/admin/prices`. A user reports a wrong price from the food page. The report creates a notification for administrators. Accepting a report writes a `Reported` price.

### Where prices show

- The food page (`/ingredients/[id]`) shows the current price for the region of the user, with its source and date. It has the **Report a wrong price** action.
- The plan page shows cost per day and per week against `WeeklyBudget`.
- The shopping list shows a cost per item and a total, with unpriced items listed.

## AI planning

`GenerateMealPlanCommand` is one metered command. Inputs: rules, `DurationDays`, `StartDate`, a free-text preference, and an optional list of budget ceilings. Steps:

1. Build the context through `IDataAccessPolicy`. It holds the goal, the recipes of the user, the catalogue foods that match the allow list or the preference text, and the price book.
2. Call the provider with the `meal_plan_v1` schema. The schema has days, entries, and prep sessions. An entry has slot, kind, food name or recipe id, grams or servings, and note.
3. Resolve food names to catalogue foods by exact name, then by search. An unresolved name becomes a text entry marked for review.
4. Run `PlanRuleCheck` and `PlanCostCalculator`.
5. Save the plan as `Draft`. Return the plan, the violations, and the cost table. One draft per budget ceiling.

The person confirms a draft to make it `Active`. Chat turns edit a draft through the entry commands. The model never writes nutrition or prices into the plan. Code computes both.

Quota: a `Planning` allowance line, separate from personal chat, so a Free account gets one plan a day. See the metering table in [AI](AI.md).

Typed cards: `AiChatTurnDto.Cards[]` carries `{ type, data }` with `type` in `plan_preview`, `day_plan`, `cost_table`, `shopping_list`. The API validates each card against its schema. The web renders a fixed component per type. Telegram renders text and a keyboard. The MCP App `meal-plan` renders the same data. No model-written markup is rendered anywhere.

New chat tools, all through existing commands: `generate_meal_plan`, `get_plan_day`, `add_plan_entry`, `update_plan_entry`, `remove_plan_entry`, `generate_shopping_list`. Planning tools use `DataAxis.Planning`. Consent for planning follows the nutrition write flag. MCP grants map `DataAxis.Planning` to `planning:read` and `planning:write`.

## Surfaces

### Web

Pages follow [DESIGN](../DESIGN.md): `log-page`, `facts-strip`, `ledger-row`, one empty-state sentence, one filled action.

| Route | Content |
| --- | --- |
| `/meal-plan` | Active plan first, then drafts and past plans. A draft from a coach shows **Accept** and **Decline**. |
| `/meal-plan/[id]` | Day tabs, entries per slot, nutrition per day against targets, violations, cost. **Log** on each entry. **Make shopping list**. |
| `/meal-plan/[id]/edit` | Rules, entries, prep sessions. |
| `/meal-plan/new` | From a template, from the assistant, or empty. |
| `/meal-plan/shopping-list` and `/shopping-lists/[id]` | Items by category or prep session, add, edit, delete, toggle, clear checked, cost. |
| `/today` | The plan entries for today with **Log**, when an active plan covers today. |
| `/admin/meal-plans`, `/admin/prices` | Templates. Regions, sources, aliases, review queue, manual prices. |
| `/trainer/clients/[id]/plan`, `/trainer/templates` | Draft for a client. Templates of the coach. |

All API calls go through `lib/api/*` modules typed from `types/api.generated.ts`. Pages do not declare their own response types.

### MCP

Tools: `list_meal_plans`, `get_meal_plan`, `get_plan_day`, `create_meal_plan`, `update_meal_plan`, `delete_meal_plan`, `add_plan_entry`, `update_plan_entry`, `remove_plan_entry`, `generate_shopping_list`, `log_plan_entry`, `list_templates`, `apply_template`, `generate_meal_plan`, and the shopping list CRUD. Entry tools take the entry id and say so. Admin tools manage templates and prices. Trainer tools draft for a client under `trainer` scope.

The MCP App `meal-plan` renders `get_meal_plan` and `get_plan_day`. It calls `log_plan_entry` and `generate_shopping_list` through the host. The prompt `plan_my_week` builds a plan through `create_meal_plan` and `add_plan_entry` (and `generate_meal_plan` from step 4); `grocery_run` calls `generate_shopping_list`. Neither asks the model to aggregate.

### Telegram

| Input | Behavior |
| --- | --- |
| `/plan` or `/plan tomorrow` | The entries of the day with one **Log** button per entry (`pe:{entryId}`). Violations and the eating window at the end. |
| `/shop` | The newest list, ten items per page, one toggle button per item (`sh:{itemId}`), **Open** button to the Mini App. |
| Evening reminder | An outbox job at `EatingWindowEnd` minus 30 minutes: "Your window closes at 20:00. Today: 1,640 of 1,800 kcal." Push and Telegram. |

Callback data stays under 64 bytes because it carries ids only.

### Telegram Mini App

The Mini App is the web app. Next.js serves `/tg/plan` and `/tg/shopping-list`, which render the same components as `/meal-plan/[id]` and the shopping list without the shell. On open, the page sends `initData` to `POST /telegram/miniapp/session` on the bot service. The bot service verifies the HMAC with the bot token and checks that `auth_date` is within five minutes. It then calls `POST /api/Telegram/sessions` with the service key for the linked user. The API returns an ordinary session, and the page receives the `mizan_session` cookie. A chat that is not linked gets the link page. The `/plan` and `/shop` replies carry a `web_app` button.

## Tests

- One integration test per user story, through the API, against PostgreSQL. Name it for the story: `AClientAcceptsACoachsDraft_AndItBecomesTheActivePlan`.
- Domain rules (`PlanRuleCheck`, `ShoppingAggregator`, `PriceBook`, `PlanCostCalculator`) get unit tests with tables of cases.
- MCP tests cover scope gating and one call per tool through the stubbed backend. Telegram tests feed updates to the handler through the fake HTTP world.
- The Playwright fixture API is typed from `types/api.generated.ts` and serves plans, lists, and prices. Every meal plan test asserts that the fixture saw no unhandled call.
- A test lists every `/api/...` path the frontend calls and checks each against the OpenAPI document.
- Do not add a test that restates an implementation. Delete a test when its behavior is covered by a story test.

## Steps

1. **Cleanup.** One branch migration. Fix the defects listed in the pull request. Fail-safe audit redaction and the impersonation guard. Server-side scaling of AI proposals with `Idempotency-Key`. One copy of each badge. Remove the unused planning code. CI gains type check, format checks, OpenAPI drift check, and a pinned Bun.
2. **Planning core.** Entries, rules, prep sessions, today, log from plan, list generation and CRUD, templates, trainer drafts. Web, MCP, MCP App, Telegram `/plan` and `/shop`.
3. **Prices.** Tables and admin pages. `Mizan.Prices` with the asbeza and Arada Mart scrapers and the numbeo gap fill. Reports, the food page, and costing on plans and lists.
4. **AI planning.** `GenerateMealPlanCommand`, drafts, budget ceilings, typed cards, planning quota line, chat tools, MCP and Telegram wiring.
5. **Mini App.** `/tg` routes, session exchange, `web_app` buttons, evening reminder.

## Deferred

- **Decision models** (TypeSafe Jev, Microsoft-Decision-1). They return calibrated probabilities for typed questions and could route Telegram free text, score plan adherence, or tag templates. Not now. Each job is deterministic code or a small structured-output call. A second provider also needs its own metered path. Revisit when Telegram routing cost or template tagging volume justifies it.
- **Generative UI** (Thesys OpenUI). Not now. The ledger design is fixed and the planning screens have a known shape. The MCP Apps are plain HTML. Typed cards rendered by fixed components give the same result without model-written markup.
