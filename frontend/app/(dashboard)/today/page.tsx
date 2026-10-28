import Link from "next/link";
import { Suspense } from "react";
import { Plus, ArrowRight } from "lucide-react";
import { getUserServer } from "@/helper/session";
import {
  getDayDiary,
  getLogGoal,
  getLogMeasurements,
  getLogWorkouts,
  type DayDiary,
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
import TodayPlan from "@/components/planning/TodayPlan";

export const metadata = { title: "Today · Mizan" };

function MealRow({ entry }: { entry: LogMeal }) {
  return (
    <div className="grid min-h-[78px] grid-cols-[minmax(0,1fr)_auto_36px] items-center gap-3 border-b border-border py-2.5 pr-0 pl-3 text-[14px] max-sm:gap-2 max-sm:pl-1 [&_.icon-button]:w-9">
      <div className="min-w-0">
        <p className="font-medium break-words">{entry.name}</p>
        <p className="text-muted-foreground text-xs mt-1">
          {entry.amountGrams != null
            ? `${Math.round(entry.amountGrams * 10) / 10} g`
            : `${entry.servings} ${entry.servings === 1 ? "serving" : "servings"}`}{" "}
          · {Math.round(entry.proteinGrams ?? 0)} g protein
        </p>
      </div>
      <p className="tabular-nums text-sm whitespace-nowrap">
        {Math.round(entry.calories ?? 0).toLocaleString()}{" "}
        <span className="text-muted-foreground text-xs">kcal</span>
      </p>
      <DeleteLogEntry id={entry.id} name={entry.name} />
    </div>
  );
}

/** The rows of one meal. A logged recipe folds its ingredient rows under the total the API sent for it. */
function MealEntries({
  entries,
  groups,
}: {
  entries: LogMeal[];
  groups: DayDiary["groups"];
}) {
  const seen = new Set<string>();
  return entries.map((entry) => {
    if (!entry.groupId) return <MealRow key={entry.id} entry={entry} />;
    if (seen.has(entry.groupId)) return null;
    seen.add(entry.groupId);
    const group = entries.filter((item) => item.groupId === entry.groupId);
    return (
      <details className="border-b border-border" key={entry.groupId}>
        <summary className="flex cursor-pointer items-center justify-between gap-4 px-3 py-4 text-[14px]">
          <span className="min-w-0 break-words font-medium">
            {entry.groupName || "Recipe"}
            <span className="block text-xs font-normal text-muted-foreground mt-1">
              {group.length} ingredients · Show details
            </span>
          </span>
          <span
            className="tabular-nums text-sm whitespace-nowrap"
            data-testid="recipe-group-calories"
          >
            {Math.round(
              groups.find((g) => g.groupId === entry.groupId)?.totals
                .calories ?? 0,
            )}{" "}
            <span className="text-xs text-muted-foreground">kcal</span>
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
          <p className="text-muted-foreground mt-2">
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
            <p className="text-sm text-muted-foreground">{nutrient.name}</p>
            <p className="tabular-nums nutrition-value">
              {Math.round(nutrient.value).toLocaleString()}
              <span className="text-sm font-normal text-muted-foreground">
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
        <p className="text-sm text-muted-foreground -mt-3 mb-8">
          A daily target gives your log some context.{" "}
          <Link href="/goal" className="text-link">
            Set your targets <ArrowRight size={13} />
          </Link>
        </p>
      )}
      <Suspense fallback={null}>
        <TodayPlan date={date} />
      </Suspense>
      <div className="grid grid-cols-[minmax(0,1.8fr)_minmax(240px,1fr)] gap-12 max-xl:gap-7 max-sm:grid-cols-1 max-sm:gap-9">
        <section aria-labelledby="meals-title">
          <header className="log-section-header">
            <h2 id="meals-title">Meals</h2>
            <LogEntryButton kind="meal" date={date} className="text-link">
              Add meal
            </LogEntryButton>
          </header>
          {diary.entries.length === 0 ? (
            <div className="log-empty">
              <p>Your meals go here.</p>
              <p className="text-muted-foreground text-sm mt-2">
                Say what you ate, add a photo, or search. You check it before it
                is saved.
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
                  <div
                    role="group"
                    aria-label={formatMealType(type)}
                    className="mt-6"
                    key={type}
                  >
                    <div className="meal-group-header">
                      <h3>{formatMealType(type)}</h3>
                      <span
                        className="tabular-nums text-xs text-muted-foreground"
                        data-testid="meal-calories"
                      >
                        {Math.round(
                          diary.meals.find((meal) => meal.mealType === type)
                            ?.totals.calories ?? 0,
                        ).toLocaleString()}{" "}
                        kcal
                      </span>
                    </div>
                    <MealEntries entries={entries} groups={diary.groups} />
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
        <div className="grid content-start gap-8 max-sm:gap-7">
          <section aria-labelledby="training-title">
            <header className="log-section-header">
              <h2 id="training-title">Training</h2>
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
                  <p className="text-muted-foreground text-sm mt-1">
                    {workout.exercises.length} exercise
                    {workout.exercises.length === 1 ? "" : "s"}
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
                          <p className="text-muted-foreground text-xs mt-1">
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
                <p className="text-muted-foreground text-sm">
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
            </header>
            {measurements.length ? (
              measurements.map((measurement) => (
                <div key={measurement.id} className="py-4 border-b">
                  <div className="flex justify-between gap-2">
                    <div>
                      {measurement.weightKg != null && (
                        <p className="tabular-nums text-2xl font-medium">
                          {measurement.weightKg}{" "}
                          <span className="text-sm font-normal text-muted-foreground">
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
                    <p className="text-sm text-muted-foreground mt-2 break-words">
                      {measurement.notes}
                    </p>
                  )}
                </div>
              ))
            ) : (
              <p className="text-muted-foreground text-sm py-5">
                No measurements logged for this day.
              </p>
            )}
            <LogEntryButton
              kind="measurement"
              date={date}
              className="text-link mt-3"
            >
              Add measurement
            </LogEntryButton>
          </section>
        </div>
      </div>
    </div>
  );
}
