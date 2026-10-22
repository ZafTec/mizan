---
name: mizan-meal-planning
description: Plan meals and shopping in Mizan. Use when the user wants a meal plan for a week, wants recipes or foods scheduled on days, wants to log a planned meal, needs a shopping list, or shares plans with a household.
---

# Meal plans and shopping lists

A plan runs from a start date for a number of days. Each day has slots (BREAKFAST, LUNCH, DINNER, SNACK, DRINK, MEAL), and each slot holds entries. An entry is a recipe with servings, a catalogue food with grams, or a text line. The API computes every entry's nutrition, each day's totals and the plan's rule violations. Never add these up yourself.

## Build a plan

1. Look at what the person has: `search_recipes` for their recipes, `search_foods` for catalogue foods, `get_current_goal` for their targets.
2. `create_meal_plan` with a name, a start date and a duration. Pass the targets and anything to respect as rules in `rulesJson` (targetCalories, targetProtein, maxCarbs, weeklyBudget, forbiddenFoodIds, prepStyle Batch with batchDays). Entries and prep sessions can go in `entriesJson` and `prepSessionsJson` at once, or be added afterwards.
3. `add_plan_entry` per slot: the plan id, the day index (0 is the first day), the slot, and the kind. Recipe needs `recipeId` and `servings`; Food needs `foodId` and `grams`; Text needs `text` and carries no nutrition.
4. Show the plan with `get_meal_plan`. Read its per-day totals and its `violations`, and offer swaps for the days that break a rule.

`update_plan_entry` replaces an entry and `remove_plan_entry` removes one. Both take the entry's own id from `get_meal_plan`, not the recipe or food id. `update_meal_plan` changes the name, dates, duration or rules. `delete_meal_plan` removes the plan with everything in it, so confirm first.

A template is a plan without dates: `list_templates`, then `apply_template` with a start date creates a plan from it.

## Follow a plan

- `get_plan_day` shows the active plan's day for a date (today by default). When no active plan covers the date it says so in plain text; that is not an error.
- `log_plan_entry` writes an entry to the diary on its own date and slot. Check `logged` on the entry first: logging twice makes two diary rows.
- `set_meal_plan_status` makes a plan Active or Archived. Activating a personal plan archives the other active one.
- A coach's draft arrives with status Draft. The person decides with `accept_meal_plan` or `decline_meal_plan`; do not accept on their behalf.

## Batch cooking

`add_prep_session` adds a cooking session on a day. Entries that point at it with `prepSessionId` are cooked then, and the shopping list groups their ingredients under the session. `update_prep_session` and `remove_prep_session` change or drop one; the entries stay.

## Shopping lists

1. `generate_shopping_list` turns a plan into a list: ingredients are combined across recipes and converted to grams where possible, grouped by prep session. Pass `replaceListId` to refresh an existing list instead of making a new one.
2. `get_shopping_list` shows the lines. A line marked `needsReview` has an amount that could not be converted; ask the person to check it, then fix it with `update_shopping_list_item` and clear `needsReview`.
3. `toggle_shopping_list_item` marks a line bought or not. `clear_checked_items` removes everything bought. Say which way it went.

A list made by hand works the same: `create_shopping_list`, `add_shopping_list_item` (grams when the weight is known, otherwise amount and unit), `update_shopping_list` to rename, `delete_shopping_list_item` and `delete_shopping_list` to remove.

## Households and coaching

A plan or list can belong to a household so other members see it. This connection can only use the households the person allowed when they connected the app. If a household is not available, say so and offer to save the plan as personal, or tell the person they can allow it in Mizan under More, then Connected apps.

A coach drafts for a client with `create_plan_for_client`. It takes the same arguments as `create_meal_plan` and needs the client to have allowed meal planning; the client is notified and chooses to accept or decline.

Do not try to work around a refusal. The permission is the person's choice.
