# MCP Server Hardening & Best-Practice Handoff

**Date:** 2026-07-09. Produced from a file-by-file review of `backend/Mizan.Mcp.Server` (v2.0.0), the tool-backing API endpoints, the Paddle entitlement layer, and the MCP test suites, cross-referenced against `01-security-remediation.md`, `02-current-state-audit.md`, and `06-test-baseline.md`.

**No implementation has been done.** This file is a complete handoff spec for the implementing agent. Scope: the MCP server itself, its error/gating surface, and the backend seams it depends on. The workout/exercise tool rebuild is intentionally coordinated with (not duplicated from) `04-feature-mapping.md` / `05-implementation-roadmap.md`.

---

## 1. Current architecture (verified working)

```
MCP client (Claude, etc.)
  │ Bearer <mcp token>  (also accepted via ?token= query param — see F8)
  ▼
Mizan.Mcp.Server  (ASP.NET, MapMcp("/mcp"), stateful HTTP transport, 30min idle)
  │ McpTokenAuthenticationHandler → POST /api/McpTokens/validate (backend, per request, uncached)
  │ CallToolFilter: auth check → ActivitySource span → tool call → LogUsageAsync
  ▼
BackendApiClient (typed HttpClient)
  │ X-Api-Key: <Mcp:ServiceApiKey>  +  X-Impersonate-User: <userId>
  ▼
Mizan.Api  (ApiKeyAuthenticationHandler mints principal with sub + real role claims)
```

- 14 tool classes, ~45 tools, registered in `Program.cs:124-137`.
- Telemetry is solid: Serilog, OTLP traces, Prometheus metrics (`mcp.tool_calls.count`/`.duration`), per-call usage rows via `POST /api/McpTokens/usage`.
- **Pro gating already flows through**: the impersonated principal carries `sub`, so `ProAuthorizationHandler` (`Mizan.Api/Authorization/ProRequirement.cs`) and command-level `IEntitlementService` checks fire for MCP calls exactly as for browser calls. Nothing bypasses billing. The problem is entirely in how the resulting errors surface (F4) and that no tool advertises its gate (F5).

## 2. Findings

Ordered by priority. Each has evidence, a fix spec, and tests (consolidated testing strategy in §4).

### F1 — Duplicate tool names shadow an entire tool class (P0, correctness)

`NutritionTools` registers `get_daily_nutrition` and `log_food`; `MealTools` registers the same two names. `MealTools` is registered first (`Program.cs:126` vs `:127`) and wins — the integration test at `McpIntegrationTests.cs:541-580` asserts the *MealTools* response shape (`totalCalories` etc. computed from `/api/Meals/range`), confirming `NutritionTools` is dead code in practice. Consequences: `/api/Nutrition/daily` (the richer summary that includes goal progress) is unreachable via MCP, and the shadowing is an undocumented SDK-behavior dependency that can silently flip on a package bump.

**Fix:**
1. Delete `NutritionTools.LogFood` (raw-JSON string body, inferior to the typed `MealTools.LogFood`).
2. Rename `NutritionTools.GetDailyNutrition` → tool name `get_nutrition_summary`, description "Daily totals plus goal progress", keep it pointing at `/api/Nutrition/daily`. Or fold it into `MealTools` and delete the class; either way exactly one registration per name.
3. Add a startup assertion (or unit test) that enumerates registered tool names and fails on duplicates.

### F2 — MCP collapses meal types, silently breaking habit tracking (P0, correctness)

Commit `c5cbdb9` (2026-06-13) widened the canonical vocabulary to BREAKFAST/LUNCH/DINNER/SNACK/DRINK (+legacy MEAL) in `Mizan.Domain/Constants/MealTypes.cs` *specifically so the habits check works* — but did not touch the MCP. `MealTools.NormalizeMealType` (`MealTools.cs:148-157`) still maps `BREAKFAST|LUNCH|DINNER → MEAL`. Every meal logged through MCP loses its meal-type identity and won't satisfy breakfast/lunch/dinner habit checks. Same issue in the tool descriptions ("Meal category: MEAL, SNACK, or DRINK") which actively teach the client the wrong vocabulary.

**Fix:**
1. `NormalizeMealType` passes through the full canonical set (uppercase), accepts `BEVERAGE→DRINK` as a courtesy alias, keeps `MEAL` as legacy-accepted, throws on anything else listing valid values.
2. Update the `[Description]` on `log_food`, `log_meal`, `log_meal_manual` to `BREAKFAST, LUNCH, DINNER, SNACK, or DRINK`.
3. `MealPlanTools.AddRecipe`/`UpdateRecipe` already use lowercase breakfast/lunch/dinner/snack — verify against `MealPlansController` expectations and align casing handling with `MealTypes.Normalize`.

