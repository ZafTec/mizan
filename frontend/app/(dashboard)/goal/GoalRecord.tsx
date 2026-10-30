"use client";

import { useCallback, useEffect, useState } from "react";
import ConfirmationModal from "@/components/ConfirmationModal";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import type { components } from "@/types/api.generated";

type Goal = components["schemas"]["UserGoalDto"];
type CheckIn = components["schemas"]["GoalProgressEntryDto"];
type History = components["schemas"]["GoalProgressHistoryDto"];

type Pending =
  | { kind: "goal"; id: string }
  | { kind: "check-in"; id: string; date: string };

const dateLabel = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
  });

/**
 * The active goal and its recent check-ins, each with a delete action. The
 * check-in values are shown as the API stored them.
 */
export default function GoalRecord({
  refreshKey,
  onGoalDeleted,
}: {
  refreshKey: unknown;
  onGoalDeleted: () => void;
}) {
  const [goal, setGoal] = useState<Goal | null>(null);
  const [checkIns, setCheckIns] = useState<CheckIn[]>([]);
  const [pending, setPending] = useState<Pending | null>(null);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    try {
      const [current, history] = await Promise.all([
        clientApi<Goal | null>("/api/Goals"),
        clientApi<History>("/api/Goals/progress?days=90"),
      ]);
      setGoal(current ?? null);
      setCheckIns([...(history?.progressEntries ?? [])].reverse());
    } catch {
      setGoal(null);
      setCheckIns([]);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const confirm = async () => {
    if (!pending) return;
    setDeleting(true);
    try {
      if (pending.kind === "goal") {
        await clientApi(`/api/Goals/${pending.id}`, { method: "DELETE" });
        appToast.success("Goal deleted");
        onGoalDeleted();
      } else {
        await clientApi(`/api/Goals/progress/${pending.id}`, {
          method: "DELETE",
        });
        appToast.success("Check-in deleted");
      }
      await load();
    } catch (error) {
      appToast.error(error, "Could not delete it. Try again.");
    } finally {
      setDeleting(false);
      setPending(null);
    }
  };

  if (!goal) return null;

  return (
    <section
      aria-labelledby="goal-record-title"
      className="card space-y-4 p-4 sm:p-6"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2
            id="goal-record-title"
            className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100"
          >
            Current goal and check-ins
          </h2>
          <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Set {dateLabel(goal.createdAt.slice(0, 10))}
          </p>
        </div>
        <button
          type="button"
          className="btn-secondary-destructive"
          onClick={() => setPending({ kind: "goal", id: goal.id })}
        >
          Delete goal
        </button>
      </div>

      {checkIns.length === 0 ? (
        <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          No check-ins yet.
        </p>
      ) : (
        <ul className="divide-y divide-charcoal-blue-100 dark:divide-charcoal-blue-800">
          {checkIns.map((entry) => (
            <li
              key={entry.id}
              className="flex items-center justify-between gap-3 py-2"
            >
              <div className="min-w-0 text-sm">
                <p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">
                  {dateLabel(entry.date)}
                </p>
                <p className="truncate text-charcoal-blue-500 dark:text-charcoal-blue-400">
                  {entry.actualCalories} kcal
                  {entry.actualWeight != null && ` · ${entry.actualWeight} kg`}
                  {entry.notes && ` · ${entry.notes}`}
                </p>
              </div>
              <button
                type="button"
                className="btn-ghost-destructive btn-sm shrink-0"
                aria-label={`Delete check-in for ${dateLabel(entry.date)}`}
                onClick={() =>
                  setPending({ kind: "check-in", id: entry.id, date: entry.date })
                }
              >
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}

      <ConfirmationModal
        isOpen={pending !== null}
        onClose={() => setPending(null)}
        onConfirm={confirm}
        title={pending?.kind === "goal" ? "Delete this goal?" : "Delete this check-in?"}
        message={
          pending?.kind === "goal"
            ? "The goal and all its check-ins are removed. You will have no daily targets until you set a new goal."
            : `The check-in for ${pending ? dateLabel(pending.date) : ""} is removed. Your diary is not changed.`
        }
        confirmText="Delete"
        isDanger
        isLoading={deleting}
      />
    </section>
  );
}
