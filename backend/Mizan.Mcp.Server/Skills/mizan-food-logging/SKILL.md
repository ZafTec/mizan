---
name: mizan-food-logging
description: Log what the user ate in Mizan accurately. Use when the user says what they ate or drank, sends a meal photo, wants a day logged in one go, or wants a logged entry fixed or removed.
---

# Logging food in Mizan

Mizan keeps a food diary with real nutrition for each food. The goal is a diary the person can trust, so the numbers must come from the catalogue or from the person, never from a guess.

## One food

1. Call `search_foods` with the name the person used.
2. Pick the match that fits. If two foods fit equally (for example whole and skimmed milk), ask which one.
3. Work out `servings` from the amount eaten. **`servings` is a count of the food's own serving size, not grams.** Read `servingSize` and `servingUnit` in the search result and convert. See [serving counts](references/serving-counts.md).
4. Call `log_food` with `foodId`, `date`, `mealType` (BREAKFAST, LUNCH, DINNER or SNACK) and `servings`.
5. Call `get_food_diary` for the date if the person wants to see the result.

## A recipe

Use `search_recipes`, then `log_meal` with the recipe id and recipe servings.

## Something that is not in the catalogue

Use `log_meal_manual` only when the person gives you the nutrition, or when no catalogue food is close. Never invent calories or macros. If you do not have the numbers, ask.

## A photo

`analyze_food_image` returns an estimate to confirm. It logs nothing. Show the person what it found, let them correct amounts or items, then log the confirmed items with `log_food` or `log_meal_manual`. It uses the person's daily assistant allowance, so call it once per photo.

## Several things at once

`log_day` takes one date with meals, workout sets and a weight. It is **not one database transaction**. It returns a receipt of what completed, what failed and what remains.

- Retry only the items that remain.
- A write marked as possibly saved (`mayHaveBeenSaved`) is unconfirmed. Check `get_food_diary` before repeating it, or you will log it twice.

## Fixing a mistake

Find the entry with `get_food_diary` and call `delete_meal` with its id, then log the correct one. Say what you removed.

## Do not

- Guess servings, grams, calories or macros.
- Log without a date when the person said "yesterday" or a weekday. Work out the actual date.
- Call `search_foods` once and then reuse an old id for a different food.