### F3 — Raw-JSON string parameters defeat the point of MCP (P1, best practice)

Four tools take an opaque `string body` and deserialize blind: `WorkoutTools.LogWorkout` (`WorkoutTools.cs:17`), `ExerciseTools.CreateExercise` (`ExerciseTools.cs:34`), `ProfileTools.UpdateMyProfile` (`ProfileTools.cs:25`), `NutritionTools.LogFood` (deleted by F1). No input schema is exposed to the client, no validation happens before the backend round-trip, and the description ("JSON body matching the LogWorkoutCommand schema") references a schema the client cannot see.

**Fix:** typed parameters mirroring the backing command DTOs, same pattern as `MealTools.LogFood` / `BodyMeasurementTools.LogMeasurement`:
- `log_workout`: `name`, `workoutDate` (YYYY-MM-DD), `durationMinutes`, `notes?`, plus `exercisesJson` for the nested set array *with a fully documented per-set schema* (`exerciseId`, `sets: [{setNumber, reps?, weightKg?, durationSeconds?, distanceMeters?}]`). Nested arrays are the one legitimate JSON-string case; the schema must be in the description. **Coordinate with doc 04:** the LiftLog rebuild reshapes this command (per-set logging, sessions); do the typed signature as part of that phase, not before, or you'll do it twice. Until then, minimum viable fix: document the exact current `LogWorkoutCommand` shape in the description and validate `exercises` is non-empty client-side (backend G3: empty-exercise workouts farm achievements).
- `create_exercise`: `name`, `category`, `muscleGroup?`, `equipment?`, `description?`.
- `update_my_profile`: enumerate the actually-updatable fields from `UpdateUserCommand` (read it first) instead of "JSON body with profile fields".

### F4 — Entitlement errors surface as raw HTTP noise (P1, the pro-gating ask)

`BackendApiClient.SendAsync` (`BackendApiClient.cs:71-75`) throws `InvalidOperationException($"API error {status}: {content}")`; the call-tool filter returns `ex.Message` verbatim. A free user hitting a gate gets:

```
API error 403: {"type":"https://tools.ietf.org/...","title":"Forbidden","status":403,"detail":"Free plan is limited to 1 meal plan. Upgrade to Pro for unlimited meal plans."}
```

The useful sentence is buried in ProblemDetails JSON. Agents retry, misreport, or give up.

**Inventory of gates that MCP tools can hit today** (all verified in code):

| Tool | Gate | Source |
|---|---|---|
| `get_goal_progress` | Pro-only endpoint | `GoalsController.cs:55-56` `[Authorize(Policy = "RequirePro")]` |
| `create_meal_plan` | Free tier: 1 plan max | `CreateMealPlanCommand.cs:79-84` |
| `create_shopping_list` | Free tier: 1 list max | `CreateShoppingListCommand.cs:27-32` |
| `invite_to_household` | Pro-only + 6-member cap | `InviteHouseholdMemberCommand.cs:61-72` |
| `create_food`, `update_food`, `delete_food` | Admin role | `FoodsController.cs:40,48,63` |

AI endpoints (`/api/Nutrition/ai/*`, Pro-gated) are not exposed as MCP tools — see §3 open decisions.

**Fix:**
1. In `BackendApiClient.SendAsync`, parse the error body as ProblemDetails (`title`, `detail`, `errors` dictionary for 400 validation). Throw a new `BackendApiException(status, title, detail, errors)` instead of `InvalidOperationException`.
2. Map in one place (the exception → tool-result path):
   - 401 → "MCP token is no longer valid. Create a new token in Profile → MCP."
   - 403 where detail mentions the plan/upgrade → prefix `[UPGRADE REQUIRED]` + the backend's own detail sentence + "Manage plan: https://mizan.euaell.me/billing". The backend messages are already user-quality; reuse them, don't rewrite them.
   - 403 otherwise → the detail (admin/ownership denials).
   - 404 → "Not found: <detail>" (agents need this to stop retrying fabricated IDs — same failure class as the AI-suggestions audit finding).
   - 400 → flatten the validation `errors` dictionary to `field: message` lines.
   - 429 → pass through with retry hint.
3. Optional backend assist (small, coordinated change): add `"errorCode": "upgrade_required"` to ProblemDetails `extensions` in the `ForbiddenAccessException` branch of the exception handler (`Mizan.Api/Program.cs:296`), so the MCP match is exact instead of string-sniffing. Do it if touching the handler anyway; the string match is acceptable v1.

