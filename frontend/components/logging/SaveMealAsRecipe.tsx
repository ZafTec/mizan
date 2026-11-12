"use client";

import { useId, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Modal } from "@/components/ui/dialog";
import {
  Drawer,
  DrawerBody,
  DrawerCloseButton,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from "@/components/ui/drawer";
import { useNarrowScreen } from "@/lib/hooks/useMediaQuery";
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
  const narrow = useNarrowScreen();
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

  function openForm() {
    setError("");
    setWeights([]);
    setOpen(true);
  }

  function close() {
    if (!busy) setOpen(false);
  }

  const description = (
    <>
      Give this {formatMealType(mealType).toLowerCase()} a name. Its{" "}
      {itemCount} items and quantities are already filled in.
    </>
  );
  const fields = (
    <form id={`${id}-form`} onSubmit={submit} className="space-y-4">
      <fieldset disabled={busy} className="space-y-4">
        <div className="space-y-1.5">
          <label htmlFor={`${id}-title`} className="block text-sm font-medium">
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
    </form>
  );
  const save = (
    <button
      type="submit"
      form={`${id}-form`}
      disabled={busy}
      className="btn-primary min-h-11 w-full shrink-0"
    >
      {busy ? "Saving…" : "Save recipe"}
    </button>
  );

  return (
    <>
      <button
        type="button"
        onClick={openForm}
        className="flex min-h-11 items-center text-sm font-medium text-brand-700 underline-offset-4 hover:underline dark:text-brand-300"
      >
        Save as recipe
      </button>
      {/* A phone gets a sheet from the bottom; a wider screen the shared dialog. */}
      {narrow ? (
        <Drawer
          open={open}
          onOpenChange={(next) => (next ? setOpen(true) : close())}
          dismissible={!busy}
        >
          <DrawerContent>
            <DrawerHeader>
              <div className="min-w-0 flex-1 py-1.5">
                <DrawerTitle>Save as recipe</DrawerTitle>
                <DrawerDescription>{description}</DrawerDescription>
              </div>
              <DrawerCloseButton label="Close save recipe" disabled={busy} />
            </DrawerHeader>
            <DrawerBody className="space-y-4">
              {fields}
              {save}
            </DrawerBody>
          </DrawerContent>
        </Drawer>
      ) : (
        <Modal
          open={open}
          onClose={close}
          title="Save as recipe"
          description={description}
          footer={save}
          closeOnOverlayClick={!busy}
          showClose={!busy}
        >
          {fields}
        </Modal>
      )}
    </>
  );
}

export default SaveMealAsRecipe;
