"use client";

import { useEffect, useMemo, useReducer, useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
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
}: {
  initialTab: Tab;
  initialHistory: { items: WorkoutSummaryDto[]; totalCount: number };
  initialTemplates: WorkoutTemplateDto[];
  initialStats: WorkoutStatsDto;
  defaultPublishWorkouts: boolean;
}) {
  const router = useRouter();
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

  useEffect(() => {
    const localValue = localStorage.getItem("mizan-workout-draft");
    const local = localValue ? parseStoredDraft(localValue) : null;
    if (localValue && !local) localStorage.removeItem("mizan-workout-draft");

    clientApi<{ payload: string; updatedAt: string }>("/api/Workouts/draft")
      .then((value) => {
        const server = parseStoredDraft(value.payload, value.updatedAt);
        setResume(newerStoredDraft(local, server)?.draft ?? null);
      })
      .catch(() => setResume(local?.draft ?? null));
  }, []);

  useEffect(() => {
    if (draft.exercises.length === 0) return;
    localStorage.setItem("mizan-workout-draft", serializeStoredDraft(draft));
    const timer = window.setTimeout(
      () =>
        clientApi("/api/Workouts/draft", {
          method: "PUT",
          body: { payload: JSON.stringify(draft) },
        }).catch(() => {}),
      600,
    );
    return () => window.clearTimeout(timer);
  }, [draft]);

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
    router.replace(`/workouts?tab=${value}`, { scroll: false });
  }
  function empty() {
    dispatch({ type: "replace", draft: createEmptyDraft() });
    selectTab("log");
  }

  async function template(id: string) {
    try {
      dispatch({
        type: "replace",
        draft: draftFromTemplate(
          await clientApi<NextSessionDto>(
            `/api/WorkoutTemplates/${id}/next-session`,
          ),
        ),
      });
      selectTab("log");
    } catch (error) {
      appToast.error(error, "Could not start template");
    }
  }

  function repeat(workout: WorkoutSummaryDto) {
    const next = createEmptyDraft();
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
    selectTab("log");
  }

  async function finish() {
    try {
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
      localStorage.removeItem("mizan-workout-draft");
      await clientApi("/api/Workouts/draft", { method: "DELETE" }).catch(
        () => {},
      );
      dispatch({ type: "replace", draft: createEmptyDraft() });
      appToast.success("Workout finished");
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not finish workout");
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
            setResume(null);
            selectTab("log");
          }}
          onDiscard={() => {
            setResume(null);
            localStorage.removeItem("mizan-workout-draft");
            clientApi("/api/Workouts/draft", { method: "DELETE" }).catch(
              () => {},
            );
          }}
        />
      )}
      <header className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="eyebrow">Training</p>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight sm:text-4xl">
            Workouts
          </h1>
          <p className="mt-2 text-sm text-charcoal-blue-500">
            Plan, train, recover, and see the work compound.
          </p>
        </div>
        <button className="btn-primary" onClick={() => selectTab("log")}>
          Start workout
        </button>
      </header>
      <nav className="flex max-w-full gap-1 overflow-x-auto rounded-2xl bg-charcoal-blue-100 p-1 dark:bg-charcoal-blue-900/70">
        {(["history", "log", "templates", "stats"] as Tab[]).map((value) => (
          <button
            key={value}
            className={`whitespace-nowrap rounded-xl px-4 py-2 text-sm font-semibold capitalize ${tab === value ? "bg-white text-charcoal-blue-900 shadow-sm dark:bg-charcoal-blue-950 dark:text-charcoal-blue-50" : "text-charcoal-blue-500"}`}
            onClick={() => selectTab(value)}
          >
            {value}
          </button>
        ))}
      </nav>
      {tab === "history" && (
        <WorkoutHistory workouts={initialHistory.items} onRepeat={repeat} />
      )}
      {tab === "templates" && (
        <TemplateList templates={initialTemplates} onStart={template} />
      )}
      {tab === "stats" && <WorkoutStats stats={initialStats} />}
      {tab === "log" &&
        (draft.exercises.length === 0 ? (
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
          />
        ))}
    </div>
  );
}
