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
    Legend,
} from "recharts";
import type { BodyMeasurement } from "@/data/bodyMeasurement";

type TimeRange = "1M" | "3M" | "6M" | "1Y" | "ALL";

const TIME_RANGES: { label: string; value: TimeRange }[] = [
    { label: "1M", value: "1M" },
    { label: "3M", value: "3M" },
    { label: "6M", value: "6M" },
    { label: "1Y", value: "1Y" },
    { label: "All", value: "ALL" },
];

const SERIES = [
    { key: "weight",      label: "Weight (kg)",      color: "#6366f1" },
    { key: "bodyFat",     label: "Body Fat (%)",      color: "#f43f5e" },
    { key: "muscle",      label: "Muscle (kg)",       color: "#8b5cf6" },
    { key: "waist",       label: "Waist (cm)",        color: "#f97316" },
    { key: "hips",        label: "Hips (cm)",         color: "#ec4899" },
    { key: "chest",       label: "Chest (cm)",        color: "#06b6d4" },
    { key: "leftArm",     label: "Left Arm (cm)",     color: "#10b981" },
    { key: "rightArm",    label: "Right Arm (cm)",    color: "#84cc16" },
    { key: "leftThigh",   label: "Left Thigh (cm)",   color: "#f59e0b" },
    { key: "rightThigh",  label: "Right Thigh (cm)",  color: "#eab308" },
] as const;

type SeriesKey = typeof SERIES[number]["key"];

function getDateCutoff(range: TimeRange): Date | null {
    if (range === "ALL") return null;
    const now = new Date();
    const months = { "1M": 1, "3M": 3, "6M": 6, "1Y": 12 }[range];
    now.setMonth(now.getMonth() - months);
    return now;
}

export default function MeasurementChart({ measurements }: { measurements: BodyMeasurement[] }) {
    const [range, setRange] = useState<TimeRange>("3M");
    const [hiddenSeries, setHiddenSeries] = useState<Set<SeriesKey>>(
        new Set(["waist", "hips", "chest", "leftArm", "rightArm", "leftThigh", "rightThigh"])
    );

    const toggleSeries = (key: SeriesKey) => {
        setHiddenSeries((prev) => {
            const next = new Set(prev);
            if (next.has(key)) next.delete(key);
            else next.add(key);
            return next;
        });
    };

    const cutoff = getDateCutoff(range);
    const filtered = measurements
        .filter((m) => !cutoff || new Date(m.date) >= cutoff)
        .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

    const data = filtered.map((m) => ({
        date: new Date(m.date).toLocaleDateString(undefined, { month: "short", day: "numeric" }),
        weight:     m.weightKg ?? null,
        bodyFat:    m.bodyFatPercentage ?? null,
        muscle:     m.muscleMassKg ?? null,
        waist:      m.waistCm ?? null,
        hips:       m.hipsCm ?? null,
        chest:      m.chestCm ?? null,
        leftArm:    m.leftArmCm ?? null,
        rightArm:   m.rightArmCm ?? null,
        leftThigh:  m.leftThighCm ?? null,
        rightThigh: m.rightThighCm ?? null,
    }));

    const visibleSeries = SERIES.filter(
        (s) => !hiddenSeries.has(s.key) && data.some((d) => d[s.key] !== null)
    );

    if (data.length < 2) {
        return (
            <div className="card p-6">
                <h2 className="section-title mb-4">Progress Chart</h2>
                <div className="text-center py-8 text-slate-500 dark:text-slate-400">
                    Need at least 2 measurements to show a chart.
                </div>
            </div>
        );
    }

    return (
        <div className="card p-6">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
                <h2 className="section-title">Progress Chart</h2>
                <div className="flex gap-1 bg-slate-100 dark:bg-slate-800 rounded-lg p-1">
                    {TIME_RANGES.map((r) => (
                        <button
                            key={r.value}
                            onClick={() => setRange(r.value)}
                            className={`px-3 py-1 text-xs font-medium rounded-md transition-colors ${
                                range === r.value
                                    ? "bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 shadow-sm"
                                    : "text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-300"
                            }`}
                        >
                            {r.label}
                        </button>
                    ))}
                </div>
            </div>

            {/* Series toggles */}
            <div className="flex flex-wrap gap-2 mb-4">
                {SERIES.filter((s) => data.some((d) => d[s.key] !== null)).map((s) => {
                    const active = !hiddenSeries.has(s.key);
                    return (
                        <button
                            key={s.key}
                            onClick={() => toggleSeries(s.key)}
                            className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium transition-all border ${
                                active
                                    ? "text-white border-transparent"
                                    : "bg-white dark:bg-slate-900 text-slate-500 dark:text-slate-400 border-slate-200 dark:border-slate-700"
                            }`}
                            style={active ? { backgroundColor: s.color, borderColor: s.color } : {}}
                        >
                            <span
                                className="w-2 h-2 rounded-full inline-block"
                                style={{ backgroundColor: active ? "white" : s.color }}
                            />
                            {s.label}
                        </button>
                    );
                })}
            </div>

            {visibleSeries.length === 0 ? (
                <div className="text-center py-8 text-slate-500 dark:text-slate-400 text-sm">
                    Select a metric above to display it on the chart.
                </div>
            ) : (
                <ResponsiveContainer width="100%" height={300}>
                    <LineChart data={data} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                        <XAxis dataKey="date" tick={{ fontSize: 12 }} stroke="#94a3b8" />
                        <YAxis tick={{ fontSize: 12 }} stroke="#94a3b8" />
                        <Tooltip
                            contentStyle={{
                                backgroundColor: "#fff",
                                border: "1px solid #e2e8f0",
                                borderRadius: "0.75rem",
                                fontSize: "0.875rem",
                            }}
                        />
                        <Legend />
                        {visibleSeries.map((s) => (
                            <Line
                                key={s.key}
                                type="monotone"
                                dataKey={s.key}
                                name={s.label}
                                stroke={s.color}
                                strokeWidth={2}
                                dot={{ r: 3 }}
                                connectNulls
                            />
                        ))}
                    </LineChart>
                </ResponsiveContainer>
            )}
        </div>
    );
}
