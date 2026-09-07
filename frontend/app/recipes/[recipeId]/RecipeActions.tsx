"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Bookmark, Share2, Trash2 } from "lucide-react";
import { clientApi } from "@/lib/api.client";
import { appToast, getErrorMessage } from "@/lib/toast";
import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";

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
          <Bookmark size={16} fill={pinned ? "currentColor" : "none"} />
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
          <Share2 size={16} />
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
          <p className="text-sm log-muted mt-2">
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
        <p className="text-sm log-muted mt-4">
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
            className="text-link text-destructive!"
            onClick={() => setDeleting(true)}
          >
            <Trash2 size={15} />
            Delete recipe
          </button>
        </div>
      )}
      <Dialog
        open={deleting}
        onOpenChange={(open) => {
          if (!pending) setDeleting(open);
        }}
      >
        <DialogContent>
          <DialogTitle>Delete this recipe?</DialogTitle>
          <DialogDescription>
            Your previous meal entries keep their recorded nutrition. The saved
            recipe will be removed.
          </DialogDescription>
          <div className="flex justify-end gap-3">
            <button
              className="btn-secondary"
              disabled={pending}
              onClick={() => setDeleting(false)}
            >
              Keep recipe
            </button>
            <button
              className="btn-danger"
              disabled={pending}
              onClick={async () => {
                setPending(true);
                try {
                  await clientApi(`/api/Recipes/${recipeId}`, {
                    method: "DELETE",
                  });
                  router.push("/recipes");
                  router.refresh();
                } catch (cause) {
                  appToast.error(cause, "Could not delete recipe");
                } finally {
                  setPending(false);
                }
              }}
            >
              {pending ? "Deleting…" : "Delete recipe"}
            </button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
