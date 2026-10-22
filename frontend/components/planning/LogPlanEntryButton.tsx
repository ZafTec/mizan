"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { logPlanEntry, type PlanEntry } from "@/lib/api/mealPlans";
import { appToast } from "@/lib/toast";

/**
 * Writes one plan entry to the diary. The mark turns to "Logged" at once and
 * reverts only if the API refuses; a text entry has no nutrition and cannot
 * be logged, so its button is disabled with the reason.
 */
export default function LogPlanEntryButton({
  planId,
  entry,
  onLogged,
}: {
  planId: string;
  entry: PlanEntry;
  onLogged?: (entryId: string) => void;
}) {
  const router = useRouter();
  const [logged, setLogged] = useState(entry.logged);
  const [busy, setBusy] = useState(false);
  const title =
    entry.kind === "Recipe"
      ? entry.recipeTitle
      : entry.kind === "Food"
        ? entry.foodName
        : entry.text;

  if (entry.kind === "Text")
    return (
      <span className="text-xs log-muted" title="A text line has no nutrition">
        Not loggable
      </span>
    );

  if (logged)
    return (
      <span
        className="text-sm font-medium text-brand-700 dark:text-brand-300"
        aria-label={`${title} logged`}
      >
        Logged
      </span>
    );

  return (
    <button
      type="button"
      className="btn-secondary btn-sm min-h-9"
      disabled={busy}
      aria-label={`Log ${title}`}
      onClick={async () => {
        setBusy(true);
        setLogged(true);
        try {
          const result = await logPlanEntry(planId, entry.id);
          appToast.success(result.message || `${title} logged`);
          onLogged?.(entry.id);
          router.refresh();
        } catch (error) {
          setLogged(false);
          appToast.error(error, "Could not log this entry");
        } finally {
          setBusy(false);
        }
      }}
    >
      Log
    </button>
  );
}
