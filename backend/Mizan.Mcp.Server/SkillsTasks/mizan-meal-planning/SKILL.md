---
name: mizan-meal-planning
description: Plan meals and shopping in Mizan. Use when the user wants a meal plan for a week, wants recipes or foods scheduled on days, wants to log a planned meal, needs a shopping list, or shares plans with a household.
---

# Meal plans and shopping lists

A plan runs from a start date for a number of days. Each day has slots (BREAKFAST, LUNCH, DINNER, SNACK, DRINK, MEAL), and each slot holds entries. An entry is a recipe with servings, a catalogue food with grams, or a text line. Mizan computes every entry's nutrition, each day's totals, the costs and the plan's rule violations. Never add these up yourself.

## Draft a plan

`plan_meals` (action `generate`) drafts a whole plan from a `startDate`, `durationDays`, the person's words as `preference`, any targets, and up to three weekly budgets in `budgetCeilings` (one draft each; `dailyBudget` caps each day). When the person has not said which day the plan starts, ask before calling; never assume today. Mizan chooses foods and recipes and saves each draft with status Draft. Show the person the violations, the weekly cost and the lines left for review. A draft is not their plan until they accept it. It spends the planning allowance: one plan a day on Free.

## Build a plan by hand

1. Look at what the person has: `find_food` (kind `recipe`, then kind `food`) and `get_nutrition` (view `goals`) for their targets.
2. `plan_meals` (action `create`) with a `name`, a `startDate` and `durationDays`. Pass the targets and anything to respect as `rules` (targetCalories, targetProtein, maxCarbs, weeklyBudget, forbiddenFoodIds, prepStyle Batch with batchDays). `entries` and `prepSessions` can go in at once; `templateId` starts from a template (`get_meal_plan` view `templates`).
3. Add or change entries in one `edit_meal_plan` call with `planId` and `changes`: `add_entry` (dayIndex from 0, slot, and `recipeId` with `servings`, `foodId` with `grams`, or `text`), `update_entry` (the entry's own id from `get_meal_plan`, not the recipe or food id), `add_prep` and `update_prep`. The changes are made in order and are not one transaction; the receipt says what remains, so send only that.
4. Show the plan with `get_meal_plan` (view `plan`, `planId`). Read its per-day totals and its `violations`, and offer swaps for the days that break a rule.

`plan_meals` action `update` changes the name, dates, duration or rules; `activate` makes a plan the one the person follows and `archive` puts it away. Remove an entry with `delete` (kind `plan_entry`, with `planId`) and a prep session with kind `prep_session`.

## Follow a plan

- `get_meal_plan` (view `day`) shows the active plan's day for a date, today by default. When no plan covers the date it says so; that is not an error.
- Log an entry with `log_food`, item `{ planEntryId, planId }`. Check `logged` on the entry first: logging twice makes two diary rows.
- A coach's draft arrives with status Draft. The person decides with `plan_meals` action `accept` or `decline`; do not decide on their behalf.

## Shopping lists

1. `update_shopping_list` (action `from_plan`, `planId`) turns a plan into a list: ingredients are combined across recipes, converted to grams where possible, and grouped by prep session. Pass `replaceListId` to refresh an existing list instead of making a new one.
2. `get_shopping_list` (view `list`, `listId`) shows the lines. A line marked `needsReview` has an amount that could not be converted; ask the person to check it, then fix it with action `update_item` and `needsReview` false.
3. Action `check` ticks a line as bought (`isChecked` false unticks it). Say which way it went.

A list made by hand works the same: action `create`, then `add_items` with many items in one call (grams when the weight is known, otherwise amount and unit), `rename` to rename. `delete` removes an item (kind `shopping_item`), a list (`shopping_list`) or everything ticked (`checked_items`).

## Removing a plan

`delete` with kind `meal_plan` removes a plan with everything in it. It first returns a preview (its name, status, counts and the lists made from it) and a token, and removes nothing. Show the preview; only when the person agrees, call `delete` with kind `confirm` and the token.

## Costs

`get_meal_plan` and `get_shopping_list` carry costs in the person's price region: per entry, per day, per week, per item, and the lines that have no price. Quote those numbers; do not compute your own. Say how much of the list is priced and the lowest confidence (Estimated means a rough guess). `find_food` with `foodId` and `includePrice` gives one food's price and source; the regions are in the resource `mizan://reference/price-regions`. When the person says a price is wrong, `save_food` kind `price_report` with what they paid.

## Households

A plan or list can belong to a household so other members see it (`householdId` when creating). This connection can only use the households the person allowed when they connected the app. If a household is not available, say so and offer to save the plan as personal, or tell the person they can allow it in Mizan under More, then Connected apps.

Do not try to work around a refusal. The permission is the person's choice.
