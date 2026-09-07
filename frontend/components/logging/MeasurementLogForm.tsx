"use client";

import { useId, useRef, useState, type FormEvent } from "react";
import { clientApi } from "@/lib/api.client";
import { publishLogFeedback } from "@/lib/log-feedback";
import type { GamificationFeedback } from "@/types/gamification";
import { getErrorMessage } from "@/lib/toast";
import { dateInTimeZone } from "@/lib/log-date";

const SECONDARY_MEASUREMENTS = [
  ["bodyFatPercentage", "Body fat (%)", 100],
  ["muscleMassKg", "Muscle mass (kg)", 1000],
  ["waistCm", "Waist (cm)", 500],
  ["hipsCm", "Hips (cm)", 500],
  ["chestCm", "Chest (cm)", 500],
  ["leftArmCm", "Left arm (cm)", 500],
  ["rightArmCm", "Right arm (cm)", 500],
  ["leftThighCm", "Left thigh (cm)", 500],
  ["rightThighCm", "Right thigh (cm)", 500],
] as const;

export default function MeasurementLogForm({
  initialDate,
  timeZone = "UTC",
  onSaved,
  onBusyChange,
}: {
  initialDate?: string;
  timeZone?: string;
  onSaved: (message: string) => void;
  onBusyChange: (busy: boolean) => void;
}) {
  const id = useId();
  const today = dateInTimeZone(timeZone);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const savingRef = useRef(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (savingRef.current) return;
    const data = new FormData(event.currentTarget);
    const values = Object.fromEntries(
      ["weightKg", ...SECONDARY_MEASUREMENTS.map(([name]) => name)].map(
        (name) => [name, data.get(name) ? Number(data.get(name)) : null],
      ),
    );
    if (!Object.values(values).some((value) => value !== null)) {
      setError("Enter your weight or at least one body measurement.");
      return;
    }
    savingRef.current = true;
    setBusy(true);
    onBusyChange(true);
    setError("");
    try {
      const feedback = await clientApi<GamificationFeedback>("/api/BodyMeasurements", {
        method: "POST",
        body: {
          date: data.get("date"),
          ...values,
          notes: String(data.get("notes") ?? "").trim() || null,
        },
      });
      publishLogFeedback(feedback);
      onSaved("Measurement logged");
    } catch (cause) {
      setError(
        getErrorMessage(
          cause,
          "Could not save your measurement. Please try again.",
        ),
      );
    } finally {
      savingRef.current = false;
      setBusy(false);
      onBusyChange(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-5">
      <fieldset disabled={busy} className="space-y-5">
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <label htmlFor={`${id}-date`} className="block text-sm font-medium">
              Date
            </label>
            <input
              id={`${id}-date`}
              name="date"
              type="date"
              required
              max={today}
              defaultValue={initialDate || today}
              className="input min-h-11 w-full min-w-0"
            />
          </div>
          <div className="space-y-1.5">
            <label
              htmlFor={`${id}-weightKg`}
              className="block text-sm font-medium"
            >
              Weight (kg)
            </label>
            <input
              id={`${id}-weightKg`}
              name="weightKg"
              type="number"
              inputMode="decimal"
              min="0.1"
              max="1000"
              step="0.1"
              placeholder="e.g. 72.5"
              className="input min-h-11 w-full"
            />
          </div>
        </div>
        <details className="border-y border-border">
          <summary className="cursor-pointer py-4 text-sm font-medium">
            More body measurements
          </summary>
          <div className="grid grid-cols-2 gap-3 pb-5">
            {SECONDARY_MEASUREMENTS.map(([name, label, maximum]) => (
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
                  inputMode="decimal"
                  min="0.1"
                  max={maximum}
                  step="0.1"
                  className="input min-h-11 w-full"
                />
              </div>
            ))}
          </div>
        </details>
        <div className="space-y-1.5">
          <label htmlFor={`${id}-notes`} className="block text-sm font-medium">
            Notes{" "}
            <span className="font-normal text-charcoal-blue-700 dark:text-charcoal-blue-300">
              (optional)
            </span>
          </label>
          <textarea
            id={`${id}-notes`}
            name="notes"
            rows={2}
            maxLength={1000}
            className="input w-full resize-y"
          />
        </div>
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
        {busy ? "Saving…" : "Log measurement"}
      </button>
    </form>
  );
}