### F5 — Tools don't advertise their gates or roles (P1, pairs with F4)

`create_food` says "requires admin role" — good. Nothing else declares anything. Agents discover gates by failing.

**Fix:**
1. Append to each gated tool's `[Description]`: `get_goal_progress` "(Pro feature)"; `create_meal_plan` "(free plan: 1 meal plan; Pro: unlimited)"; `create_shopping_list` same pattern; `invite_to_household` "(Pro feature; households cap at 6 members)".
2. New read-only tool `get_my_subscription` → `GET /api/Subscriptions/me` (`SubscriptionsController.cs:20`, exists, user-scoped). Lets agents check the plan before attempting gated writes. Annotate `ReadOnly = true, Idempotent = true`.
3. Add server-level `instructions` to `AddMcpServer` options (MCP initialize supports it): one paragraph covering "search before create; dates are YYYY-MM-DD; meal types are BREAKFAST/LUNCH/DINNER/SNACK/DRINK; some tools are Pro-gated — check get_my_subscription".

### F6 — Missing tools / API parity gaps (P2)

| Gap | Backing endpoint | Action |
|---|---|---|
| No `list_workouts` — `WorkoutTools` has only `log_workout`; users cannot ask "what did I train this week" | `GET /api/Workouts` exists (`WorkoutsController.cs:25`) | Add now; typed paging params like `list_exercises` |
| No workout get-by-id/update/delete | Endpoints don't exist (audit G1) | Blocked on backend; ships with LiftLog Phase (doc 04). Add MCP tools in the same PR as the endpoints |
| No shopping-list delete/item-delete/item-update | Endpoints don't exist (`ShoppingListsController` has GET/POST/toggle only) | Blocked; shopping-list repair is a DEEP_DIVE sprint. Same-PR rule applies |
| No exercise get-by-id/update/delete | Endpoints don't exist (`ExercisesController` has GET/POST only) | Same as above; exercise catalog is doc 04 Phase 2 |
| No `get_my_trainer` / trainer-request tools exist but no chat access | By design for now | Leave; chat via MCP is a product decision |

The rule to carry forward: **every new controller endpoint added by the LiftLog phases gets its MCP tool in the same PR**, with the F4 error mapping and F5 description conventions applied from day one.

### F7 — Per-request token validation round-trip (P2, performance)

`McpTokenAuthenticationHandler` calls `POST /api/McpTokens/validate` on every HTTP request (`McpTokenAuthenticationHandler.cs:48`). Under an agent burst (10 tool calls in a session) that's 10 synchronous backend+DB round-trips for the same token.

**Fix:** in-memory `IMemoryCache` keyed by SHA-256 of the token, TTL 60s, caching only *successful* validations. 60s bounds the revocation delay to something operationally acceptable (tokens are revoked from the profile UI; document the delay there). Do NOT cache failures (avoids wedging a just-created token). Note: S7 (`01-security-remediation.md`) adds IP rate limiting to this same endpoint — the cache also protects the MCP server from that limiter under legitimate load.

### F8 — Security hygiene inside the MCP server (P2; the big items are in doc 01)

Doc 01 owns R1 (raw MCP tokens in audit log — CRITICAL, rotate all tokens after fix), R2 (service-key impersonation scoping), S7 (validate-endpoint rate limit). Do not duplicate that work. MCP-server-local items not covered there:

1. `McpTokenAuthenticationHandler.cs:52` logs a 10-char token prefix on validation failure. Tokens are high-entropy bearer credentials; log the SHA-256 prefix instead, never raw material.
2. Query-string token acceptance (`ExtractToken`, `?token=`) puts credentials in access logs, proxies, and browser history. Current MCP spec guidance is Authorization-header-only. Check whether any real client depends on it (SSE reconnects from browsers historically did); if none, remove; if kept, ensure request logging (`RequestResponseLoggingMiddleware`, Serilog request logging) redacts the `token` query key.
3. `LogUsageAsync` sends `Parameters = null` always — correct post-R1 posture (parameters can contain user content). Keep null; if debugging demands it, log parameter *names* only.

### F9 — Protocol polish (P3, cheap wins)

- Annotations audit: `log_workout`, `create_exercise`, `create_recipe`, `create_*` lack `Idempotent = false`/`Destructive` where applicable; `export_profile` is `ReadOnly` but heavy — note "large response" in description. `delete_*` tools already carry `Destructive = true` — keep the convention.
- `get_food_diary` description doesn't say the date defaults to *UTC* today — the local-date bug family (audit D6/E5) applies to MCP too. When the backend `ClientDate` fix lands (doc 02 cross-cutting table), thread an optional `timezone`/local-date param through `get_food_diary`, `get_daily_nutrition`, `log_food`, `log_meal`.
- Version string: bump `ServerInfo.Version`/health endpoint together (currently hardcoded "2.0.0" in three places in `Program.cs`) — single constant.

