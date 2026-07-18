"use client";

import Image from "next/image";
import { useState, type Dispatch } from "react";
import type {
  WeightScope,
  WorkoutDraft,
  WorkoutDraftAction,
} from "@/lib/workouts/draft";
import ExercisePicker from "./ExercisePicker";

export default function ActiveWorkout({
  draft,
  dispatch,
  restRemaining,
  onFinish,
}: {
  draft: WorkoutDraft;
  dispatch: Dispatch<WorkoutDraftAction>;
  restRemaining: number | null;
  onFinish: () => Promise<void>;
}) {
  const [weightScopes, setWeightScopes] = useState<Record<string, WeightScope>>(
    {},
  );

  return (
    <div className="space-y-5">
      <div className="card grid gap-4 p-5 sm:grid-cols-3">
        <label className="sm:col-span-2">
          <span className="label">Workout name</span>
          <input
            className="input"
            value={draft.name}
            onChange={(event) =>
              dispatch({
                type: "set-field",
                field: "name",
                value: event.target.value,
              })
            }
            placeholder="Upper body strength"
          />
        </label>
        <label>
          <span className="label">Date</span>
          <input
            className="input"
            type="date"
            value={draft.workoutDate}
            onChange={(event) =>
              dispatch({
                type: "set-field",
                field: "workoutDate",
                value: event.target.value,
              })
            }
          />
        </label>
        <label>
          <span className="label">Bodyweight (kg)</span>
          <input
            className="input"
            type="number"
            min={20}
            max={500}
            step="0.1"
            value={draft.bodyweightKg ?? ""}
            onChange={(event) =>
              dispatch({
                type: "set-field",
                field: "bodyweightKg",
                value: event.target.value
                  ? Number(event.target.value)
                  : undefined,
              })
            }
          />
        </label>
        <div className="sm:col-span-2 flex items-end">
          <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Tap a set to complete target reps. Tap again to count down to zero
            and clear it.
          </p>
        </div>
      </div>

      {restRemaining !== null && (
        <div
          aria-live="polite"
          className="sticky top-0 z-20 flex items-center justify-between rounded-2xl border border-tuscan-sun-300 bg-tuscan-sun-50 px-4 py-3 shadow-lg dark:border-tuscan-sun-500/30 dark:bg-tuscan-sun-950/80"
        >
          <span className="font-semibold">Rest timer</span>
          <span className="font-mono text-xl font-bold">
            {Math.floor(restRemaining / 60)}:
            {String(restRemaining % 60).padStart(2, "0")}
          </span>
          <button
            className="btn-ghost btn-sm"
            onClick={() => dispatch({ type: "clear-rest-timer" })}
          >
            Skip
          </button>
        </div>
      )}

      <ExercisePicker dispatch={dispatch} />

      {draft.exercises.length === 0 ? (
        <div className="card p-10 text-center">
          <Image
            src="/illustrations/empty-exercise-search.svg"
            alt=""
            width={224}
            height={180}
            className="mx-auto h-auto w-56"
          />
          <h2 className="mt-4 text-lg font-semibold">
            Add your first exercise
          </h2>
          <p className="mt-1 text-sm text-charcoal-blue-500">
            Search the catalog or create a custom movement.
          </p>
        </div>
      ) : (
        draft.exercises.map((exercise, exerciseIndex) => (
          <article key={exercise.uid} className="card overflow-hidden">
            <header className="flex flex-wrap items-center gap-3 border-b border-charcoal-blue-200 p-4 dark:border-white/10">
              <WorkoutIcon
                id={
                  exercise.category === "Cardio"
                    ? "wi-cat-cardio"
                    : "wi-dumbbell"
                }
              />
              <div className="min-w-0 flex-1">
                <h2 className="font-semibold">{exercise.name}</h2>
                <p className="text-xs text-charcoal-blue-500">
                  {exercise.category} · {exercise.sets.length} sets
                </p>
              </div>
              {exerciseIndex < draft.exercises.length - 1 && (
                <button
                  className={`btn-sm ${exercise.supersetWithNext ? "btn-primary" : "btn-ghost"}`}
                  onClick={() =>
                    dispatch({
                      type: "toggle-superset",
                      exerciseUid: exercise.uid,
                    })
                  }
                >
                  Superset
                </button>
              )}
              <button
                className="btn-ghost btn-sm text-red-600"
                onClick={() =>
                  dispatch({
                    type: "remove-exercise",
                    exerciseUid: exercise.uid,
                  })
                }
              >
                Remove
              </button>
            </header>
            <div className="space-y-3 p-4">
              <div className="flex justify-end">
                <label className="flex items-center gap-2 text-xs font-semibold text-charcoal-blue-500">
                  Apply weight to
                  <select
                    className="rounded-xl border border-charcoal-blue-200 bg-white px-2 py-1 text-charcoal-blue-800 dark:border-white/10 dark:bg-charcoal-blue-950 dark:text-charcoal-blue-100"
                    value={weightScopes[exercise.uid] ?? "uncompleted"}
                    onChange={(event) =>
                      setWeightScopes((current) => ({
                        ...current,
                        [exercise.uid]: event.target.value as WeightScope,
                      }))
                    }
                  >
                    <option value="set">This set</option>
                    <option value="uncompleted">Uncompleted sets</option>
                    <option value="all">All sets</option>
                  </select>
                </label>
              </div>
              <div className="grid grid-cols-[2rem_1fr_1fr_2.75rem] gap-2 px-2 text-xs font-semibold uppercase tracking-wide text-charcoal-blue-500">
                <span>Set</span>
                <span>Weight</span>
                <span>Reps</span>
                <span>Done</span>
              </div>
              {exercise.sets.map((set, index) => (
                <div
                  key={set.uid}
                  className={`grid grid-cols-[2rem_1fr_1fr_2.75rem] items-center gap-2 rounded-2xl border p-2 ${set.completedAt ? "border-brand-400 bg-brand-50 dark:border-brand-500/30 dark:bg-brand-950/40" : "border-charcoal-blue-200 dark:border-white/10"}`}
                >
                  <span className="text-center text-sm font-semibold text-charcoal-blue-500">
                    {index + 1}
                  </span>
                  <input
                    aria-label={`Weight for set ${index + 1}`}
                    className="input !py-2 text-center"
                    type="number"
                    min={0}
                    max={1000}
                    step="0.5"
                    value={set.weightKg}
                    onChange={(event) =>
                      dispatch({
                        type: "apply-weight",
                        exerciseUid: exercise.uid,
                        setUid: set.uid,
                        weightKg: Number(event.target.value),
                        scope: weightScopes[exercise.uid] ?? "uncompleted",
                      })
                    }
                  />
                  <input
                    aria-label={`Reps for set ${index + 1}`}
                    className="input !py-2 text-center"
                    type="number"
                    min={0}
                    max={1000}
                    value={set.repsCompleted ?? set.targetReps ?? ""}
                    onChange={(event) =>
                      dispatch({
                        type: "edit-set",
                        exerciseUid: exercise.uid,
                        setUid: set.uid,
                        patch: {
                          repsCompleted: event.target.value
                            ? Number(event.target.value)
                            : undefined,
                        },
                      })
                    }
                  />
                  <button
                    aria-label={`Cycle set ${index + 1}`}
                    className={`press-feedback flex h-11 w-11 items-center justify-center rounded-2xl ${set.completedAt ? "bg-brand-600 text-white" : "bg-charcoal-blue-100 text-charcoal-blue-500 dark:bg-charcoal-blue-900"}`}
                    onClick={() =>
                      dispatch({
                        type: "cycle-reps",
                        exerciseUid: exercise.uid,
                        setUid: set.uid,
                        now: new Date().toISOString(),
                      })
                    }
                  >
                    <WorkoutIcon id="wi-rep-check" />
                  </button>
                </div>
              ))}
              <div className="flex flex-wrap gap-2">
                <button
                  className="btn-secondary btn-sm"
                  onClick={() =>
                    dispatch({ type: "add-set", exerciseUid: exercise.uid })
                  }
                >
                  Add set
                </button>
                {exercise.sets.length > 1 && (
                  <button
                    className="btn-ghost btn-sm"
                    onClick={() =>
                      dispatch({
                        type: "remove-set",
                        exerciseUid: exercise.uid,
                        setUid: exercise.sets.at(-1)!.uid,
                      })
                    }
                  >
                    Remove last
                  </button>
                )}
              </div>
              <input
                className="input"
                value={exercise.notes ?? ""}
                onChange={(event) =>
                  dispatch({
                    type: "set-exercise-notes",
                    exerciseUid: exercise.uid,
                    notes: event.target.value,
                  })
                }
                placeholder="Exercise notes"
                maxLength={500}
              />
            </div>
          </article>
        ))
      )}

      <button
        className="btn-primary w-full"
        disabled={draft.exercises.length === 0}
        onClick={onFinish}
      >
        Finish workout
      </button>
    </div>
  );
}

function WorkoutIcon({ id }: { id: string }) {
  return (
    <svg className="size-5" aria-hidden="true">
      <use href={`/illustrations/icons-workout.svg#${id}`} />
    </svg>
  );
}
