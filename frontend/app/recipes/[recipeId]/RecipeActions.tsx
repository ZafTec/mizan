"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast, getErrorMessage } from "@/lib/toast";
import { ConfirmDialog } from "@/components/ConfirmDialog";

export default function RecipeActions({
  recipeId,
  isOwner,
  isFavorited: initialFavorited,
}: {
  recipeId: string;
  isOwner: boolean;
  isFavorited: boolean;
}) {
  const router = useRouter();
  const [pinned, setPinned] = useState(initialFavorited);
  const [pending, setPending] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");
  const [preparation, setPreparation] = useState(false);
  const [ingredientId, setIngredientId] = useState<string | null>(null);
  async function savePreparation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    const data = new FormData(event.currentTarget);
    setPending(true);
    setError("");
    try {
      const result = await clientApi<{ foodId: string }>(
        `/api/Recipes/${recipeId}/preparation`,
        { method: "POST", body: { yieldGrams: Number(data.get("yield")) } },
      );
      setIngredientId(result.foodId);
      setPreparation(false);
      router.refresh();
      appToast.success(
        "Ingredient saved",
        "Find it by name in the food picker.",
      );
    } catch (cause) {
      setError(getErrorMessage(cause, "Could not save this ingredient."));
    } finally {
      setPending(false);
    }
  }
  return (
    <div className="mt-8">
      <div className="flex flex-wrap gap-3">
        <button
          className="btn-secondary"
          aria-pressed={pinned}
          disabled={pending}
          onClick={async () => {
            setPending(true);
            try {
              const result = await clientApi<{ isFavorited: boolean }>(
                `/api/Recipes/${recipeId}/favorite`,
                { method: "POST" },
              );
              setPinned(result.isFavorited);
              router.refresh();
            } catch (cause) {
              appToast.error(cause, "Could not update pin");
            } finally {
              setPending(false);
            }
          }}
        >
          {pinned ? "Pinned" : "Pin recipe"}
        </button>
        <button
          className="btn-secondary"
          onClick={async () => {
            try {
              await navigator.clipboard.writeText(window.location.href);
              appToast.success("Recipe link copied");
            } catch (cause) {
              appToast.error(cause, "Could not copy link");
            }
          }}
        >
          Copy link
        </button>
        {isOwner && (
          <button
            className="btn-secondary"
            onClick={() => {
              setPreparation(!preparation);
              setError("");
            }}
          >
            Use as ingredient
          </button>
        )}
      </div>
      {preparation && (
        <form
          onSubmit={savePreparation}
          className="mt-5 border-y py-5 max-w-lg"
        >
          <p className="font-medium">Save a preparation</p>
          <p className="text-sm text-muted-foreground mt-2">
            Weigh the finished batch so its nutrition per gram is accurate. You
            can then log a portion or use it in another meal.
          </p>
          <label htmlFor="batch-weight" className="label mt-4">
            Finished batch weight (g)
          </label>
          <input
            id="batch-weight"
            name="yield"
            type="number"
            min="0.1"
            step="any"
            required
            className="input"
            disabled={pending}
          />
          <button className="btn-primary mt-4" disabled={pending}>
            {pending ? "Saving…" : "Save ingredient"}
          </button>
        </form>
      )}
      {ingredientId && (
        <p className="text-sm text-muted-foreground mt-4">
          Saved as an ingredient. Find it by name when logging a meal.
        </p>
      )}
      {error && (
        <p role="alert" className="text-destructive text-sm mt-4">
          {error}
        </p>
      )}
      {isOwner && (
        <div className="mt-8 pt-5 border-t">
          <button
            type="button"
            className="btn-ghost-destructive btn-sm -ml-3.5"
            onClick={() => setDeleting(true)}
          >
            Delete recipe
          </button>
        </div>
      )}
      <ConfirmDialog
        open={deleting}
        onClose={() => setDeleting(false)}
        title="Delete this recipe?"
        description="The saved recipe is removed. Meals you already logged from it keep their recorded nutrition."
        confirmLabel="Delete recipe"
        pendingLabel="Deleting…"
        cancelLabel="Keep recipe"
        errorFallback="Could not delete recipe"
        onConfirm={async () => {
          await clientApi(`/api/Recipes/${recipeId}`, { method: "DELETE" });
          router.push("/recipes");
          router.refresh();
        }}
      />
    </div>
  );
}
