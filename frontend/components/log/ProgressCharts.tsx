"use client";

import { useState } from "react";
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import { formatLogDate, shiftDate } from "@/lib/log-date";
import { dailyWeightTrend, exerciseTrends } from "@/lib/log-trends";
import type { DailyNutritionSummary } from "@/data/meal";
import type { BodyMeasurement } from "@/data/bodyMeasurement";
import type { LogWorkout } from "@/data/log";
import { LogEntryButton } from "@/components/logging/LogEntryButton";

const axis = { fill: "var(--muted-foreground)", fontSize: 11 };
const dateTick = (date: string) =>
  formatLogDate(date, { month: "short", day: "numeric" });
const tooltipStyle = {
  background: "var(--popover)",
  color: "var(--popover-foreground)",
  border: "1px solid var(--border)",
  borderRadius: 3,
  fontSize: 12,
};

function DataRows({
  rows,
  columns,
}: {
  rows: Array<{ date: string; [key: string]: string | number | null }>;
  columns: Array<{ key: string; label: string }>;
}) {
  return (
    <details className="chart-data">
      <summary>View data</summary>
      <div className="overflow-x-auto">
        <table>
          <thead>
            <tr>
              <th>Date</th>
              {columns.map((column) => (
                <th key={column.key}>{column.label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${row.date}-${index}`}>
                <td>{dateTick(row.date)}</td>
                {columns.map((column) => (
                  <td className="num" key={column.key}>
                    {row[column.key]?.toLocaleString() ?? "—"}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}

export default function ProgressCharts({
  nutrition,
  measurements,
  workouts,
  days,
  end,
}: {
  nutrition: DailyNutritionSummary[];
  measurements: BodyMeasurement[];
  workouts: LogWorkout[];
  days: number;
  end: string;
}) {
  const exercises = exerciseTrends(workouts);
  const [exerciseId, setExerciseId] = useState("");
  const selected =
    exercises.find((exercise) => exercise.id === exerciseId) ?? exercises[0];
  const weightPoints = dailyWeightTrend(measurements);
  const nutritionPoints = Array.from({ length: days }, (_, index) => {
    const date = shiftDate(end, index - days + 1);
    const entry = nutrition.find((day) => day.date.slice(0, 10) === date);
    return {
      date,
      calories: entry ? Math.round(entry.calories) : null,
      protein: entry ? Math.round(entry.protein) : null,
    };
  });
  return (
    <div className="progress-sections">
      <section aria-labelledby="nutrition-trend">
        <header className="log-section-header">
          <h2 id="nutrition-trend">Nutrition</h2>
          <div className="chart-legend">
            <span>
              <i style={{ background: "var(--foreground)" }} />
              Calories
            </span>
            <span>
              <i style={{ background: "var(--chart-1)" }} />
              Protein
            </span>
          </div>
        </header>
        {nutrition.length ? (
          <>
            <p className="log-muted text-sm mt-4">
              Daily intake. Gaps are days without a meal entry.
            </p>
            <div className="trend-chart">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart
                  data={nutritionPoints}
                  margin={{ top: 16, right: 0, left: -12, bottom: 4 }}
                  accessibilityLayer
                >
                  <CartesianGrid vertical={false} stroke="var(--border)" />
                  <XAxis
                    dataKey="date"
                    tickFormatter={dateTick}
                    tick={axis}
                    tickLine={false}
                    axisLine={false}
                    minTickGap={35}
                  />
                  <YAxis
                    yAxisId="calories"
                    tick={axis}
                    tickLine={false}
                    axisLine={false}
                    width={56}
                  />
                  <YAxis
                    yAxisId="protein"
                    orientation="right"
                    tick={axis}
                    tickLine={false}
                    axisLine={false}
                    width={44}
                    unit="g"
                  />
                  <Tooltip
                    contentStyle={tooltipStyle}
                    labelFormatter={(label) => dateTick(String(label))}
                  />
                  <Line
                    yAxisId="calories"
                    dataKey="calories"
                    name="Calories (kcal)"
                    stroke="var(--foreground)"
                    strokeWidth={2}
                    dot={{ r: 2 }}
                    activeDot={{ r: 4 }}
                    isAnimationActive={false}
                  />
                  <Line
                    yAxisId="protein"
                    dataKey="protein"
                    name="Protein (g)"
                    stroke="var(--chart-1)"
                    strokeWidth={2}
                    dot={{ r: 2 }}
                    activeDot={{ r: 4 }}
                    isAnimationActive={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
            <DataRows
              rows={nutritionPoints}
              columns={[
                { key: "calories", label: "Calories (kcal)" },
                { key: "protein", label: "Protein (g)" },
              ]}
            />
          </>
        ) : (
          <div className="log-empty">
            <p>Your nutrition trend starts with a meal.</p>
            <LogEntryButton kind="meal" className="text-link mt-3">
              Log a meal
            </LogEntryButton>
          </div>
        )}
      </section>
      <section aria-labelledby="weight-trend">
        <header className="log-section-header">
          <h2 id="weight-trend">Bodyweight</h2>
          <span className="text-xs log-muted">Kilograms</span>
        </header>
        {weightPoints.length ? (
          <>
            <p className="log-muted text-sm mt-4">
              {weightPoints.length}{" "}
              {weightPoints.length === 1 ? "weigh-in" : "weigh-ins"} in this
              period
              {weightPoints.length > 1
                ? ` · ${Math.round((weightPoints.at(-1)!.weight - weightPoints[0].weight) * 10) / 10 > 0 ? "+" : ""}${Math.round((weightPoints.at(-1)!.weight - weightPoints[0].weight) * 10) / 10} kg change`
                : " · Add another to see a trend"}
            </p>
            <div className="trend-chart">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart
                  data={weightPoints}
                  margin={{ top: 16, right: 16, left: -12, bottom: 4 }}
                  accessibilityLayer
                >
                  <CartesianGrid vertical={false} stroke="var(--border)" />
                  <XAxis
                    dataKey="date"
                    tickFormatter={dateTick}
                    tick={axis}
                    tickLine={false}
                    axisLine={false}
                    minTickGap={35}
                  />
                  <YAxis
                    domain={["dataMin - 1", "dataMax + 1"]}
                    tickFormatter={(value: number) =>
                      value.toLocaleString(undefined, {
                        maximumFractionDigits: 1,
                      })
                    }
                    tick={axis}
                    tickLine={false}
                    axisLine={false}
                    width={56}
                  />
                  <Tooltip
                    contentStyle={tooltipStyle}
                    labelFormatter={(label) => dateTick(String(label))}
                    formatter={(value) =>
                      typeof value === "number"
                        ? value.toLocaleString(undefined, {
                            maximumFractionDigits: 1,
                          })
                        : value
                    }
                  />
                  <Line
                    dataKey="weight"
                    name="Weight (kg)"
                    stroke="var(--chart-1)"
                    strokeWidth={2}
                    dot={{ r: 3 }}
                    isAnimationActive={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
            <DataRows
              rows={weightPoints}
              columns={[{ key: "weight", label: "Weight (kg)" }]}
            />
          </>
        ) : (
          <div className="log-empty">
            <p>A little perspective on your weight.</p>
            <p className="log-muted text-sm mt-2">
              Add a weigh-in to start seeing change over time.
            </p>
            <LogEntryButton kind="measurement" className="text-link mt-3">
              Add a measurement
            </LogEntryButton>
          </div>
        )}
      </section>
      <section aria-labelledby="strength-trend">
        <header className="log-section-header">
          <h2 id="strength-trend">Strength</h2>
        </header>
        {selected ? (
          <>
            <div className="strength-controls">
              <div>
                <label className="label" htmlFor="trend-exercise">
                  Exercise
                </label>
                <select
                  id="trend-exercise"
                  className="input"
                  value={selected.id}
                  onChange={(event) => setExerciseId(event.target.value)}
                >
                  {exercises.map((exercise) => (
                    <option key={exercise.id} value={exercise.id}>
                      {exercise.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="chart-legend">
                <span>
                  <i style={{ background: "var(--foreground)" }} />
                  Volume (kg)
                </span>
                <span>
                  <i style={{ background: "var(--chart-1)" }} />
                  Estimated 1RM (kg)
                </span>
              </div>
            </div>
            <div className="trend-chart">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart
                  data={selected.points}
                  margin={{ top: 16, right: 0, left: -12, bottom: 4 }}
                  accessibilityLayer
                >
                  <CartesianGrid vertical={false} stroke="var(--border)" />
                  <XAxis
                    dataKey="date"
                    tickFormatter={dateTick}
                    tick={axis}
                    tickLine={false}
                    axisLine={false}
                    minTickGap={35}
                  />
                  <YAxis
                    yAxisId="volume"
                    tick={axis}
                    tickLine={false}
                    axisLine={false}
                    width={56}
                  />
                  <YAxis
                    yAxisId="max"
                    orientation="right"
                    tick={axis}
                    tickLine={false}
                    axisLine={false}
                    width={44}
                  />
                  <Tooltip
                    contentStyle={tooltipStyle}
                    labelFormatter={(label) => dateTick(String(label))}
                  />
                  <Line
                    yAxisId="volume"
                    dataKey="volume"
                    name="Volume (kg)"
                    stroke="var(--foreground)"
                    strokeWidth={2}
                    dot={{ r: 3 }}
                    isAnimationActive={false}
                  />
                  <Line
                    yAxisId="max"
                    dataKey="estimatedMax"
                    name="Estimated 1RM (kg)"
                    stroke="var(--chart-1)"
                    strokeWidth={2}
                    dot={{ r: 3 }}
                    isAnimationActive={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
            <p className="text-xs log-muted">
              Volume is weight × reps across completed sets. Estimated one-rep
              max uses the Epley formula for sets of up to 12 reps.
            </p>
            <DataRows
              rows={selected.points}
              columns={[
                { key: "volume", label: "Volume (kg)" },
                { key: "estimatedMax", label: "Estimated 1RM (kg)" },
              ]}
            />
          </>
        ) : (
          <div className="log-empty">
            <p>See the work add up.</p>
            <p className="log-muted text-sm mt-2">
              Complete weighted sets to compare volume and estimated strength
              here.
            </p>
            <LogEntryButton kind="workout" className="text-link mt-3">
              Start a workout
            </LogEntryButton>
          </div>
        )}
      </section>
    </div>
  );
}
