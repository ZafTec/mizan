import Link from "next/link";
import { Suspense } from "react";
import { Plus, Dumbbell, Ruler, ArrowRight } from "lucide-react";
import { getUserServer } from "@/helper/session";
import {
  getDayDiary,
  getLogGoal,
  getLogMeasurements,
  getLogWorkouts,
  type LogMeal,
} from "@/data/log";
import { dateInTimeZone, formatLogDate, isLogDate } from "@/lib/log-date";
import { formatMealType, MEAL_TYPES } from "@/lib/utils/mealType";
import DateNavigator from "@/components/log/DateNavigator";
import DeleteLogEntry from "@/components/log/DeleteLogEntry";
import { LogEntryButton } from "@/components/logging/LogEntryButton";
import SaveMealAsRecipe from "@/components/logging/SaveMealAsRecipe";
import ResumeWorkoutBanner from "@/components/context/ResumeWorkoutBanner";
import TrainerStrip from "@/components/context/TrainerStrip";

export const metadata = { title: "Today · Mizan" };

function MealRow({ entry }: { entry: LogMeal }) {
  return (
    <div className="meal-row">
      <div className="min-w-0">
        <p className="font-medium break-words">{entry.name}</p>
        <p className="log-muted text-xs mt-1">
          {entry.amountGrams != null
            ? `${Math.round(entry.amountGrams * 10) / 10} g`
            : `${entry.servings} ${entry.servings === 1 ? "serving" : "servings"}`}{" "}
          · {Math.round(entry.proteinGrams ?? 0)} g protein
        </p>
      </div>
      <p className="num text-sm whitespace-nowrap">
        {Math.round(entry.calories ?? 0).toLocaleString()}{" "}
        <span className="log-muted text-xs">kcal</span>
      </p>
      <DeleteLogEntry id={entry.id} name={entry.name} />
    </div>
  );
}

function MealEntries({ entries }: { entries: LogMeal[] }) {
  const seen = new Set<string>();
  return entries.map((entry) => {
    if (!entry.groupId) return <MealRow key={entry.id} entry={entry} />;
    if (seen.has(entry.groupId)) return null;
    seen.add(entry.groupId);
    const group = entries.filter((item) => item.groupId === entry.groupId);
    return (
      <details className="recipe-log-group" key={entry.groupId}>
        <summary>
          <span className="min-w-0 break-words font-medium">
            {entry.groupName || "Recipe"}
            <span className="block text-xs font-normal log-muted mt-1">
              {group.length} ingredients · Show details
            </span>
          </span>
          <span className="num text-sm whitespace-nowrap">
            {Math.round(
              group.reduce((sum, item) => sum + (item.calories ?? 0), 0),
            )}{" "}
            <span className="text-xs log-muted">kcal</span>
          </span>
        </summary>
        <div className="pl-4">
          {group.map((item) => (
            <MealRow key={item.id} entry={item} />
          ))}
        </div>
      </details>
    );
  });
}

