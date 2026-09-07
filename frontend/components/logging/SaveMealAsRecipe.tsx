"use client";

import { useId, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import * as Dialog from "@radix-ui/react-dialog";
import { Bookmark, X } from "lucide-react";
import { ApiError } from "@/lib/api";
import { clientApi } from "@/lib/api.client";
import { appToast, getErrorMessage } from "@/lib/toast";
import { formatMealType } from "@/lib/utils/mealType";

type WeightItem = { id: string; name: string; kind: "recipe" | "entry" };

export function SaveMealAsRecipe({
  date,
  mealType,
  itemCount,
  onSaved,
}: {
  date: string;
  mealType: string;
  itemCount: number;
  onSaved?: () => void;
}) {
  const id = useId();
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [weights, setWeights] = useState<WeightItem[]>([]);
  const savingRef = useRef(false);
  if (itemCount < 2) return null;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (savingRef.current) return;
    const data = new FormData(event.currentTarget);
    const recipeYields: Record<string, number> = {};
    const entryWeightsGrams: Record<string, number> = {};
    for (const item of weights) {
      const destination =
        item.kind === "recipe" ? recipeYields : entryWeightsGrams;
      destination[item.id] = Number(data.get(`weight-${item.id}`));
    }
    savingRef.current = true;
    setBusy(true);
    setError("");
    try {
      await clientApi("/api/Recipes/promote", {
        method: "POST",
        body: {
          entryDate: date,
          mealType,
          title: String(data.get("title")).trim(),
          ...(weights.length ? { recipeYields, entryWeightsGrams } : {}),
        },
      });
      setOpen(false);
      appToast.success(
        "Recipe saved",
        "Find it in the food and recipe picker next time.",
      );
      router.refresh();
      onSaved?.();
    } catch (cause) {
      if (
        cause instanceof ApiError &&
        cause.body &&
        typeof cause.body === "object"
      ) {
        const body = cause.body as { errorCode?: string; items?: WeightItem[] };
        if (
          body.errorCode === "promotion_weights_required" &&
          Array.isArray(body.items)
        ) {
          setWeights(body.items);
          setError(
            "Add these weights so your recipe keeps the correct nutrition.",
          );
          return;
        }
      }
      setError(
        getErrorMessage(cause, "Could not save this recipe. Please try again."),
      );
    } finally {
      savingRef.current = false;
      setBusy(false);
    }
  }

  return (
    <Dialog.Root
      open={open}
      onOpenChange={(next) => {
        if (!busy) {
          setOpen(next);
          if (next) {
            setError("");
            setWeights([]);
          }
        }
      }}
    >
      <Dialog.Trigger asChild>
        <button
          type="button"
          className="flex min-h-11 items-center gap-2 text-sm font-medium text-brand-700 underline-offset-4 hover:underline dark:text-brand-300"
        >
          <Bookmark aria-hidden="true" size={15} strokeWidth={1.7} />
          Save as recipe
        </button>
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-[100] bg-charcoal-blue-950/45" />
        <Dialog.Content
          className="fixed inset-x-0 bottom-0 z-[101] max-h-[92dvh] overflow-y-auto rounded-t-md border border-border bg-background p-5 pb-[calc(1.25rem+env(safe-area-inset-bottom,0px))] text-foreground sm:left-1/2 sm:right-auto sm:top-1/2 sm:bottom-auto sm:w-[calc(100%-2rem)] sm:max-w-md sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-md sm:p-6"
          onEscapeKeyDown={(event) => {
            if (busy) event.preventDefault();
          }}
          onPointerDownOutside={(event) => {
            if (busy) event.preventDefault();
          }}
        >
          <div className="mb-5 flex items-start justify-between gap-4">
            <div>
              <Dialog.Title className="text-xl font-semibold tracking-tight">
                Save as recipe
              </Dialog.Title>
              <Dialog.Description className="mt-2 text-sm leading-relaxed text-charcoal-blue-700 dark:text-charcoal-blue-300">
                Give this {formatMealType(mealType).toLowerCase()} a name. Its{" "}
                {itemCount} items and quantities are already filled in.
              </Dialog.Description>
            </div>
            <Dialog.Close
              disabled={busy}
              aria-label="Close save recipe"
              className="-mr-2 -mt-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-sm hover:bg-muted"
            >
              <X aria-hidden="true" size={18} strokeWidth={1.7} />
            </Dialog.Close>
          </div>
          <form onSubmit={submit} className="space-y-4">
            <fieldset disabled={busy} className="space-y-4">
              <div className="space-y-1.5">
                <label
                  htmlFor={`${id}-title`}
                  className="block text-sm font-medium"
                >
                  Recipe name
                </label>
                <input
                  id={`${id}-title`}
                  name="title"
                  required
                  maxLength={200}
                  placeholder="e.g. My usual breakfast"
                  autoComplete="off"
                  className="input min-h-11 w-full"
                />
              </div>
              {weights.map((item) => (
                <div key={`${item.kind}:${item.id}`} className="space-y-1.5">
                  <label
                    htmlFor={`${id}-${item.id}`}
                    className="block text-sm font-medium"
                  >
                    {item.name} (g)
                  </label>
                  <p
                    id={`${id}-${item.id}-hint`}
                    className="text-xs leading-relaxed text-charcoal-blue-700 dark:text-charcoal-blue-300"
                  >
                    {item.kind === "recipe"
                      ? "Finished weight of the entire recipe, before dividing into servings."
                      : "Weight of the amount you logged in this meal."}
                  </p>
                  <input
                    id={`${id}-${item.id}`}
                    name={`weight-${item.id}`}
                    aria-describedby={`${id}-${item.id}-hint`}
                    type="number"
                    inputMode="decimal"
                    required
                    min="0.01"
                    step="any"
                    className="input min-h-11 w-full"
                  />
                </div>
              ))}
            </fieldset>
            {error && (
              <p
                role="alert"
                className="text-sm text-burnt-peach-700 dark:text-burnt-peach-300"
              >
                {error}
              </p>
            )}
            <button
              type="submit"
              disabled={busy}
              className="btn-primary min-h-11 w-full disabled:opacity-50"
            >
              {busy ? "Saving…" : "Save recipe"}
            </button>
          </form>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

export default SaveMealAsRecipe;
