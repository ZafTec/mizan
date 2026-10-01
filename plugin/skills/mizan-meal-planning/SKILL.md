---
name: mizan-meal-planning
description: Plan meals and shopping in Mizan. Use when the user wants a meal plan for a week, wants recipes scheduled on dates, needs a shopping list, or shares meals with a household.
---

# Meal plans and shopping lists

## Build a plan

1. Look at what the person already has: `search_recipes` for their recipes, and `get_current_goal` for their targets.
2. `create_meal_plan` with a name and a start and end date.
3. `add_recipe_to_meal_plan` for each slot: the plan id, the recipe id, the date, the meal type and the servings.
4. Show the plan with `get_meal_plan`. It includes nutrition totals, so compare them with the goal and offer to adjust.

Change a slot with `update_meal_plan_recipe`, or remove it with `remove_recipe_from_meal_plan`. `delete_meal_plan` removes the plan and everything scheduled in it, so confirm first.

## Shopping lists

1. `create_shopping_list` with a name.
2. `add_shopping_list_item` for each thing to buy.
3. `toggle_shopping_list_item` marks an item bought or not. Say which way it went.

## Households

A plan or list can belong to a household so other members see it. This connection can only use the households the person allowed when they connected the app. If a household is not available, say so and offer to save the plan as personal, or tell the person they can allow it in Mizan under More, then Connected apps.

Do not try to work around a refusal. The permission is the person's choice.
