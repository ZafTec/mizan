---
name: mizan-recipes
description: Turn logged meals into reusable recipes in Mizan and manage them. Use when the user wants to save a meal as a recipe, reuse a batch they cooked, edit or delete a recipe, or favorite one.
---

# Recipes in Mizan

Recipes are built from meals the person has already logged, so their nutrition comes from real entries.

## Save a logged meal as a recipe

1. Check the meal with `get_nutrition` (view `day`, include `entries`) for the date. A recipe needs **at least two logged items** in the same meal.
2. Call `save_food` (kind `recipe`) with `fromLog` (`date` and `mealType`) and a `title`.
3. If the answer says weights are missing, **ask the person** for them and retry with `fromLog.recipeYields` or `fromLog.entryWeightsGrams`. Never guess a weight or a batch yield.

## Reuse a batch as an ingredient

When someone cooks a large batch and eats it over several days, call `save_food` (kind `ingredient`) with the `recipeId` and the **finished weight of the whole batch in grams** as `yieldGrams`. Per-100 g nutrition depends on it. Ask for the weight; do not estimate it.

## Change or remove

- `save_food` (kind `recipe`) with `recipeId` and `title` changes a recipe and replaces the ingredient list completely. Read it with `find_food` (kind `recipe`, `recipeId`) first and send the full list, or you will drop ingredients.
- `save_food` (kind `recipe`) with `recipeId` and `favorite` true or false sets the favourite. Say which way it went.
- `delete` (kind `recipe`) is permanent and only the owner can do it. It first returns a preview (its name and what uses it) and a token. Show it; only when the person agrees, call `delete` with kind `confirm` and the token.

## Photos

`save_food` (kind `image`) stores a recipe photo from `imageUrl` (preferred) or `imageBase64` and returns its address for the recipe's `imageUrl`. Images are limited to about 4 MB.