## 3. Open decisions for the user (do not decide unilaterally)

1. **Expose AI tools via MCP?** `/api/Nutrition/ai/chat` and `ai/analyze-image` are Pro-gated and functional. An `ask_nutrition_ai` tool is trivially addable but doubles AI cost surface (agent calling an AI). Recommendation: skip; MCP clients are already LLMs.
2. **Query-string token removal** (F8.2) — needs confirmation no deployed client uses it.
3. **Backend `errorCode` extension** (F4.3) — coordinate with whoever owns the exception handler; it's a 5-line change but touches every API consumer's error contract.

## 4. Testing strategy

Baseline (from `06-test-baseline.md`): 219 backend tests, 217 green; the 2 reds are stale meal-type tests unrelated to this work — fix them first (they're 5 minutes and touch the same validator this work touches). MCP-specific suites: `McpIntegrationTests` (47), `McpAuthenticationTests` (19), `McpServerTests` (2), `McpSystemTests` (1), `McpTokensControllerTests` (2). All run via `docker-compose --profile test up test` against `mizan_test`.

### Per-finding tests (write in the same PR as the fix)

| Finding | Tests |
|---|---|
| F1 | `tools/list` returns no duplicate names (assert `toolNames.Distinct().Count() == toolNames.Count` — add to the existing list test at `McpIntegrationTests.cs:240`); `get_nutrition_summary` returns the `/api/Nutrition/daily` shape incl. goal fields |
| F2 | `log_food` with `mealType: "BREAKFAST"` → follow-up `get_food_diary` shows `BREAKFAST`, not `MEAL`; invalid type → error listing the 5 valid values; regression: `MEAL` still accepted |
| F3 | `log_workout` with typed params round-trips; empty `exercises` rejected client-side with a readable message (not a backend 500/false achievement tick); malformed nested set JSON → readable error naming the bad field |
| F4 | The critical suite. Seed helper `SeedSubscriptionAsync(userId, plan)` writing a `subscriptions` row (mirror what the Paddle webhook writes; see `6df09c0` for how billing-gated tests were fixed before). Free user: 2nd `create_meal_plan` → `isError: true`, text starts `[UPGRADE REQUIRED]`, contains the backend sentence, contains billing URL, contains NO raw JSON braces. Pro user: same call succeeds. Repeat pattern for `create_shopping_list`, `invite_to_household`, `get_goal_progress`. 404 mapping: `get_recipe` with random UUID → "Not found", no stack noise. 400 mapping: `create_recipe` with empty title → flattened field message |
| F5 | `get_my_subscription` returns plan for free and pro seeds; `tools/list` descriptions of the 4 gated tools contain "Pro" |
| F6 | `list_workouts` returns seeded workouts paginated |
| F7 | Unit-test the cache: two requests, one backend validate call (mock `IBackendApiClient`); revoked token still accepted within TTL then rejected after (document, don't fight, the TTL); failed validation NOT cached |
| F8 | Log-capture test: failed auth writes no raw token substring to the log sink |

### Contract & regression

- **Contract check:** for every typed tool, an integration test that calls it with all parameters populated and asserts backend 2xx — this is the cheap guard against DTO drift (the codegen equivalent for MCP; there is no OpenAPI safety net on this path).
- **Full-suite gate:** all 219+ tests green in Docker before merge; the intentional `Tool failed:` log noise from error-path tests stays (documented in 06).
- **Manual smoke (30 min):** connect a real MCP client (Claude desktop) against local compose with a free-tier token: log a breakfast, hit the meal-plan quota, read the upgrade message, check `get_my_subscription`, list workouts. Then repeat the quota step with a pro-seeded user. Script the checklist into the PR description.

### Suggested order

1. Fix the 2 stale meal-type tests (baseline to green).
2. F1 + F2 (small, correctness, same files) — one PR.
3. F4 + F5 (the gating surface) — one PR; this is the bulk of the value.
4. F3 minimal (`update_my_profile`, `create_exercise` typed; `log_workout` documented-only pending doc 04) + F6 `list_workouts` — one PR.
5. F7 + F8 + F9 — one PR.
6. Everything else in F3/F6 rides the LiftLog phase PRs per the same-PR rule.

Estimated effort: 2-3 days for steps 1-5 including tests, assuming no backend `errorCode` coordination stall (fall back to string matching if it stalls).
