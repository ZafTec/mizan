"use client";

import { useId, useRef, useState } from "react";
import { Camera, Sparkles } from "lucide-react";
import {
  analyzeFoodPhoto,
  analyzeFoodText,
  type FoodAnalysis,
} from "@/lib/api/food-photo";
import { getErrorMessage } from "@/lib/toast";
import Link from "next/link";
import { useSubscription } from "@/lib/hooks/useSubscription";

export const MAX_DESCRIPTION = 600;

/**
 * The way most meals should be logged: say what you ate, or show it. The assistant answers with a proposal that
 * lands in the meal below for the person to check and change. Nothing is saved from here.
 */
export default function AiMealInput({
  disabled,
  onProposal,
  onBusyChange,
}: {
  disabled?: boolean;
  onProposal: (analysis: FoodAnalysis, source: "text" | "photo") => void;
  onBusyChange?: (busy: boolean) => void;
}) {
  const id = useId();
  const fileInput = useRef<HTMLInputElement>(null);
  const [text, setText] = useState("");
  const [busy, setBusy] = useState<"text" | "photo" | null>(null);
  const [error, setError] = useState("");
  const { isPro, loading } = useSubscription();
  const [walled, setWalled] = useState(false);

  // Gated at the moment of the attempt, as everywhere else. The notice is inline rather than a dialog: this sits
  // inside the logging sheet, which holds focus, so a second dialog opened from here could not be reached.
  // While the plan is still loading the action runs, because the server decides.
  const guard =
    <A extends unknown[]>(action: (...args: A) => void) =>
    (...args: A) => {
      if (isPro || loading) action(...args);
      else setWalled(true);
    };

  async function run(source: "text" | "photo", work: () => Promise<FoodAnalysis>) {
    setBusy(source);
    onBusyChange?.(true);
    setError("");
    try {
      const analysis = await work();
      if (analysis.foods.length === 0) {
        setError(
          source === "photo"
            ? "I could not tell what that was. Try describing it instead."
            : "I could not find any food in that. Try naming what you ate.",
        );
        return;
      }
      onProposal(analysis, source);
      if (source === "text") setText("");
    } catch (cause) {
      setError(
        getErrorMessage(
          cause,
          source === "photo"
            ? "The assistant could not read that photo."
            : "The assistant could not read that. Try again.",
        ),
      );
    } finally {
      setBusy(null);
      onBusyChange?.(false);
    }
  }

  const description = text.trim();
  const estimate = guard(() => {
    if (description) void run("text", () => analyzeFoodText(description));
  });

  return (
    <section
      aria-label="Describe your meal"
      className="space-y-3 rounded-md border border-brand-600/30 bg-brand-50/50 p-4 dark:border-brand-400/30 dark:bg-brand-950/20"
    >
      <div className="flex items-center gap-2">
        <Sparkles
          aria-hidden="true"
          size={18}
          strokeWidth={1.7}
          className="shrink-0 text-brand-700 dark:text-brand-300"
        />
        <label htmlFor={id} className="text-sm font-semibold">
          Tell me what you ate
        </label>
      </div>
      <textarea
        id={id}
        value={text}
        rows={2}
        maxLength={MAX_DESCRIPTION}
        disabled={disabled || busy !== null}
        placeholder="Two eggs, toast with butter and a black coffee"
        onChange={(event) => setText(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter" && (event.metaKey || event.ctrlKey)) {
            event.preventDefault();
            estimate();
          }
        }}
        className="input min-h-16 w-full resize-y"
        aria-describedby={`${id}-hint`}
      />
      <p
        id={`${id}-hint`}
        className="text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300"
      >
        The assistant proposes and you check it before anything is saved.
      </p>
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
          disabled={disabled || busy !== null}
          onClick={guard(() => fileInput.current?.click())}
          className="btn-secondary min-h-11 shrink-0"
        >
          <Camera aria-hidden="true" size={16} strokeWidth={1.7} />
          {busy === "photo" ? "Reading…" : "Photo"}
        </button>
        <button
          type="button"
          disabled={disabled || busy !== null || description.length === 0}
          onClick={estimate}
          className="btn-primary min-h-11 flex-1 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <Sparkles aria-hidden="true" size={16} strokeWidth={1.7} />
          {busy === "text" ? "Estimating…" : "Estimate meal"}
        </button>
      </div>
      <input
        ref={fileInput}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        capture="environment"
        className="hidden"
        aria-label="Meal photo"
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = "";
          if (file) void run("photo", () => analyzeFoodPhoto(file));
        }}
      />
      {walled && (
        <div role="note" className="space-y-2 border-t border-border pt-3 text-sm">
          <h3 className="font-semibold">Log a meal in plain words</h3>
          <p className="text-charcoal-blue-700 dark:text-charcoal-blue-300">
            Pro turns a sentence or a photo into a meal for you to check.
            Searching foods and recipes stays free.
          </p>
          <div className="flex flex-wrap gap-2">
            <Link href="/billing?checkout=pro" className="btn-primary min-h-11">
              <Sparkles aria-hidden="true" size={16} strokeWidth={1.7} />
              See Pro
            </Link>
            <button
              type="button"
              onClick={() => setWalled(false)}
              className="btn-ghost min-h-11"
            >
              Not now
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
