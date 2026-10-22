"use client";

import Image from "next/image";
import type {
  WorkoutStatsDto,
  WorkoutSummaryDto,
  WorkoutTemplateDto,
} from "@/types/workout";

export function WorkoutHistory({
  workouts,
  onRepeat,
}: {
  workouts: WorkoutSummaryDto[];
  onRepeat: (workout: WorkoutSummaryDto) => void;
}) {
  if (workouts.length === 0) {
    return (
      <Empty
        art="empty-workouts.svg"
        title="No workouts yet"
        body="Start with a built-in program or an empty workout."
      />
    );
  }

  return (
    <div className="space-y-3">
      {workouts.map((workout) => {
        const sets = workout.exercises.reduce(
          (total, exercise) => total + exercise.sets.length,
          0,
        );
        const volume = workout.exercises
          .flatMap((exercise) => exercise.sets)
          .reduce(
            (total, set) => total + (set.weightKg ?? 0) * (set.reps ?? 0),
            0,
          );
        return (
          <details key={workout.id} className="group border-b border-border">
            <summary className="flex min-h-14 cursor-pointer list-none items-center gap-4 py-3">
              <span className="min-w-0 flex-1">
                <strong className="block truncate">
                  {workout.name || "Workout"}
                </strong>
                <span className="num text-sm log-muted">
                  {new Date(
                    `${workout.workoutDate}T00:00:00`,
                  ).toLocaleDateString(undefined, {
                    day: "numeric",
                    month: "short",
                  })}{" "}
                  · {workout.exercises.length}{" "}
                  {workout.exercises.length === 1 ? "exercise" : "exercises"} ·{" "}
                  {sets} sets
                </span>
              </span>
              <span className="num hidden text-sm sm:block">
                <span className="font-semibold">
                  {Math.round(volume).toLocaleString()}
                </span>{" "}
                <span className="log-muted">kg</span>
              </span>
              <button
                className="btn-ghost btn-sm"
                onClick={(event) => {
                  event.preventDefault();
                  onRepeat(workout);
                }}
              >
                Repeat
              </button>
            </summary>
            <ul className="border-t border-border pb-3 pl-1">
              {workout.exercises.map((exercise) => (
                <li
                  key={exercise.id}
                  className="flex items-baseline justify-between gap-4 py-2 text-sm"
                >
                  <span className="font-medium">{exercise.exerciseName}</span>
                  <span className="num text-right log-muted">
                    {exercise.sets
                      .map(
                        (set) => `${set.weightKg ?? 0} kg × ${set.reps ?? 0}`,
                      )
                      .join(" · ")}
                  </span>
                </li>
              ))}
            </ul>
          </details>
        );
      })}
    </div>
  );
}

export function TemplateList({
  templates,
  onStart,
}: {
  templates: WorkoutTemplateDto[];
  onStart: (id: string) => void;
}) {
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {templates.map((template) => (
        <article key={template.id} className="card-hover p-5">
          <div className="flex items-start justify-between">
            <span className="icon-chip h-11 w-11 text-brand-700">
              <WorkoutIcon id="wi-template" />
            </span>
            {template.isBuiltIn && (
              <span
                className="rounded-full bg-tuscan
-sun-100 px-2 py-1 text-xs font-semibold text-tuscan-sun-800"
              >
                Built in
              </span>
            )}
          </div>
          <h2 className="mt-4 text-lg font-semibold">{template.name}</h2>
          <p className="mt-1 text-sm text-charcoal-blue-500">
            {template.programName || "Custom template"}
            {template.programName ? ` · Session ${template.sessionOrder}` : ""}
          </p>
          {template.notes && (
            <p className="mt-3 line-clamp-2 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
              {template.notes}
            </p>
          )}
          <button
            className="btn-primary mt-5 w-full"
            onClick={() => onStart(template.id)}
          >
            Start workout
          </button>
        </article>
      ))}
    </div>
  );
}

export function WorkoutStats({ stats }: { stats: WorkoutStatsDto }) {
  const maxSets = Math.max(
    1,
    ...stats.perMuscleGroup.map((group) => group.sets),
  );
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-5">
        <Stat
          label="Workouts / week"
          value={stats.workoutsPerWeek.toFixed(1)}
        />
        <Stat label="Sets / week" value={stats.setsPerWeek.toFixed(1)} />
        <Stat
          label="Average session"
          value={`${Math.round(stats.averageSessionMinutes)} min`}
        />
        <Stat
          label="Total volume"
          value={`${Math.round(stats.totalVolumeKg).toLocaleString()} kg`}
        />
        <Stat
          label="Personal records"
          value={stats.personalRecordCount.toLocaleString()}
        />
      </div>
      {stats.heaviestLift && (
        <div className="card flex items-center gap-5 p-6">
          <Image
            src="/illustrations/celebration-pr.svg"
            alt=""
            width={128}
            height={128}
            className="h-auto w-32"
          />
          <div>
            <p className="eyebrow">Heaviest lift</p>
            <h2 className="mt-2 text-2xl font-semibold">
              {stats.heaviestLift.name} · {stats.heaviestLift.weightKg} kg
            </h2>
            <p className="text-sm text-charcoal-blue-500">
              {new Date(
                `${stats.heaviestLift.date}T00:00:00`,
              ).toLocaleDateString()}
            </p>
          </div>
        </div>
      )}
      <div className="card p-6">
        <h2 className="section-title">Weekly muscle coverage</h2>
        <div className="mt-5 space-y-3">
          {stats.perMuscleGroup.map((group) => (
            <div key={group.muscleGroup}>
              <div className="mb-1 flex justify-between text-sm">
                <span>{group.muscleGroup}</span>
                <strong>{group.sets} sets</strong>
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-charcoal-blue-100 dark:bg-charcoal-blue-900">
                <div
                  className="h-full rounded-full bg-brand-600"
                  style={{ width: `${(group.sets / maxSets) * 100}%` }}
                />
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="card p-5">
      <p className="text-xs uppercase tracking-wide text-charcoal-blue-500">
        {label}
      </p>
      <p className="mt-2 text-2xl font-bold">{value}</p>
    </div>
  );
}

function Empty({
  art,
  title,
  body,
}: {
  art: string;
  title: string;
  body: string;
}) {
  return (
    <div className="card p-10 text-center">
      <Image
        src={`/illustrations/${art}`}
        alt=""
        width={224}
        height={180}
        className="mx-auto h-auto w-56"
      />
      <h2 className="mt-4 text-xl font-semibold">{title}</h2>
      <p className="mt-1 text-sm text-charcoal-blue-500">{body}</p>
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
