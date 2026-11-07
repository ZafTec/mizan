---
name: mizan-food-logging
description: Log what the user ate in Mizan accurately. Use when the user says what they ate or drank, sends a meal photo, wants a day logged in one go, or wants a logged entry fixed or removed.
---

# Logging food in Mizan

Mizan keeps a food diary with real nutrition for each food. The goal is a diary the person can trust, so the numbers must come from the catalogue or from the person, never from a guess.

## Find, then log

1. Call `find_food` (kind `food`) with the name the person used as `query`. For their saved recipes use kind `recipe`.
2. Pick the match that fits. If two foods fit equally (for example whole and skimmed milk), ask which one.
3. Work out `servings` from the amount eaten. **`servings` is a count of the food's own serving size, not grams.** Read `servingSize` and `servingUnit` in the result and convert. See [serving counts](references/serving-counts.md).
4. Call `log_food` with `date`, `mealType` (BREAKFAST, LUNCH, DINNER or SNACK) and `items`. Each item is exactly one of:
   - `foodId` with `servings`,
   - `recipeId` with recipe `servings`,
   - `planEntryId` with its `planId` (an entry of a meal plan, logged with the nutrition the plan computed),
   - `name` with the nutrition the person gave: `calories` and macros, or `amountGrams` with the per-100 g figures.
5. Call `get_nutrition` (view `day`, include `entries`) for the date if the person wants to see the result.

## Several things at once

Send everything eaten in one `log_food` call, one item each, with a `mealType` or `date` on an item when it differs. The items are saved one by one and **not as one database transaction**. The receipt lists what completed, what failed and what remains.

- Retry only the items that remain.
- A failed item marked `mayHaveBeenSaved` is unconfirmed. Check the day with `get_nutrition` (include `entries`) before repeating it, or you will log it twice.

Workouts go to `log_workout` and weight to `log_body`.

## Something that is not in the catalogue

Log it by `name` only when the person gives you the nutrition, or when no catalogue food is close. Never invent calories or macros. If you do not have the numbers, ask.

## A photo or a description

`estimate_food` (kind `photo` with `imageUrl`, or kind `text`) returns an estimate to confirm. It logs nothing and uses the person's AI allowance, so call it once per meal. Show the person what it found, let them correct amounts or items, then log the confirmed items with `log_food`: each food says its source, a recipe (`recipeId`), a catalogue food (`foodId`) or an estimate (log it by `name` with its figures).

## Fixing a mistake

Find the entry with `get_nutrition` (view `day`, include `entries`) and remove it with `delete` (kind `diary_entry`, or `diary_group` for a whole logged recipe) and its id, then log the correct one. Say what you removed.

## Do not

- Guess servings, grams, calories or macros.
- Log without a date when the person said "yesterday" or a weekday. Work out the actual date.
- Search once and then reuse an old id for a different food.
