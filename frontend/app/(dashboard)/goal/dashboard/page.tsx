"use client";

import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import Link from "next/link";
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, BarChart, Bar, RadialBarChart, RadialBar, ReferenceLine } from "recharts";
import { GoalData } from "@/types/goal";
import Loading from "@/components/Loading";


const MACRO_COLORS = {
  calories: "#f97316", // orange
  protein: "#ef4444",  // red
  carbs: "#f59e0b",    // amber
  fat: "#eab308",      // yellow
};

export default function GoalDashboard() {
  const [data, setData] = useState<GoalData | null>(null);
  const [loading, setLoading] = useState(true);
  const [days, setDays] = useState(7);

  useEffect(() => {
    fetchData();
  }, [days]);

  async function fetchData() {
    try {
      const result = await clientApi<GoalData>(`/api/Goals/progress?days=${days}`);
      setData(result);
    } catch (error) {
      console.error("Failed to fetch goal data:", error);
    } finally {
      setLoading(false);
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <Loading />
      </div>
    );
  }

  if (!data?.goal) {
    return (
      <div className="max-w-4xl mx-auto">
        <div className="card p-12 text-center">
          <div className="w-20 h-20 mx-auto mb-6 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center">
            <i className="ri-target-line text-4xl text-white" />
          </div>
          <h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-3">
            No Active Goal
          </h2>
          <p className="text-slate-600 dark:text-slate-400 mb-6 max-w-md mx-auto">
            Start tracking your nutrition journey by setting up your first goal. Define your targets and watch your progress unfold.
          </p>
          <Link href="/goal" className="btn-primary inline-flex">
            <i className="ri-add-line text-xl" />
            Create Your First Goal
          </Link>
        </div>
      </div>
    );
  }

  const { goal, progressEntries } = data;
  const latestEntry = progressEntries[progressEntries.length - 1];

  // Calculate today's progress
  const todayProgress = latestEntry
    ? {
        calories: (latestEntry.actualCalories / goal.targetCalories!) * 100,
        protein: (latestEntry.actualProteinGrams / goal.targetProteinGrams!) * 100,
        carbs: (latestEntry.actualCarbsGrams / goal.targetCarbsGrams!) * 100,
        fat: (latestEntry.actualFatGrams / goal.targetFatGrams!) * 100,
      }
    : null;

  // Prepare chart data
  const chartData = progressEntries.map((entry) => ({
    date: new Date(entry.date).toLocaleDateString("en-US", { month: "short", day: "numeric" }),
    calories: entry.actualCalories,
    protein: entry.actualProteinGrams,
    carbs: entry.actualCarbsGrams,
    fat: entry.actualFatGrams,
  }));

  // Radial progress data
  const radialData = todayProgress
    ? [
        { name: "Calories", value: Math.min(todayProgress.calories, 100), fill: MACRO_COLORS.calories },
        { name: "Protein", value: Math.min(todayProgress.protein, 100), fill: MACRO_COLORS.protein },
        { name: "Carbs", value: Math.min(todayProgress.carbs, 100), fill: MACRO_COLORS.carbs },
        { name: "Fat", value: Math.min(todayProgress.fat, 100), fill: MACRO_COLORS.fat },
      ]
    : [];

  return (
    <div className="max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-slate-900 dark:text-slate-100">Goal Dashboard</h1>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Track your daily nutrition progress</p>
        </div>
        <div className="flex gap-3">
          <Link href="/goal/progress" className="btn-primary">
            <i className="ri-add-line text-xl" />
            Log Today's Progress
          </Link>
          <Link href="/goal" className="btn-secondary">
            <i className="ri-settings-line" />
            Edit Goal
          </Link>
        </div>
      </div>

      {/* Time Range Selector */}
      <div className="flex gap-2">
        {[7, 14, 30].map((d) => (
          <button
            key={d}
            onClick={() => setDays(d)}
            className={`px-4 py-2 rounded-xl font-medium transition-all ${
              days === d
                ? "bg-brand-500 text-white shadow-lg"
                : "bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800"
            }`}
          >
            {d} Days
          </button>
        ))}
      </div>

      {/* Today's Progress Radial */}
      {todayProgress && (
        <div className="card p-6">
          <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 mb-6">Today's Progress</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
            {/* Radial Chart */}
            <div>
              <ResponsiveContainer width="100%" height={300}>
                <RadialBarChart
                  cx="50%"
                  cy="50%"
                  innerRadius="20%"
                  outerRadius="90%"
                  data={radialData}
                  startAngle={90}
                  endAngle={-270}
                >
                  <RadialBar
                    background
                    dataKey="value"
                  />
                  <Legend
                    iconSize={10}
                    layout="vertical"
                    verticalAlign="middle"
                    align="right"
                  />
                </RadialBarChart>
              </ResponsiveContainer>
            </div>

            {/* Macro Stats */}
            <div className="space-y-4">
              {[
                { label: "Calories", actual: latestEntry.actualCalories, target: goal.targetCalories, color: MACRO_COLORS.calories, unit: "kcal" },
                { label: "Protein", actual: latestEntry.actualProteinGrams, target: goal.targetProteinGrams, color: MACRO_COLORS.protein, unit: "g" },
                { label: "Carbs", actual: latestEntry.actualCarbsGrams, target: goal.targetCarbsGrams, color: MACRO_COLORS.carbs, unit: "g" },
                { label: "Fat", actual: latestEntry.actualFatGrams, target: goal.targetFatGrams, color: MACRO_COLORS.fat, unit: "g" },
              ].map((macro) => (
                <div key={macro.label}>
                  <div className="flex justify-between items-center mb-2">
                    <span className="text-sm font-medium text-slate-700 dark:text-slate-300 flex items-center gap-2">
                      <span className="w-3 h-3 rounded-full" style={{ backgroundColor: macro.color }} />
                      {macro.label}
                    </span>
                    <span className="text-sm text-slate-600 dark:text-slate-400">
                      {macro.actual?.toFixed(1)} / {macro.target} {macro.unit}
                    </span>
                  </div>
                  <div className="h-2 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
                    <div
                      className="h-full transition-all duration-500"
                      style={{
                        width: `${Math.min((macro.actual! / macro.target!) * 100, 100)}%`,
                        backgroundColor: macro.color,
                      }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Line Chart - Calories Trend */}
      <div className="card p-6">
        <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 mb-6">Calorie Trend</h2>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={chartData}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
            <XAxis dataKey="date" stroke="#64748b" />
            <YAxis stroke="#64748b" />
            <Tooltip
              contentStyle={{
                backgroundColor: "#ffffff",
                border: "1px solid #e2e8f0",
                borderRadius: "12px",
                boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)",
              }}
            />
            <Legend />
            <Line
              type="monotone"
              dataKey="calories"
              stroke={MACRO_COLORS.calories}
              strokeWidth={3}
              dot={{ fill: MACRO_COLORS.calories, strokeWidth: 2, r: 6 }}
              activeDot={{ r: 8 }}
            />
            {goal.targetCalories && (
              <ReferenceLine
                y={goal.targetCalories}
                stroke={MACRO_COLORS.calories}
                strokeDasharray="6 3"
                strokeWidth={2}
                label={{ value: `Target: ${goal.targetCalories} kcal`, position: "insideTopRight", fontSize: 11, fill: MACRO_COLORS.calories }}
              />
            )}
          </LineChart>
        </ResponsiveContainer>
      </div>

      {/* Bar Chart - Macros Breakdown */}
      <div className="card p-6">
        <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 mb-6">Macro Breakdown</h2>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={chartData}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
            <XAxis dataKey="date" stroke="#64748b" />
            <YAxis stroke="#64748b" />
            <Tooltip
              contentStyle={{
                backgroundColor: "#ffffff",
                border: "1px solid #e2e8f0",
                borderRadius: "12px",
                boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)",
              }}
            />
            <Legend />
            <Bar dataKey="protein" fill={MACRO_COLORS.protein} radius={[8, 8, 0, 0]} />
            <Bar dataKey="carbs" fill={MACRO_COLORS.carbs} radius={[8, 8, 0, 0]} />
            <Bar dataKey="fat" fill={MACRO_COLORS.fat} radius={[8, 8, 0, 0]} />
            {goal.targetProteinGrams && (
              <ReferenceLine y={goal.targetProteinGrams} stroke={MACRO_COLORS.protein} strokeDasharray="6 3" strokeWidth={1.5} />
            )}
            {goal.targetCarbsGrams && (
              <ReferenceLine y={goal.targetCarbsGrams} stroke={MACRO_COLORS.carbs} strokeDasharray="6 3" strokeWidth={1.5} />
            )}
            {goal.targetFatGrams && (
              <ReferenceLine y={goal.targetFatGrams} stroke={MACRO_COLORS.fat} strokeDasharray="6 3" strokeWidth={1.5} />
            )}
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Recent Entries */}
      <div className="card p-6">
        <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 mb-6">Recent Entries</h2>
        <div className="space-y-3">
          {progressEntries.slice(-5).reverse().map((entry) => (
            <div key={entry.id} className="flex items-center justify-between p-4 rounded-xl bg-slate-50 dark:bg-slate-800 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors">
              <div>
                <div className="font-medium text-slate-900 dark:text-slate-100">
                  {new Date(entry.date).toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric" })}
                </div>
                {entry.notes && <div className="text-sm text-slate-500 dark:text-slate-400 mt-1">{entry.notes}</div>}
              </div>
              <div className="flex gap-6 text-sm">
                <div className="text-center">
                  <div className="text-slate-500 dark:text-slate-400">Calories</div>
                  <div className="font-bold text-slate-900 dark:text-slate-100">{entry.actualCalories}</div>
                </div>
                <div className="text-center">
                  <div className="text-slate-500 dark:text-slate-400">Protein</div>
                  <div className="font-bold text-slate-900 dark:text-slate-100">{entry.actualProteinGrams.toFixed(1)}g</div>
                </div>
                <div className="text-center">
                  <div className="text-slate-500 dark:text-slate-400">Carbs</div>
                  <div className="font-bold text-slate-900 dark:text-slate-100">{entry.actualCarbsGrams.toFixed(1)}g</div>
                </div>
                <div className="text-center">
                  <div className="text-slate-500 dark:text-slate-400">Fat</div>
                  <div className="font-bold text-slate-900 dark:text-slate-100">{entry.actualFatGrams.toFixed(1)}g</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
