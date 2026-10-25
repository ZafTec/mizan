"use client";

import { useId } from "react";
import FoodSearch from "./FoodSearch";
import {
  MACRO_MODES,
  MACRO_MODE_LABELS,
  PREP_STYLES,
  type RulesFormErrors,
  type RulesFormValues,
} from "@/lib/planning/rules";

/**
 * The rules a plan is held to, as one form section. Used on a new plan and
 * when editing one. Validation lives in lib/planning/rules.ts; this only
 * renders the fields and the first error for each.
 */
export default function RulesFields({
  values,
  errors,
  durationDays,
  onChange,
}: {
  values: RulesFormValues;
  errors: RulesFormErrors;
  durationDays: number;
  onChange: (next: RulesFormValues) => void;
}) {
  const id = useId();
  const set = <K extends keyof RulesFormValues>(
    key: K,
    value: RulesFormValues[K],
  ) => onChange({ ...values, [key]: value });
  const field = (key: keyof RulesFormValues) => `${id}-${key}`;
  const error = (key: keyof RulesFormValues) =>
    errors[key] ? (
      <p id={`${field(key)}-error`} className="input-error mt-1 text-sm">
        {errors[key]}
      </p>
    ) : null;
  const custom = values.macroMode === "Custom";
  const days = Array.from({ length: Math.max(1, durationDays) }, (_, i) => i);

  return (
    <fieldset className="space-y-5">
      <legend className="mb-3 text-base font-semibold">Rules</legend>
      <div>
        <label htmlFor={field("macroMode")} className="label">
          Macro mode
        </label>
        <select
          id={field("macroMode")}
          className="input w-full"
          value={values.macroMode}
          onChange={(event) => set("macroMode", event.target.value)}
        >
          {MACRO_MODES.map((mode) => (
            <option key={mode} value={mode}>
              {MACRO_MODE_LABELS[mode]}
            </option>
          ))}
        </select>
        <p className="mt-1 text-xs log-muted">
          {values.macroMode === "Keto"
            ? "Carbs are capped at 30 g a day unless you set a cap."
            : values.macroMode === "HighProtein"
              ? "Protein gets a daily target from your goal."
              : custom
                ? "Set the daily targets below."
                : "Daily targets are optional."}
        </p>
      </div>
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        {(
          [
            ["targetCalories", "Calories (kcal)"],
            ["targetProtein", "Protein (g)"],
            ["maxCarbs", "Max carbs (g)"],
            ["targetFat", "Fat (g)"],
          ] as const
        ).map(([key, label]) => (
          <div key={key}>
            <label htmlFor={field(key)} className="label">
              {label}
            </label>
            <input
              id={field(key)}
              type="number"
              inputMode="decimal"
              min={0}
              className="input num w-full"
              value={values[key]}
              onChange={(event) => set(key, event.target.value)}
              aria-invalid={errors[key] ? true : undefined}
              aria-describedby={errors[key] ? `${field(key)}-error` : undefined}
            />
            {error(key)}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label htmlFor={field("eatingWindowStart")} className="label">
            Eating window starts
          </label>
          <input
            id={field("eatingWindowStart")}
            type="time"
            className="input w-full"
            value={values.eatingWindowStart}
            onChange={(event) => set("eatingWindowStart", event.target.value)}
          />
          {error("eatingWindowStart")}
        </div>
        <div>
          <label htmlFor={field("eatingWindowEnd")} className="label">
            Eating window ends
          </label>
          <input
            id={field("eatingWindowEnd")}
            type="time"
            className="input w-full"
            value={values.eatingWindowEnd}
            onChange={(event) => set("eatingWindowEnd", event.target.value)}
            aria-invalid={errors.eatingWindowEnd ? true : undefined}
          />
          {error("eatingWindowEnd")}
        </div>
      </div>
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
        <div>
          <label htmlFor={field("weeklyBudget")} className="label">
            Weekly budget
          </label>
          <input
            id={field("weeklyBudget")}
            type="number"
            inputMode="decimal"
            min={0}
            className="input num w-full"
            value={values.weeklyBudget}
            onChange={(event) => set("weeklyBudget", event.target.value)}
          />
          {error("weeklyBudget")}
        </div>
        <div>
          <label htmlFor={field("dailyBudget")} className="label">
            Daily budget
          </label>
          <input
            id={field("dailyBudget")}
            type="number"
            inputMode="decimal"
            min={0}
            className="input num w-full"
            value={values.dailyBudget}
            onChange={(event) => set("dailyBudget", event.target.value)}
            aria-invalid={errors.dailyBudget ? true : undefined}
            aria-describedby={
              errors.dailyBudget ? `${field("dailyBudget")}-error` : undefined
            }
          />
          {error("dailyBudget")}
        </div>
        <div>
          <label htmlFor={field("currency")} className="label">
            Currency
          </label>
          <input
            id={field("currency")}
            type="text"
            maxLength={3}
            placeholder="ETB"
            className="input w-full uppercase"
            value={values.currency}
            onChange={(event) => set("currency", event.target.value)}
            aria-invalid={errors.currency ? true : undefined}
          />
          {error("currency")}
        </div>
      </div>
      <div>
        <label htmlFor={field("prepStyle")} className="label">
          Cooking
        </label>
        <select
          id={field("prepStyle")}
          className="input w-full"
          value={values.prepStyle}
          onChange={(event) => set("prepStyle", event.target.value)}
        >
          {PREP_STYLES.map((style) => (
            <option key={style} value={style}>
              {style === "Daily" ? "Cook every day" : "Cook in batches"}
            </option>
          ))}
        </select>
        {values.prepStyle === "Batch" && (
          <div className="mt-3">
            <p className="label">Cooking days</p>
            <div
              className="flex flex-wrap gap-2"
              role="group"
              aria-label="Cooking days"
            >
              {days.map((day) => {
                const checked = values.batchDays.includes(day);
                return (
                  <label
                    key={day}
                    className={`flex min-h-11 cursor-pointer items-center gap-2 border px-3 text-sm ${checked ? "border-foreground" : "border-border"}`}
                  >
                    <input
                      type="checkbox"
                      className="h-4 w-4 accent-brand-700"
                      checked={checked}
                      onChange={(event) =>
                        set(
                          "batchDays",
                          event.target.checked
                            ? [...values.batchDays, day].sort((a, b) => a - b)
                            : values.batchDays.filter((d) => d !== day),
                        )
                      }
                    />
                    Day {day + 1}
                  </label>
                );
              })}
            </div>
            {error("batchDays")}
          </div>
        )}
      </div>
      <FoodSearch
        label="Allowed foods (everything else is a violation)"
        value={values.allowedFoods}
        onChange={(next) => set("allowedFoods", next)}
        error={errors.allowedFoods}
      />
      <FoodSearch
        label="Forbidden foods"
        value={values.forbiddenFoods}
        onChange={(next) => set("forbiddenFoods", next)}
        error={errors.forbiddenFoods}
      />
    </fieldset>
  );
}
