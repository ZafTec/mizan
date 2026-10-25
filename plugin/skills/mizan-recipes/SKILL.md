---
name: mizan-recipes
description: Turn logged meals into reusable recipes in Mizan and manage them. Use when the user wants to save a meal as a recipe, reuse a batch they cooked, edit or delete a recipe, or favorite one.
---

# Recipes in Mizan

Recipes are built from meals the person has already logged, so their nutrition comes from real entries.

## Save a logged meal as a recipe

1. Check the meal with `get_food_diary` for the date. A recipe needs **at least two logged items** in the same meal.
2. Call `promote_to_recipe` with the `date`, the `mealType` and a `title`.
3. If the response says weights are missing, **ask the person** for them and retry with `recipeYieldsJson` or `entryWeightsGramsJson`. Never guess a weight or a batch yield.

## Reuse a batch as an ingredient

When someone cooks a large batch and eats it over several days, call `promote_recipe_to_preparation` with the recipe id and the **finished weight of the whole batch in grams**. Per-100 g nutrition depends on it. Ask for the weight; do not estimate it.

## Change or remove

- `update_recipe` replaces the ingredient list completely. Read the recipe with `get_recipe` first and send the full list, or you will drop ingredients.
- `delete_recipe` is permanent and only the owner can do it. Confirm with the person first.
- `toggle_favorite_recipe` switches the favorite flag. Say which way it went.

## Photos

`upload_image` stores a recipe photo (folder `recipes`) and returns a key and URL. Images are limited to about 4 MB.