export default async function TodayPage({
  searchParams,
}: {
  searchParams: Promise<{ date?: string }>;
}) {
  const [user, params] = await Promise.all([getUserServer(), searchParams]);
  const today = dateInTimeZone(user.timeZoneId || "UTC");
  const date =
    isLogDate(params.date) && params.date <= today ? params.date : today;
  const [diary, goal, workouts, measurements] = await Promise.all([
    getDayDiary(date),
    getLogGoal(),
    getLogWorkouts(date, date),
    getLogMeasurements(date, date),
  ]);
  const nutrients = [
    {
      name: "Calories",
      value: diary.totals.calories,
      target: goal?.targetCalories,
      unit: "kcal",
      color: "var(--foreground)",
    },
    {
      name: "Protein",
      value: diary.totals.protein,
      target: goal?.targetProteinGrams,
      unit: "g",
      color: "var(--chart-1)",
    },
    {
      name: "Carbs",
      value: diary.totals.carbs,
      target: goal?.targetCarbsGrams,
      unit: "g",
      color: "var(--chart-2)",
    },
    {
      name: "Fat",
      value: diary.totals.fat,
      target: goal?.targetFatGrams,
      unit: "g",
      color: "var(--chart-3)",
    },
  ];
  const types = [
    ...MEAL_TYPES,
    ...diary.entries
      .map((entry) => entry.mealType.toUpperCase())
      .filter((type) => !(MEAL_TYPES as readonly string[]).includes(type)),
  ];
  return (
    <div className="log-page">
      <header className="log-page-header">
        <div>
          <h1>
            {date === today
              ? "Today"
              : formatLogDate(date, { weekday: "long" })}
          </h1>
          <p className="log-muted mt-2">
            {formatLogDate(date, {
              weekday: date === today ? "long" : undefined,
              month: "long",
              day: "numeric",
              year: "numeric",
            })}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <DateNavigator date={date} today={today} />
          <LogEntryButton date={date} className="btn-primary">
            <Plus size={18} /> Log entry
          </LogEntryButton>
        </div>
      </header>
      {date === today && (
        <Suspense fallback={null}>
          <ResumeWorkoutBanner />
          <TrainerStrip />
        </Suspense>
      )}
      <section aria-label="Daily nutrition" className="nutrition-strip">
        {nutrients.map((nutrient) => (
          <div className="nutrition-readout" key={nutrient.name}>
            <p className="text-sm log-muted">{nutrient.name}</p>
            <p className="num nutrition-value">
              {Math.round(nutrient.value).toLocaleString()}
              <span className="text-sm font-normal log-muted">
                {" "}
                {nutrient.target
                  ? `/ ${nutrient.target.toLocaleString()} `
                  : " "}
                {nutrient.unit}
              </span>
            </p>
            {nutrient.target && nutrient.target > 0 ? (
              <div
                className="target-track"
                role="meter"
                aria-label={nutrient.name}
                aria-valuenow={Math.round(nutrient.value)}
                aria-valuemin={0}
                aria-valuemax={Math.max(
                  nutrient.target,
                  Math.round(nutrient.value),
                )}
                aria-valuetext={`${Math.round(nutrient.value)} of ${nutrient.target} ${nutrient.unit}`}
              >
                <span
                  style={{
                    width: `${Math.min(100, (nutrient.value / nutrient.target) * 100)}%`,
                    background: nutrient.color,
                  }}
                />
              </div>
            ) : null}
          </div>
        ))}
      </section>
      {!goal && (
        <p className="text-sm log-muted -mt-3 mb-8">
          A daily target gives your log some context.{" "}
          <Link href="/goal" className="text-link">
            Set your targets <ArrowRight size={13} />
          </Link>
        </p>
      )}
      <div className="day-log-grid">
        <section aria-labelledby="meals-title">
          <header className="log-section-header">
            <h2 id="meals-title">Meals</h2>
            <LogEntryButton kind="meal" date={date} className="text-link">
              <Plus size={16} /> Add meal
            </LogEntryButton>
          </header>
          {diary.entries.length === 0 ? (
            <div className="log-empty">
              <p>Your meals go here.</p>
              <p className="log-muted text-sm mt-2">
                Search for a food, reuse a recipe, or add a quick entry.
              </p>
              <LogEntryButton
                kind="meal"
                date={date}
                className="btn-secondary mt-5"
              >
                Log your first meal
              </LogEntryButton>
            </div>
          ) : (
            types
              .filter((type, index) => types.indexOf(type) === index)
              .map((type) => {
                const entries = diary.entries.filter(
                  (entry) => entry.mealType.toUpperCase() === type,
                );
                if (!entries.length) return null;
                return (
                  <div className="meal-group" key={type}>
                    <div className="meal-group-header">
                      <h3>{formatMealType(type)}</h3>
                      <span className="num text-xs log-muted">
                        {Math.round(
                          entries.reduce(
                            (total, entry) => total + (entry.calories ?? 0),
                            0,
                          ),
                        ).toLocaleString()}{" "}
                        kcal
                      </span>
                    </div>
                    <MealEntries entries={entries} />
                    {entries.length >= 2 && (
                      <div className="pt-2">
                        <SaveMealAsRecipe
                          date={date}
                          mealType={type}
                          itemCount={entries.length}
                        />
                      </div>
                    )}
                  </div>
                );
              })
          )}
        </section>
        <div className="day-side-log">
          <section aria-labelledby="training-title">
            <header className="log-section-header">
              <h2 id="training-title">Training</h2>
              <Dumbbell size={18} className="log-muted" />
            </header>
            {workouts.length ? (
              workouts.map((workout) => (
                <div key={workout.id} className="py-4 border-b">
                  <div className="flex justify-between gap-3">
                    <h3 className="font-medium">{workout.name || "Workout"}</h3>
                    <DeleteLogEntry
                      kind="Workouts"
                      id={workout.id}
                      name={workout.name || "workout"}
                    />
                  </div>
                  <p className="log-muted text-sm mt-1">
                    {workout.exercises.length} exercises
                    {workout.durationMinutes
                      ? ` · ${workout.durationMinutes} min`
                      : ""}
                  </p>
                  <details className="mt-3 text-sm">
                    <summary className="text-link">View sets</summary>
                    <ul className="mt-2 space-y-2">
                      {workout.exercises.map((exercise) => (
                        <li key={exercise.id}>
                          <span className="font-medium">
                            {exercise.exerciseName}
                          </span>
                          <p className="log-muted text-xs mt-1">
                            {exercise.sets
                              .filter((set) => set.completed)
                              .map(
                                (set) =>
                                  `${set.reps ?? "–"} reps${set.weightKg != null ? ` × ${set.weightKg} kg` : ""}`,
                              )
                              .join(" · ") || "No completed sets"}
                          </p>
                        </li>
                      ))}
                    </ul>
                  </details>
                </div>
              ))
            ) : (
              <div className="py-5">
                <p className="log-muted text-sm">
                  No workout logged for this day.
                </p>
                <LogEntryButton
                  kind="workout"
                  date={date}
                  className="text-link mt-3"
                >
                  Start a workout <ArrowRight size={14} />
                </LogEntryButton>
              </div>
            )}
          </section>
          <section aria-labelledby="measurements-title">
            <header className="log-section-header">
              <h2 id="measurements-title">Measurements</h2>
              <Ruler size={18} className="log-muted" />
            </header>
            {measurements.length ? (
              measurements.map((measurement) => (
                <div key={measurement.id} className="py-4 border-b">
                  <div className="flex justify-between gap-2">
                    <div>
                      {measurement.weightKg != null && (
                        <p className="num text-2xl font-medium">
                          {measurement.weightKg}{" "}
                          <span className="text-sm font-normal log-muted">
                            kg
                          </span>
                        </p>
                      )}
                      {measurement.bodyFatPercentage != null && (
                        <p className="text-sm mt-1">
                          {measurement.bodyFatPercentage}% body fat
                        </p>
                      )}
                      {measurement.waistCm != null && (
                        <p className="text-sm mt-1">
                          {measurement.waistCm} cm waist
                        </p>
                      )}
                      {measurement.weightKg == null &&
                        measurement.bodyFatPercentage == null &&
                        measurement.waistCm == null && (
                          <p className="text-sm">Body measurements recorded</p>
                        )}
                    </div>
                    <DeleteLogEntry
                      kind="BodyMeasurements"
                      id={measurement.id}
                      name="measurement"
                    />
                  </div>
                  {measurement.notes && (
                    <p className="text-sm log-muted mt-2 break-words">
                      {measurement.notes}
                    </p>
                  )}
                </div>
              ))
            ) : (
              <p className="log-muted text-sm py-5">
                No measurements logged for this day.
              </p>
            )}
            <LogEntryButton
              kind="measurement"
              date={date}
              className="text-link mt-3"
            >
              <Plus size={14} /> Add measurement
            </LogEntryButton>
          </section>
        </div>
      </div>
    </div>
  );
}
