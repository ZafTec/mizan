"use client";

import { useEffect, useMemo, useReducer, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { useSession } from "@/lib/auth-client";
import { appToast } from "@/lib/toast";
import { GamificationToaster } from "@/components/gamification/GamificationToaster";
import type { GamificationFeedback } from "@/types/gamification";
import type {
  NextSessionDto,
  WorkoutStatsDto,
  WorkoutSummaryDto,
  WorkoutTemplateDto,
} from "@/types/workout";
import {
  createEmptyDraft,
  draftFromTemplate,
  newerStoredDraft,
  parseStoredDraft,
  serializeStoredDraft,
  toLogWorkoutPayload,
  workoutDraftReducer,
  type WorkoutDraft,
} from "@/lib/workouts/draft";
import ActiveWorkout from "./ActiveWorkout";
import StartWorkoutPanel from "./StartWorkoutPanel";
import { TemplateList, WorkoutHistory, WorkoutStats } from "./WorkoutOverview";
import { PostWorkout, ResumePrompt } from "./WorkoutPrompts";

type Tab = "history" | "log" | "templates" | "stats";

export default function WorkoutDashboard({
  initialTab,
  initialHistory,
  initialTemplates,
  initialStats,
  defaultPublishWorkouts,
  sessionOnly = false,
  initialDate,
}: {
  initialTab: Tab;
  initialHistory: { items: WorkoutSummaryDto[]; totalCount: number };
  initialTemplates: WorkoutTemplateDto[];
  initialStats?: WorkoutStatsDto;
  defaultPublishWorkouts: boolean;
  sessionOnly?: boolean;
  initialDate?: string;
}) {
  const router = useRouter();
  const { data: session } = useSession();
  const draftKey = session?.user.id
    ? `mizan-workout-draft:${session.user.id}`
    : null;
  const [tab, setTab] = useState<Tab>(initialTab);
  const [draft, dispatch] = useReducer(workoutDraftReducer, undefined, () =>
    createEmptyDraft(),
  );
  const [resume, setResume] = useState<WorkoutDraft | null>(null);
  const [feedback, setFeedback] = useState<GamificationFeedback>({});
  const [summary, setSummary] = useState<{
    id: string;
    exercises: number;
    sets: number;
    personalRecords: Array<{
      exerciseId: string;
      exerciseName: string;
      weightKg: number;
      previousBestKg?: number | null;
    }>;
  } | null>(null);
  const [clock, setClock] = useState(0);
  const [sessionStarted, setSessionStarted] = useState(false);
  const [saveState, setSaveState] = useState<
    "saving" | "saved" | "local" | "unsaved" | null
  >(null);
  const [finishing, setFinishing] = useState(false);
  const finishingRef = useRef(false);
  const saveRequest = useRef<Promise<unknown> | null>(null);

  useEffect(() => {
    if (!draftKey) return;
    let current = true;
    let localValue: string | null = null;
    try {
      localValue = localStorage.getItem(draftKey);
    } catch {
      /* Server draft still works without browser storage. */
    }
    const local = localValue ? parseStoredDraft(localValue) : null;
    if (localValue && !local) {
      try {
        localStorage.removeItem(draftKey);
      } catch {
        /* Ignore inaccessible local storage. */
      }
    }

    clientApi<{ payload: string; updatedAt: string }>("/api/Workouts/draft")
      .then((value) => {
        if (!current) return;
        const server = parseStoredDraft(value.payload, value.updatedAt);
        setResume(newerStoredDraft(local, server)?.draft ?? null);
      })
      .catch(() => {
        if (current) setResume(local?.draft ?? null);
      });
    return () => {
      current = false;
    };
  }, [draftKey]);

  useEffect(() => {
    if (!draftKey || (!sessionStarted && draft.exercises.length === 0)) return;
    let localSaved = true;
    try {
      localStorage.setItem(draftKey, serializeStoredDraft(draft));
    } catch {
      localSaved = false;
    }
    let current = true;
    const timer = window.setTimeout(() => {
      if (finishingRef.current) return;
      setSaveState("saving");
      // Serialize writes: an older slow request must never overwrite a newer draft.
      saveRequest.current = (saveRequest.current ?? Promise.resolve())
        .then(async () => {
          if (!current || finishingRef.current) return;
          await clientApi("/api/Workouts/draft", {
            method: "PUT",
            body: { payload: JSON.stringify(draft) },
          });
        })
        .then(() => {
          if (current) setSaveState("saved");
        })
        .catch(() => {
          if (current) setSaveState(localSaved ? "local" : "unsaved");
        });
    }, 600);
    return () => {
      current = false;
      window.clearTimeout(timer);
    };
  }, [draft, sessionStarted, draftKey]);

  useEffect(() => {
    if (!draft.restTimer) return;
    const updateClock = () => setClock(Date.now());
    const timer = window.setInterval(updateClock, 1000);
    return () => window.clearInterval(timer);
  }, [draft.restTimer]);

  const restRemaining = useMemo(() => {
    if (!draft.restTimer) return null;
    const startedAt = new Date(draft.restTimer.startedAt).getTime();
    return Math.max(
      0,
      draft.restTimer.seconds -
        Math.floor((Math.max(clock, startedAt) - startedAt) / 1000),
    );
  }, [clock, draft.restTimer]);

  function selectTab(value: Tab) {
    setTab(value);
    if (sessionOnly && value === "log") return;
    router.replace(`/workouts?tab=${value}`, { scroll: false });
  }
  function empty() {
    const next = createEmptyDraft();
    if (initialDate) next.workoutDate = initialDate;
    dispatch({ type: "replace", draft: next });
    setSessionStarted(true);
    selectTab("log");
  }

  async function template(id: string) {
    try {
      const next = draftFromTemplate(
        await clientApi<NextSessionDto>(
          `/api/WorkoutTemplates/${id}/next-session`,
        ),
      );
      if (initialDate) next.workoutDate = initialDate;
      dispatch({
        type: "replace",
        draft: next,
      });
      setSessionStarted(true);
      selectTab("log");
    } catch (error) {
      appToast.error(error, "Could not start template");
    }
  }

  function repeat(workout: WorkoutSummaryDto) {
    const next = createEmptyDraft();
    if (initialDate) next.workoutDate = initialDate;
    next.name = workout.name || "Repeated workout";
    next.exercises = workout.exercises.map((exercise) => ({
      uid: crypto.randomUUID(),
      exerciseId: exercise.exerciseId,
      name: exercise.exerciseName,
      category: exercise.category,
      notes: exercise.notes ?? undefined,
      supersetWithNext: exercise.supersetWithNext,
      restSecondsMin: 60,
      restSecondsMax: 120,
      sets: exercise.sets.map((set) => ({
        uid: crypto.randomUUID(),
        targetReps: set.reps ?? undefined,
        weightKg: set.weightKg ?? 0,
        durationSeconds: set.durationSeconds ?? undefined,
        distanceMeters: set.distanceMeters ?? undefined,
      })),
    }));
    dispatch({ type: "replace", draft: next });
    setSessionStarted(true);
    selectTab("log");
  }

  async function finish() {
    if (finishingRef.current) return;
    finishingRef.current = true;
    setFinishing(true);
    try {
      // A pending autosave must settle before the completed session's draft is removed.
      await saveRequest.current;
      const result = await clientApi<
        {
          id: string;
          totalExercises: number;
          totalSets: number;
          personalRecords: Array<{
            exerciseId: string;
            exerciseName: string;
            weightKg: number;
            previousBestKg?: number | null;
          }>;
        } & GamificationFeedback
      >("/api/Workouts", { method: "POST", body: toLogWorkoutPayload(draft) });
      setFeedback(result);
      setSummary({
        id: result.id,
        exercises: result.totalExercises,
        sets: result.totalSets,
        personalRecords: result.personalRecords,
      });
      if (draftKey) {
        try {
          localStorage.removeItem(draftKey);
        } catch {
          /* Finishing must not fail after the workout was saved. */
        }
      }
      await clientApi("/api/Workouts/draft", { method: "DELETE" }).catch(
        () => {},
      );
      setSessionStarted(false);
      setSaveState(null);
      dispatch({ type: "replace", draft: createEmptyDraft() });
      appToast.success("Workout finished");
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not finish workout");
    } finally {
      finishingRef.current = false;
      setFinishing(false);
    }
  }

  if (summary)
    return (
      <PostWorkout
        summary={summary}
        defaultPublish={defaultPublishWorkouts}
        onClose={() => {
          setSummary(null);
          selectTab("history");
        }}
      />
    );

  return (
    <div className="space-y-6 lg:space-y-8" data-testid="workouts-page">
      <GamificationToaster
        streak={feedback.streak}
        unlockedAchievements={feedback.unlockedAchievements}
      />
      {resume && (
        <ResumePrompt
          onResume={() => {
            dispatch({ type: "replace", draft: resume });
            setSessionStarted(true);
            setResume(null);
            selectTab("log");
          }}
          onDiscard={() => {
            setResume(null);
            if (draftKey) {
              try {
                localStorage.removeItem(draftKey);
              } catch {
                /* The server draft can still be discarded. */
              }
            }
            clientApi("/api/Workouts/draft", { method: "DELETE" }).catch(
              () => {},
            );
          }}
        />
      )}
      <header className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-3xl font-semibold tracking-tight">
            {sessionOnly ? "Workout" : "Workouts"}
          </h1>
          <p className="mt-2 text-sm log-muted" role="status">
            {saveState === "saving"
              ? "Saving session…"
              : saveState === "saved"
                ? "Session saved"
                : saveState === "local"
                  ? "Saved on this device. Could not sync; keep this tab open and retry when connected."
                  : saveState === "unsaved"
                    ? "Could not save this session. Keep this tab open and try again."
                    : sessionStarted || draft.exercises.length > 0
                      ? "Record your sets as you go."
                      : "Choose a starting point, then record your sets as you go."}
          </p>
          {(saveState === "local" || saveState === "unsaved") && (
            <button
              className="text-link"
              onClick={() => dispatch({ type: "replace", draft: { ...draft } })}
            >
              Retry save
            </button>
          )}
        </div>
        {!sessionOnly && (
          <button className="btn-primary" onClick={() => selectTab("log")}>
            Start workout
          </button>
        )}
      </header>
      {!sessionOnly && (
        <nav className="flex max-w-full gap-1 overflow-x-auto border-b">
          {(["history", "log", "templates", "stats"] as Tab[]).map((value) => (
            <button
              key={value}
              className={`whitespace-nowrap rounded-xl px-4 py-2 text-sm font-semibold capitalize ${tab === value ? "bg-white text-charcoal-blue-900 dark:bg-charcoal-blue-950 dark:text-charcoal-blue-50" : "text-charcoal-blue-500"}`}
              onClick={() => selectTab(value)}
            >
              {value}
            </button>
          ))}
        </nav>
      )}
      {tab === "history" && (
        <WorkoutHistory workouts={initialHistory.items} onRepeat={repeat} />
      )}
      {tab === "templates" && (
        <TemplateList templates={initialTemplates} onStart={template} />
      )}
      {tab === "stats" && initialStats && <WorkoutStats stats={initialStats} />}
      {tab === "log" &&
        (!sessionStarted && draft.exercises.length === 0 ? (
          <StartWorkoutPanel
            templates={initialTemplates}
            lastWorkout={initialHistory.items[0]}
            onTemplate={template}
            onRepeat={repeat}
            onEmpty={empty}
          />
        ) : (
          <ActiveWorkout
            draft={draft}
            dispatch={dispatch}
            restRemaining={restRemaining}
            onFinish={finish}
            finishing={finishing}
          />
        ))}
    </div>
  );
}
