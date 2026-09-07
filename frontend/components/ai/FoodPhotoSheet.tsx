"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Icon } from "@/components/ui/icon";
import { appToast, getErrorMessage } from "@/lib/toast";
import { useSession } from "@/lib/auth-client";
import { dateInTimeZone } from "@/lib/log-date";
import { publishLogFeedback } from "@/lib/log-feedback";
import {
  analyzeFoodPhoto,
  logMeal,
  type FoodAnalysis,
  type RecognizedFood,
} from "@/lib/api/food-photo";
import { useProWall } from "@/components/billing/ProWall";
import { cn } from "@/lib/utils";

const MEAL_TYPES = ["BREAKFAST", "LUNCH", "DINNER", "SNACK", "DRINK"] as const;

interface Row extends RecognizedFood {
  include: boolean;
}

function round(value: number) {
  return Math.round(value * 10) / 10;
}

/**
 * A photo becomes a proposal, never an entry.
 *
 * §12 puts this plainly: the assistant proposes and the user confirms. Every
 * number here is editable and every row can be dropped before anything is
 * written, because the model is guessing at a portion size from a photograph
 * and it will sometimes be wrong by a lot.
 */
export default function FoodPhotoSheet({
  initialDate,
  initialMealType,
  onLogged,
  onBusyChange,
}: {
  initialDate?: string;
  initialMealType?: string;
  onLogged?: () => void;
  onBusyChange?: (busy: boolean) => void;
}) {
  const router = useRouter();
  const { data: session } = useSession();
  const today = dateInTimeZone(session?.user.timeZoneId || "UTC");
  const inputRef = useRef<HTMLInputElement>(null);
  const [analysis, setAnalysis] = useState<FoodAnalysis | null>(null);
  const [rows, setRows] = useState<Row[]>([]);
  const [mealType, setMealType] = useState<string>(initialMealType || "SNACK");
  const [date, setDate] = useState(initialDate || today);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const savingRef = useRef(false);
  const { guard, wall } = useProWall({
    title: "Log a meal from a photo",
    message:
      "Pro reads a plate and proposes the entries; you still confirm each one.",
  });
  function setWorking(value: boolean) {
    setBusy(value);
    onBusyChange?.(value);
  }

  async function onPick(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;

    setWorking(true);
    setError("");
    try {
      const result = await analyzeFoodPhoto(file);
      setAnalysis(result);
      setRows(result.foods.map((food) => ({ ...food, include: true })));
    } catch (error) {
      appToast.error(error, "The assistant could not read that photo.");
    } finally {
      setWorking(false);
    }
  }

  function edit(index: number, patch: Partial<Row>) {
    setRows((current) =>
      current.map((row, i) => (i === index ? { ...row, ...patch } : row)),
    );
  }

  const chosen = rows.filter((row) => row.include);

  async function confirm() {
    if (chosen.length === 0 || savingRef.current) return;
    if (
      !date ||
      date > today ||
      rows.some(
        (row) =>
          row.include &&
          (!row.name.trim() ||
            !Number.isFinite(row.portionGrams) ||
            row.portionGrams <= 0 ||
            ![row.calories, row.protein, row.carbs, row.fat].every(
              (value) => Number.isFinite(value) && value >= 0,
            )),
      )
    ) {
      setError(
        "Check the date, food names, weights, and nutrition before logging.",
      );
      return;
    }
    savingRef.current = true;
    setWorking(true);
    setError("");
    let saved = 0;
    try {
      // One entry per food rather than one lump, so the diary stays
      // editable afterwards at the same granularity the photo produced.
      for (const row of chosen) {
        const feedback = await logMeal({
          name: row.name,
          entryDate: date,
          mealType,
          servings: 1,
          amountGrams: row.portionGrams,
          calories: row.calories,
          proteinGrams: row.protein,
          carbsGrams: row.carbs,
          fatGrams: row.fat,
        });
        publishLogFeedback(feedback);
        saved += 1;
        setRows((current) => current.filter((item) => item !== row));
      }

      appToast.success(
        chosen.length === 1 ? "Logged 1 item" : `Logged ${chosen.length} items`,
      );
      setAnalysis(null);
      setRows([]);
      onLogged?.();
      router.refresh();
    } catch (cause) {
      setError(
        `${saved ? `${saved} ${saved === 1 ? "item was" : "items were"} saved. Only the remaining items will be retried. ` : ""}${getErrorMessage(cause, "Could not log those items. Please try again.")}`,
      );
      if (saved) router.refresh();
    } finally {
      savingRef.current = false;
      setWorking(false);
    }
  }

  if (!analysis) {
    return (
      <>
        <button
          type="button"
          onClick={guard(() => inputRef.current?.click())}
          disabled={busy}
          className="flex w-full items-center gap-4 rounded-2xl border border-charcoal-blue-200/70 p-4 text-left transition-colors hover:border-brand-500/40 hover:bg-brand-50/60 disabled:opacity-60 dark:border-white/10 dark:hover:border-brand-400/40 dark:hover:bg-white/5"
        >
          <span className="icon-chip h-11 w-11 shrink-0 text-brand-600 dark:text-brand-400">
            <Icon name="sparkles" size={20} />
          </span>
          <span className="min-w-0">
            <span className="block font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
              {busy ? "Reading the photo…" : "From a photo"}
            </span>
            <span className="block text-sm text-muted-foreground">
              The assistant proposes; you confirm before anything is logged
            </span>
          </span>
        </button>
        <input
          ref={inputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          capture="environment"
          className="hidden"
          onChange={onPick}
        />
        {wall}
      </>
    );
  }

  return (
    <fieldset disabled={busy} className="space-y-4">
      <div className="rounded-2xl border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300">
        Estimated from the photo at {Math.round(analysis.confidence * 100)}%
        confidence. Check the portions before logging.
        {analysis.note && <span className="mt-1 block">{analysis.note}</span>}
      </div>

      <div className="flex gap-2">
        <label className="flex-1 space-y-1">
          <span className="text-xs text-muted-foreground">Meal</span>
          <select
            value={mealType}
            onChange={(e) => setMealType(e.target.value)}
            className="input min-h-11 w-full !py-2 text-sm"
          >
            {MEAL_TYPES.map((type) => (
              <option key={type} value={type}>
                {type.charAt(0) + type.slice(1).toLowerCase()}
              </option>
            ))}
          </select>
        </label>
        <label className="flex-1 space-y-1">
          <span className="text-xs text-muted-foreground">Date</span>
          <input
            type="date"
            value={date}
            max={today}
            onChange={(e) => setDate(e.target.value)}
            className="input min-h-11 w-full !py-2 text-sm"
          />
        </label>
      </div>

      <ul className="space-y-2">
        {rows.map((row, index) => (
          <li
            key={`${row.name}-${index}`}
            className={cn(
              "rounded-2xl border p-3 transition-opacity",
              row.include
                ? "border-charcoal-blue-200 dark:border-white/10"
                : "border-charcoal-blue-200/50 opacity-50 dark:border-white/5",
            )}
          >
            <div className="flex items-center gap-2">
              <label className="flex h-11 w-11 shrink-0 cursor-pointer items-center justify-center">
                <input
                  type="checkbox"
                  checked={row.include}
                  onChange={(e) => edit(index, { include: e.target.checked })}
                  aria-label={`Include ${row.name}`}
                  className="h-4 w-4 shrink-0 accent-verdigris-600"
                />
              </label>
              <input
                value={row.name}
                onChange={(e) => edit(index, { name: e.target.value })}
                className="input min-h-11 min-w-0 flex-1 !py-1.5 text-sm"
                aria-label="Food name"
              />
            </div>

            <div className="mt-2 grid grid-cols-2 gap-3 sm:grid-cols-5">
              {(
                [
                  ["portionGrams", "Weight (g)"],
                  ["calories", "Calories"],
                  ["protein", "Protein (g)"],
                  ["carbs", "Carbs (g)"],
                  ["fat", "Fat (g)"],
                ] as const
              ).map(([field, label]) => (
                <label key={field} className="space-y-0.5">
                  <span className="block text-xs text-muted-foreground">
                    {label}
                  </span>
                  <input
                    type="number"
                    inputMode="decimal"
                    min={field === "portionGrams" ? 0.1 : 0}
                    value={round(row[field])}
                    onChange={(e) =>
                      edit(index, { [field]: Number(e.target.value) || 0 })
                    }
                    className="input min-h-11 w-full !px-2 !py-1 text-sm tabular-nums"
                  />
                </label>
              ))}
            </div>
          </li>
        ))}
      </ul>

      {error && (
        <p
          role="alert"
          className="text-sm text-burnt-peach-700 dark:text-burnt-peach-300"
        >
          {error}
        </p>
      )}
      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => {
            setAnalysis(null);
            setRows([]);
          }}
          disabled={busy}
          className="btn-ghost min-h-11 flex-1"
        >
          Discard
        </button>
        <button
          type="button"
          onClick={confirm}
          disabled={busy || chosen.length === 0}
          className="btn-primary min-h-11 flex-1"
        >
          {chosen.length === 1 ? "Log 1 item" : `Log ${chosen.length} items`}
        </button>
      </div>
    </fieldset>
  );
}
