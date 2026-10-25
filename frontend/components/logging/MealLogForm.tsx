"use client";

import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { BookOpen, Search, Trash2 } from "lucide-react";
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
import type { FoodAnalysis, NutritionPer100g } from "@/lib/api/food-photo";
import {
  previewMeal,
  type MealPreviewLine,
  type MealPreviewResult,
} from "@/lib/api/meals";
import AiMealInput from "./AiMealInput";
import FoodPicker from "./FoodPicker";
import RecipePicker from "./RecipePicker";
import {
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

/**
 * What the assistant proposed for one food: a name, a guessed weight and the nutrition per 100 g. The person
 * corrects the name and the grams; the server scales the per-100 g figures to the grams it receives. The key
 * is minted once per row and reused on a retry, so the same row can never be logged twice.
 */
type ProposedItem = {
  key: string;
  idempotencyKey: string;
  name: string;
  grams: string;
  per100g: NutritionPer100g;
};

/** How long typing pauses before the form asks the server what the meal comes to. */
const PREVIEW_DELAY_MS = 150;

/** A line's calories and protein as the preview reports them, or a mark while it is on its way. */
function lineFigures(
  result: MealPreviewResult | null,
  key: string,
): { calories: number | null; protein: number | null } | "pending" {
  const line = result?.lines.find((item) => item.key === key);
  if (!line) return "pending";
  return {
    calories: line.calories ?? null,
    protein: line.proteinGrams ?? null,
  };
}

export default function MealLogForm({
  initialDate,
  initialMealType,
  initialRecipe,
  timeZone = "UTC",
  onSaved,
  onBusyChange,
}: {
  initialDate?: string;
  initialMealType?: string;
  initialRecipe?: RecipeResult;
  timeZone?: string;
  onSaved: (message: string) => void;
  onBusyChange: (busy: boolean) => void;
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
  const [proposed, setProposed] = useState<ProposedItem[]>([]);
  const [proposal, setProposal] = useState<{
    source: "text" | "photo";
    confidence: number;
    note?: string | null;
  } | null>(null);
  const [manualOpen, setManualOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [view, setView] = useState<"meal" | "recipes">("meal");
  const [added, setAdded] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const savingRef = useRef(false);
  const mealList = useRef<HTMLElement>(null);

  // A new estimate lands below the box that asked for it. Bring it into view, so the person sees what came back.
  useEffect(() => {
    if (proposal) mealList.current?.scrollIntoView?.({ block: "start" });
  }, [proposal]);
  const totalCount = selected.length + manual.length + proposed.length;
  // What the meal comes to is the server's arithmetic (POST /api/Meals/preview), by the rules logging uses.
  const previewLines = useMemo<MealPreviewLine[]>(
    () => [
      ...selected.map((entry) =>
        entry.item.kind === "food"
          ? {
              key: entry.key,
              foodId: entry.item.id,
              grams: Number(entry.amount) || 0,
            }
          : {
              key: entry.key,
              recipeId: entry.item.id,
              servings: Number(entry.amount) || 0,
            },
      ),
      ...proposed.map((entry) => ({
        key: entry.key,
        per100g: entry.per100g,
        grams: Number(entry.grams) || 0,
      })),
      ...manual.map((entry) => ({
        key: entry.key,
        calories: entry.calories,
        proteinGrams: entry.proteinGrams,
      })),
    ],
    [selected, proposed, manual],
  );
  const [preview, setPreview] = useState<MealPreviewResult | null>(null);
  const previewRequest = useRef(0);
  useEffect(() => {
    const request = ++previewRequest.current;
    if (previewLines.length === 0) {
      setPreview(null);
      return;
    }
    const timer = setTimeout(() => {
      previewMeal(previewLines)
        .then((result) => {
          if (request === previewRequest.current) setPreview(result);
        })
        .catch(() => {
          // The preview is a convenience; logging still works without it.
        });
    }, PREVIEW_DELAY_MS);
    return () => clearTimeout(timer);
  }, [previewLines]);

  function addProposal(analysis: FoodAnalysis, source: "text" | "photo") {
    setError("");
    setAdded("");
    setProposal({
      source,
      confidence: analysis.confidence,
      note: analysis.note,
    });
    setProposed((current) => [
      ...current,
      // A food the model gave no weight for cannot be scaled, so it is not offered as a row.
      ...analysis.foods
        .filter((food) => food.per100g && food.portionGrams > 0)
        .map((food) => ({
          key: crypto.randomUUID(),
          idempotencyKey: crypto.randomUUID(),
          name: food.name,
          grams: String(Math.round(food.portionGrams)),
          per100g: food.per100g!,
        })),
    ]);
  }

  function addRecipe(recipe: RecipeResult) {
    addItem(recipeItem(recipe));
    setAdded(`Added ${recipe.title}`);
    setView("meal");
  }

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
            // What the person typed: grams of a food, servings of a recipe. The server converts.
            body: {
              ...(entry.item.kind === "food"
                ? { foodId: entry.item.id, amountGrams: Number(entry.amount) }
                : { recipeId: entry.item.id, servings: Number(entry.amount) }),
              entryDate: date,
              mealType,
            },
          },
        );
        publishLogFeedback(feedback);
        saved += 1;
        setSelected((current) =>
          current.filter((item) => item.key !== entry.key),
        );
      }
      for (const entry of proposed) {
        // The grams the person confirmed and the per-100 g figures; the server does the arithmetic.
        const feedback = await clientApi<GamificationFeedback>("/api/Meals", {
          method: "POST",
          body: {
            name: entry.name.trim(),
            entryDate: date,
            mealType,
            servings: 1,
            amountGrams: Number(entry.grams),
            per100g: entry.per100g,
          },
          headers: { "Idempotency-Key": entry.idempotencyKey },
        });
        publishLogFeedback(feedback);
        saved += 1;
        setProposed((current) =>
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
      {view === "recipes" && (
        <RecipePicker onPick={addRecipe} onBack={() => setView("meal")} />
      )}
      <div hidden={view === "recipes"} className="flex min-h-0 flex-1 flex-col">
        <div className="min-h-0 flex-1 scroll-py-6 space-y-5 overflow-y-auto overscroll-contain pb-5">
          <AiMealInput
            disabled={busy}
            onProposal={addProposal}
            onBusyChange={(working) => {
              setBusy(working);
              onBusyChange(working);
            }}
          />
          {added && (
            <p
              role="status"
              className="text-sm text-brand-700 dark:text-brand-300"
            >
              {added}
            </p>
          )}
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
              <section
                ref={mealList}
                aria-label="Items in this meal"
                className="space-y-2"
              >
                <h3 className="text-sm font-semibold">Your meal</h3>
                {proposal && proposed.length > 0 && (
                  <p className="flex items-start gap-2 text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                    <span>
                      Estimated from{" "}
                      {proposal.source === "photo"
                        ? "your photo"
                        : "your description"}{" "}
                      at {Math.round(proposal.confidence * 100)}% confidence.
                      Check the portions.
                      {proposal.note && (
                        <span className="mt-1 block">{proposal.note}</span>
                      )}
                    </span>
                  </p>
                )}
                <ul className="divide-y divide-border border-y border-border">
                  {proposed.map((entry) => {
                    const nutrition = lineFigures(preview, entry.key);
                    return (
                      <li
                        key={entry.key}
                        className="flex items-center gap-2 py-3"
                      >
                        <div className="min-w-0 flex-1">
                          <label
                            htmlFor={`${id}-${entry.key}-name`}
                            className="sr-only"
                          >
                            Food name
                          </label>
                          <input
                            id={`${id}-${entry.key}-name`}
                            value={entry.name}
                            disabled={busy}
                            maxLength={200}
                            onChange={(event) =>
                              setProposed((current) =>
                                current.map((item) =>
                                  item.key === entry.key
                                    ? { ...item, name: event.target.value }
                                    : item,
                                ),
                              )
                            }
                            className="input min-h-11 w-full !py-1.5 text-sm font-medium"
                          />
                          <p className="mt-0.5 text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                            {nutrition === "pending" ||
                            nutrition.calories === null
                              ? "…"
                              : `${Math.round(nutrition.calories)} kcal · ${Math.round((nutrition.protein ?? 0) * 10) / 10} g protein`}{" "}
                            · Estimated
                          </p>
                        </div>
                        <div className="flex shrink-0 items-center gap-1.5">
                          <label
                            htmlFor={`${id}-${entry.key}`}
                            className="sr-only"
                          >
                            {entry.name} quantity in grams
                          </label>
                          <input
                            id={`${id}-${entry.key}`}
                            type="number"
                            inputMode="decimal"
                            min="1"
                            step="any"
                            required
                            disabled={busy}
                            value={entry.grams}
                            onChange={(event) =>
                              setProposed((current) =>
                                current.map((item) =>
                                  item.key === entry.key
                                    ? { ...item, grams: event.target.value }
                                    : item,
                                ),
                              )
                            }
                            className="input min-h-11 w-[72px] px-2 text-right tabular-nums"
                          />
                          <span className="w-7 text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                            g
                          </span>
                        </div>
                        <button
                          type="button"
                          disabled={busy}
                          aria-label={`Remove ${entry.name}`}
                          onClick={() =>
                            setProposed((current) =>
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
                  {selected.map((entry) => {
                    const nutrition = lineFigures(preview, entry.key);
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
                            {nutrition === "pending"
                              ? "…"
                              : nutrition.calories === null
                                ? "Nutrition unavailable"
                                : `${Math.round(nutrition.calories)} kcal`}
                            {nutrition !== "pending" &&
                              nutrition.protein !== null &&
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
                    <li
                      key={entry.key}
                      className="flex items-center gap-3 py-3"
                    >
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium">
                          {entry.name}
                        </p>
                        <p className="text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                          {entry.calories} kcal · {entry.proteinGrams} g protein
                          · Custom entry
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
                        <Trash2
                          aria-hidden="true"
                          size={16}
                          strokeWidth={1.7}
                        />
                      </button>
                    </li>
                  ))}
                </ul>
              </section>
            )}
            {searchOpen && (
              <FoodPicker date={date} disabled={busy} onSelect={addItem} />
            )}
          </form>

          <div className="flex flex-wrap gap-x-5 gap-y-1">
            <button
              type="button"
              disabled={busy}
              aria-expanded={searchOpen}
              onClick={() => setSearchOpen((current) => !current)}
              className="flex min-h-11 items-center gap-2 text-sm font-medium text-brand-700 underline-offset-4 hover:underline dark:text-brand-300"
            >
              {searchOpen ? "Hide food search" : "Search foods"}
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => setView("recipes")}
              className="flex min-h-11 items-center gap-2 text-sm font-medium text-brand-700 underline-offset-4 hover:underline dark:text-brand-300"
            >
              <BookOpen aria-hidden="true" size={16} strokeWidth={1.7} />
              Log a recipe
            </button>
          </div>

          <button
            type="button"
            disabled={busy}
            onClick={() => setManualOpen((current) => !current)}
            aria-expanded={manualOpen}
            aria-controls={`${id}-manual`}
            className="flex min-h-11 items-center gap-2 text-sm font-medium text-brand-700 underline-offset-4 hover:underline dark:text-brand-300"
          >
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
              <span
                className="font-semibold tabular-nums"
                data-testid="meal-form-total"
              >
                {!totalCount
                  ? "Build your meal"
                  : preview
                    ? `${Math.round(preview.calories)}${preview.complete ? "" : "+"} kcal`
                    : "… kcal"}
              </span>
              <p className="mt-0.5 text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                {totalCount
                  ? `${totalCount} ${totalCount === 1 ? "item" : "items"} to log`
                  : "Describe your meal above, or search for a food."}
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
