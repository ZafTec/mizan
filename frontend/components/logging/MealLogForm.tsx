"use client";

import { useId, useRef, useState, type FormEvent } from "react";
import { Camera, Plus, Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { publishLogFeedback } from "@/lib/log-feedback";
import type { GamificationFeedback } from "@/types/gamification";
import { getErrorMessage } from "@/lib/toast";
import { dateInTimeZone } from "@/lib/log-date";
import {
  MEAL_TYPES,
  defaultMealTypeForHour,
  formatMealType,
} from "@/lib/utils/mealType";
import FoodPicker from "./FoodPicker";
import {
  nutritionForSelection,
  recipeItem,
  type RecipeResult,
  type MealSelection,
  type PickerItem,
} from "./logging";

type ManualItem = {
  key: string;
  name: string;
  calories: number;
  proteinGrams: number;
  carbsGrams: number;
  fatGrams: number;
};

export default function MealLogForm({
  initialDate,
  initialMealType,
  initialRecipe,
  timeZone = "UTC",
  onSaved,
  onBusyChange,
  onUsePhoto,
}: {
  initialDate?: string;
  initialMealType?: string;
  initialRecipe?: RecipeResult;
  timeZone?: string;
  onSaved: (message: string) => void;
  onBusyChange: (busy: boolean) => void;
  onUsePhoto?: () => void;
}) {
  const id = useId();
  const router = useRouter();
  const today = dateInTimeZone(timeZone);
  const [date, setDate] = useState(initialDate || today);
  const [mealType, setMealType] = useState(
    () =>
      initialMealType ||
      defaultMealTypeForHour(
        Number(
          new Intl.DateTimeFormat("en-GB", {
            timeZone,
            hour: "numeric",
            hourCycle: "h23",
          }).format(new Date()),
        ),
      ),
  );
  const [selected, setSelected] = useState<MealSelection[]>(() =>
    initialRecipe
      ? [
          {
            key: initialRecipe.id,
            item: recipeItem(initialRecipe),
            amount: "1",
          },
        ]
      : [],
  );
  const [manual, setManual] = useState<ManualItem[]>([]);
  const [manualOpen, setManualOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const savingRef = useRef(false);
  const totalCount = selected.length + manual.length;
  const calories =
    selected.reduce(
      (sum, entry) => sum + (nutritionForSelection(entry).calories ?? 0),
      0,
    ) + manual.reduce((sum, entry) => sum + entry.calories, 0);
  const hasUnknownNutrition = selected.some(
    (entry) => entry.item.calories === null,
  );

  function addItem(item: PickerItem) {
    setError("");
    setSelected((current) => {
      const existing = current.find(
        (entry) => entry.item.id === item.id && entry.item.kind === item.kind,
      );
      if (existing)
        return current.map((entry) =>
          entry.key === existing.key
            ? {
                ...entry,
                amount: String(Number(entry.amount) + item.servingSize),
              }
            : entry,
        );
      return [
        ...current,
        { key: crypto.randomUUID(), item, amount: String(item.servingSize) },
      ];
    });
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (savingRef.current || totalCount === 0) return;
    savingRef.current = true;
    setBusy(true);
    onBusyChange(true);
    setError("");
    let saved = 0;
    try {
      // Remove each confirmed write immediately. A later failure leaves only
      // unsaved entries here, so retrying never duplicates the successful ones.
      for (const entry of selected) {
        const feedback = await clientApi<GamificationFeedback>(
          "/api/Nutrition/log",
          {
            method: "POST",
            body: {
              ...(entry.item.kind === "food"
                ? { foodId: entry.item.id }
                : { recipeId: entry.item.id }),
              entryDate: date,
              mealType,
              servings:
                Number(entry.amount) /
                (entry.item.kind === "food" ? entry.item.servingSize : 1),
            },
          },
        );
        publishLogFeedback(feedback);
        saved += 1;
        setSelected((current) =>
          current.filter((item) => item.key !== entry.key),
        );
      }
      for (const entry of manual) {
        const { key, ...nutrition } = entry;
        const feedback = await clientApi<GamificationFeedback>("/api/Meals", {
          method: "POST",
          body: { ...nutrition, entryDate: date, mealType, servings: 1 },
        });
        publishLogFeedback(feedback);
        saved += 1;
        setManual((current) => current.filter((item) => item.key !== key));
      }
      onSaved(`${formatMealType(mealType)} logged`);
    } catch (cause) {
      setError(
        `${saved ? `${saved} ${saved === 1 ? "item was" : "items were"} saved. Only the remaining items will be retried. ` : ""}${getErrorMessage(cause, "Could not save your meal. Please try again.")}`,
      );
      if (saved) router.refresh();
    } finally {
      savingRef.current = false;
      setBusy(false);
      onBusyChange(false);
    }
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="min-h-0 flex-1 scroll-py-6 space-y-5 overflow-y-auto overscroll-contain pb-5">
        <form id={`${id}-meal`} onSubmit={submit} className="space-y-5">
          <fieldset disabled={busy} className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label
                htmlFor={`${id}-date`}
                className="block text-sm font-medium"
              >
                Date
              </label>
              <input
                id={`${id}-date`}
                type="date"
                required
                max={today}
                value={date}
                onChange={(event) => setDate(event.target.value)}
                className="input min-h-11 w-full min-w-0"
              />
            </div>
            <div className="space-y-1.5">
              <label
                htmlFor={`${id}-type`}
                className="block text-sm font-medium"
              >
                Meal
              </label>
              <select
                id={`${id}-type`}
                value={mealType}
                onChange={(event) => setMealType(event.target.value)}
                className="input min-h-11 w-full"
              >
                {MEAL_TYPES.map((type) => (
                  <option key={type} value={type}>
                    {formatMealType(type)}
                  </option>
                ))}
              </select>
            </div>
          </fieldset>
          {totalCount > 0 && (
            <section aria-label="Items in this meal" className="space-y-2">
              <h3 className="text-sm font-semibold">Your meal</h3>
              <ul className="divide-y divide-border border-y border-border">
                {selected.map((entry) => {
                  const nutrition = nutritionForSelection(entry);
                  return (
                    <li
                      key={entry.key}
                      className="flex items-center gap-2 py-3"
                    >
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium">
                          {entry.item.name}
                        </p>
                        <p className="mt-0.5 text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                          {nutrition.calories === null
                            ? "Nutrition unavailable"
                            : `${Math.round(nutrition.calories)} kcal`}
                          {nutrition.protein !== null &&
                            ` · ${Math.round(nutrition.protein * 10) / 10} g protein`}
                        </p>
                      </div>
                      <div className="flex shrink-0 items-center gap-1.5">
                        <label
                          htmlFor={`${id}-${entry.key}`}
                          className="sr-only"
                        >
                          {entry.item.name} quantity in{" "}
                          {entry.item.kind === "food" ? "grams" : "servings"}
                        </label>
                        <input
                          id={`${id}-${entry.key}`}
                          type="number"
                          inputMode="decimal"
                          min="0.01"
                          step="any"
                          required
                          disabled={busy}
                          value={entry.amount}
                          onChange={(event) =>
                            setSelected((current) =>
                              current.map((item) =>
                                item.key === entry.key
                                  ? { ...item, amount: event.target.value }
                                  : item,
                              ),
                            )
                          }
                          className="input min-h-11 w-[72px] px-2 text-right tabular-nums"
                        />
                        <span className="w-7 text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                          {entry.item.kind === "food" ? "g" : "srv"}
                        </span>
                      </div>
                      <button
                        type="button"
                        disabled={busy}
                        aria-label={`Remove ${entry.item.name}`}
                        onClick={() =>
                          setSelected((current) =>
                            current.filter((item) => item.key !== entry.key),
                          )
                        }
                        className="flex h-11 w-11 shrink-0 items-center justify-center text-charcoal-blue-700 hover:text-burnt-peach-700 dark:text-charcoal-blue-300"
                      >
                        <Trash2
                          aria-hidden="true"
                          size={16}
                          strokeWidth={1.7}
                        />
                      </button>
                    </li>
                  );
                })}
                {manual.map((entry) => (
                  <li key={entry.key} className="flex items-center gap-3 py-3">
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">
                        {entry.name}
                      </p>
                      <p className="text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                        {entry.calories} kcal · {entry.proteinGrams} g protein ·
                        Custom entry
                      </p>
                    </div>
                    <button
                      type="button"
                      disabled={busy}
                      aria-label={`Remove ${entry.name}`}
                      onClick={() =>
                        setManual((current) =>
                          current.filter((item) => item.key !== entry.key),
                        )
                      }
                      className="flex h-11 w-11 shrink-0 items-center justify-center text-charcoal-blue-700 hover:text-burnt-peach-700 dark:text-charcoal-blue-300"
                    >
                      <Trash2 aria-hidden="true" size={16} strokeWidth={1.7} />
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          )}
          <FoodPicker date={date} disabled={busy} onSelect={addItem} />
        </form>

        <button
          type="button"
          disabled={busy}
          onClick={() => setManualOpen((current) => !current)}
          aria-expanded={manualOpen}
          aria-controls={`${id}-manual`}
          className="flex min-h-11 items-center gap-2 text-sm font-medium text-brand-700 underline-offset-4 hover:underline dark:text-brand-300"
        >
          <Plus aria-hidden="true" size={16} strokeWidth={1.7} />
          {manualOpen ? "Close custom entry" : "Enter a food manually"}
        </button>
        {manualOpen && (
          <ManualFoodForm
            id={`${id}-manual`}
            onAdd={(entry) => {
              setManual((current) => [
                ...current,
                { ...entry, key: crypto.randomUUID() },
              ]);
              setManualOpen(false);
              setError("");
            }}
          />
        )}
        {onUsePhoto && (
          <button
            type="button"
            disabled={busy}
            onClick={onUsePhoto}
            className="flex min-h-11 items-center gap-2 text-sm text-charcoal-blue-700 underline-offset-4 hover:underline dark:text-charcoal-blue-300"
          >
            <Camera aria-hidden="true" size={16} strokeWidth={1.7} />
            Use a photo instead
          </button>
        )}
      </div>
      <div className="shrink-0 space-y-3 border-t border-border bg-background pb-[calc(1.25rem+env(safe-area-inset-bottom,0px))] pt-4 sm:pb-6">
        {error && (
          <p
            role="alert"
            className="text-sm text-burnt-peach-700 dark:text-burnt-peach-300"
          >
            {error}
          </p>
        )}
        <div className="flex items-center justify-between gap-4">
          <div aria-live="polite" className="text-sm">
            <span className="font-semibold tabular-nums">
              {totalCount
                ? `${Math.round(calories)}${hasUnknownNutrition ? "+" : ""} kcal`
                : "Build your meal"}
            </span>
            <p className="mt-0.5 text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
              {totalCount
                ? `${totalCount} ${totalCount === 1 ? "item" : "items"} to log`
                : "Add food from the list above."}
            </p>
          </div>
          <button
            type="submit"
            form={`${id}-meal`}
            disabled={busy || totalCount === 0 || manualOpen}
            className="btn-primary min-h-11 shrink-0 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {busy ? "Saving…" : "Log meal"}
          </button>
        </div>
      </div>
    </div>
  );
}

function ManualFoodForm({
  id,
  onAdd,
}: {
  id: string;
  onAdd: (item: Omit<ManualItem, "key">) => void;
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    onAdd({
      name: String(form.get("name")).trim(),
      calories: Number(form.get("calories")),
      proteinGrams: Number(form.get("proteinGrams")),
      carbsGrams: Number(form.get("carbsGrams")),
      fatGrams: Number(form.get("fatGrams")),
    });
  }
  return (
    <form
      id={id}
      onSubmit={submit}
      className="space-y-4 border-y border-border py-4"
    >
      <div>
        <h3 className="text-sm font-semibold">Custom food</h3>
        <p className="mt-1 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
          Enter the nutrition for the amount you ate.
        </p>
      </div>
      <div className="space-y-1.5">
        <label htmlFor={`${id}-name`} className="block text-sm font-medium">
          Food name
        </label>
        <input
          id={`${id}-name`}
          name="name"
          autoComplete="off"
          required
          maxLength={200}
          className="input min-h-11 w-full"
        />
      </div>
      <div className="grid grid-cols-2 gap-3">
        {(
          [
            ["calories", "Calories (kcal)"],
            ["proteinGrams", "Protein (g)"],
            ["carbsGrams", "Carbs (g)"],
            ["fatGrams", "Fat (g)"],
          ] as const
        ).map(([name, label]) => (
          <div key={name} className="space-y-1.5">
            <label
              htmlFor={`${id}-${name}`}
              className="block text-sm font-medium"
            >
              {label}
            </label>
            <input
              id={`${id}-${name}`}
              name={name}
              type="number"
              min="0"
              step="any"
              inputMode="decimal"
              required
              className="input min-h-11 w-full"
            />
          </div>
        ))}
      </div>
      <button type="submit" className="btn-secondary min-h-11 w-full">
        Add to meal
      </button>
    </form>
  );
}
