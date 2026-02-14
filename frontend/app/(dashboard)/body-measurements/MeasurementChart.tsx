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

function getDateCutoff(range: TimeRange): Date | null {
    if (range === "ALL") return null;
    const now = new Date();
    const months = { "1M": 1, "3M": 3, "6M": 6, "1Y": 12 }[range];
    now.setMonth(now.getMonth() - months);
    return now;
}

export default function MeasurementChart({ measurements }: { measurements: BodyMeasurement[] }) {
    const [range, setRange] = useState<TimeRange>("3M");

    const cutoff = getDateCutoff(range);
    const filtered = measurements
        .filter((m) => !cutoff || new Date(m.date) >= cutoff)
        .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

    const data = filtered.map((m) => ({
        date: new Date(m.date).toLocaleDateString(undefined, { month: "short", day: "numeric" }),
        weight: m.weightKg ?? null,
        bodyFat: m.bodyFatPercentage ?? null,
        muscle: m.muscleMassKg ?? null,
    }));

    const hasWeight = data.some((d) => d.weight !== null);
    const hasBodyFat = data.some((d) => d.bodyFat !== null);
    const hasMuscle = data.some((d) => d.muscle !== null);

    if (data.length < 2) {
        return (
            <div className="card p-6">
                <h2 className="section-title mb-4">Progress Chart</h2>
                <div className="text-center py-8 text-slate-500">
                    Need at least 2 measurements to show a chart.
                </div>
            </div>
        );
    }

    return (
        <div className="card p-6">
            <div className="flex items-center justify-between mb-6">
                <h2 className="section-title">Progress Chart</h2>
                <div className="flex gap-1 bg-slate-100 rounded-lg p-1">
                    {TIME_RANGES.map((r) => (
                        <button
                            key={r.value}
                            onClick={() => setRange(r.value)}
                            className={`px-3 py-1 text-xs font-medium rounded-md transition-colors ${
                                range === r.value
                                    ? "bg-white text-slate-900 shadow-sm"
                                    : "text-slate-500 hover:text-slate-700"
                            }`}
                        >
                            {r.label}
                        </button>
                    ))}
                </div>
            </div>
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
                    {hasWeight && (
                        <Line
                            type="monotone"
                            dataKey="weight"
                            name="Weight (kg)"
                            stroke="#6366f1"
                            strokeWidth={2}
                            dot={{ r: 3 }}
                            connectNulls
                        />
                    )}
                    {hasBodyFat && (
                        <Line
                            type="monotone"
                            dataKey="bodyFat"
                            name="Body Fat (%)"
                            stroke="#f43f5e"
                            strokeWidth={2}
                            dot={{ r: 3 }}
                            connectNulls
                        />
                    )}
                    {hasMuscle && (
                        <Line
                            type="monotone"
                            dataKey="muscle"
                            name="Muscle (kg)"
                            stroke="#8b5cf6"
                            strokeWidth={2}
                            dot={{ r: 3 }}
                            connectNulls
                        />
                    )}
                </LineChart>
            </ResponsiveContainer>
        </div>
    );
}
