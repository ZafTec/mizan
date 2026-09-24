-- Retained nutrition for recipes imported before v2 (issue #85).
--
-- v1 stored per-serving nutrition in recipe_nutrition; v2 dropped the table
-- and calculates from ingredients. For imported recipes whose ingredients
-- cannot be calculated yet, the v1 values are the only figures there are.
--
-- Input: the v1 tables recipes, recipe_ingredients, and recipe_nutrition,
-- restored from the pre-upgrade backup into a schema named legacy_v1. See
-- docs/ARCHITECTURE.md#recipes-and-retained-nutrition for the procedure.
--
-- A value is imported only when all of these hold:
--   * the recipe still exists with the serving count it had in v1;
--   * its ingredient lines are exactly the v1 lines, so the value still
--     describes it (a recipe edited since v1 is skipped as stale);
--   * the value is plausible: positive calories, no negative macros, and
--     macro energy (4/4/9 kcal per gram) within 25% of the stated calories.
--
-- The fingerprint must equal Mizan.Domain.Recipes.RecipeFingerprint. Running
-- the script again changes nothing: existing snapshots are kept.

WITH legacy_lines AS (
    SELECT ri.recipe_id,
           encode(sha256(convert_to(string_agg(
               coalesce(ri.food_id::text, '') || '|' || ri.ingredient_text || '|'
                   || coalesce(trim_scale(ri.amount)::text, '') || '|' || coalesce(lower(btrim(ri.unit)), ''),
               E'\n' ORDER BY ri.sort_order, coalesce(ri.food_id::text, '') COLLATE "C", ri.ingredient_text COLLATE "C"),
               'UTF8')), 'hex') AS fingerprint
    FROM legacy_v1.recipe_ingredients ri
    GROUP BY ri.recipe_id
),
current_lines AS (
    SELECT ri.recipe_id,
           encode(sha256(convert_to(string_agg(
               coalesce(ri.food_id::text, '') || '|' || ri.ingredient_text || '|'
                   || coalesce(trim_scale(ri.amount)::text, '') || '|' || coalesce(lower(btrim(ri.unit)), ''),
               E'\n' ORDER BY ri.sort_order, coalesce(ri.food_id::text, '') COLLATE "C", ri.ingredient_text COLLATE "C"),
               'UTF8')), 'hex') AS fingerprint
    FROM public.recipe_ingredients ri
    GROUP BY ri.recipe_id
),
candidates AS (
    SELECT n.recipe_id,
           n.calories_per_serving AS calories,
           n.protein_grams,
           n.carbs_grams,
           n.fat_grams,
           n.fiber_grams,
           r.servings,
           cl.fingerprint
    FROM legacy_v1.recipe_nutrition n
    JOIN legacy_v1.recipes lr ON lr.id = n.recipe_id
    JOIN public.recipes r ON r.id = n.recipe_id AND r.servings = lr.servings
    JOIN legacy_lines ll ON ll.recipe_id = n.recipe_id
    JOIN current_lines cl ON cl.recipe_id = n.recipe_id AND cl.fingerprint = ll.fingerprint
    WHERE n.calories_per_serving > 0
      AND n.protein_grams >= 0 AND n.carbs_grams >= 0 AND n.fat_grams >= 0
      AND coalesce(n.fiber_grams, 0) >= 0
      AND abs(4 * n.protein_grams + 4 * n.carbs_grams + 9 * n.fat_grams - n.calories_per_serving)
          <= 0.25 * n.calories_per_serving
)
INSERT INTO public.recipe_nutrition_snapshots
    (recipe_id, calories, protein_grams, carbs_grams, fat_grams, fiber_grams, servings, ingredients_fingerprint, source, captured_at)
SELECT recipe_id, calories, protein_grams, carbs_grams, fat_grams, fiber_grams, servings, fingerprint, 'legacy_v1', now()
FROM candidates
ON CONFLICT (recipe_id) DO NOTHING;
