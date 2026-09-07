"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast, getErrorMessage } from "@/lib/toast";
import type { Recipe } from "@/data/recipe";

export default function RecipeIngredients({ recipe }: { recipe: Recipe }) {
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const router = useRouter();
  const ingredients = recipe.ingredients || [];
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const form = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await clientApi(`/api/Recipes/${recipe.id}`, {
        method: "PUT",
        body: {
          id: recipe.id,
          title: recipe.title,
          description: recipe.description,
          instructions: recipe.instructions,
          servings: Number(form.get("servings")),
          prepTimeMinutes: recipe.prepTimeMinutes,
          cookTimeMinutes: recipe.cookTimeMinutes,
          imageUrl: recipe.imageUrl,
          isPublic: recipe.isPublic,
          ingredients: ingredients.map((ingredient, index) => ({
            foodId: ingredient.foodId,
            ingredientText: ingredient.ingredientText || ingredient.foodName,
            amount:
              form.get(`amount-${index}`) === ""
                ? null
                : Number(form.get(`amount-${index}`)),
            unit: ingredient.unit,
          })),
        },
      });
      setEditing(false);
      router.refresh();
      appToast.success("Recipe quantities updated");
    } catch (cause) {
      setError(
        getErrorMessage(
          cause,
          "Could not update the quantities. Please try again.",
        ),
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <section id="ingredients" className="scroll-mt-6">
      <header className="log-section-header">
        <h2>Ingredients</h2>
        {recipe.isOwner && !editing && (
          <button className="text-link" onClick={() => setEditing(true)}>
            Adjust quantities
          </button>
        )}
      </header>
      {editing ? (
        <form onSubmit={save}>
          <fieldset disabled={busy}>
            <div className="flex items-center justify-between gap-4 py-4 border-b">
              <label htmlFor="recipe-servings" className="text-sm">
                Servings per recipe
              </label>
              <input
                id="recipe-servings"
                className="input max-w-28 num"
                type="number"
                name="servings"
                min={1}
                step={1}
                required
                defaultValue={recipe.servings}
              />
            </div>
            {ingredients.map((ingredient, index) => (
              <div key={index} className="recipe-ingredient-row">
                <label className="min-w-0" htmlFor={`recipe-amount-${index}`}>
                  {ingredient.foodName || ingredient.ingredientText}
                </label>
                <div className="flex items-center gap-2">
                  <input
                    id={`recipe-amount-${index}`}
                    className="input w-28! num"
                    type="number"
                    min="0.01"
                    step="any"
                    name={`amount-${index}`}
                    defaultValue={ingredient.amount ?? ""}
                    required={!!ingredient.foodId}
                  />
                  <span className="text-xs log-muted">{ingredient.unit}</span>
                </div>
              </div>
            ))}
            {error && (
              <p role="alert" className="text-destructive text-sm mt-3">
                {error}
              </p>
            )}
            <div className="flex gap-3 mt-4">
              <button type="submit" className="btn-primary">
                {busy ? "Saving…" : "Save quantities"}
              </button>
              <button
                type="button"
                className="btn-secondary"
                onClick={() => {
                  setEditing(false);
                  setError("");
                }}
              >
                Cancel
              </button>
            </div>
          </fieldset>
        </form>
      ) : (
        <ul>
          {ingredients.map((ingredient, index) => (
            <li className="recipe-ingredient-row" key={index}>
              <span className="min-w-0">
                {ingredient.foodName || ingredient.ingredientText}
              </span>
              <span className="num shrink-0 log-muted">
                {ingredient.amount != null
                  ? `${ingredient.amount} ${ingredient.unit}`
                  : "To taste"}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
