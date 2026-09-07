# MCP interface

The MCP service exposes the same logs, recipes, consent, and account capabilities as the web app through HTTP at `/mcp`. The API remains the authority for ownership, roles, nutrition calculations, consent, and entitlements.

## Connect

1. Sign in to Mizan and open **More → MCP Tokens** (`/profile/mcp`).
2. Create a named token and copy it when shown. Store it in your MCP client's secret configuration.
3. Configure an HTTP MCP connection to `https://<your-app-host>/mcp` with the header `Authorization: Bearer <your-token>`. Local Compose uses `http://localhost:5001/mcp`.
4. Ask the client to list tools, then try `get_food_diary` with a date.

The public token authenticates as its owner. `X-Api-Key` and `X-Impersonate-User` are internal service headers, not the public client setup. Query-string tokens are accepted for compatibility, but the authorization header avoids putting credentials in URLs. Revoke a token from the same web screen when it is no longer needed. Unlimited token validations may be cached by MCP for up to 60 seconds.

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

The selected meal must contain at least two items. If the API requests missing weights, ask the user and supply `recipeYieldsJson` or `entryWeightsGramsJson`; do not infer a batch yield. `promote_recipe_to_preparation` turns an owned recipe into a reusable ingredient using its finished batch weight. Recipe creation starts from logged entries; `update_recipe` maintains an existing recipe.

## Discover tools and permissions

Use MCP `tools/list` for complete argument schemas and the list registered by the running version. The source catalogue is [`backend/Mizan.Mcp.Server/Tools`](../backend/Mizan.Mcp.Server/Tools).

Administrator tools can be listed for everyone; the API rejects unauthorized execution. Listing a tool does not grant access. AI tools also consume the caller's applicable AI quota and enforce their consent. Trainer AI calls cannot access axes the client withheld. See [AI](AI.md).

`upload_image` and `analyze_food_image` accept base64 image bytes, capped at 4 MB decoded. Food-photo analysis produces a proposal for confirmation before logging.

## Operate and verify

Compose starts `mcp` with the backend URL and internal service keys. `/health` reports transport availability. A successful health check alone does not prove token validation, permissions, or a write path; integration tests cover those separately.

The API records tool usage by token and user. Limits and current usage are visible in the token screen. A failed write should be checked in the log before blindly resubmitting, especially when a connection drops after the API may have committed it.
