"use client";

import { useState } from "react";
import {
    ComposedChart,
    LineChart,
    Line,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    ResponsiveContainer,
    Legend,
} from "recharts";
import type { Formatter, ValueType } from "recharts/types/component/DefaultTooltipContent";
import type { BodyMeasurement } from "@/data/bodyMeasurement";
import type { UserGoal } from "@/data/goal";

type TimeRange = "1M" | "3M" | "6M" | "1Y" | "ALL";
type Tab = "composition" | "circumference";

const TIME_RANGES: { label: string; value: TimeRange }[] = [
    { label: "1M", value: "1M" },
    { label: "3M", value: "3M" },
    { label: "6M", value: "6M" },
    { label: "1Y", value: "1Y" },
    { label: "All", value: "ALL" },
];

const CIRCUMFERENCE_SERIES = [
    { key: "waist",      label: "Waist",       color: "#f97316", group: "core" },
    { key: "hips",       label: "Hips",         color: "#ec4899", group: "core" },
    { key: "chest",      label: "Chest",        color: "#06b6d4", group: "core" },
    { key: "leftArm",    label: "Left Arm",     color: "#10b981", group: "limbs" },
    { key: "rightArm",   label: "Right Arm",    color: "#84cc16", group: "limbs" },
    { key: "leftThigh",  label: "Left Thigh",   color: "#f59e0b", group: "limbs" },
    { key: "rightThigh", label: "Right Thigh",  color: "#eab308", group: "limbs" },
] as const;

type CircKey = typeof CIRCUMFERENCE_SERIES[number]["key"];

function getDateCutoff(range: TimeRange): Date | null {
    if (range === "ALL") return null;
    const now = new Date();
    const months = { "1M": 1, "3M": 3, "6M": 6, "1Y": 12 }[range];
    now.setMonth(now.getMonth() - months);
    return now;
}

const chartStyle = {
    contentStyle: {
        backgroundColor: "var(--popover)",
        border: "1px solid var(--border)",
        color: "var(--popover-foreground)",
        borderRadius: "0.75rem",
        fontSize: "0.8125rem",
        boxShadow: "0 4px 6px -1px rgb(0 0 0 / 0.1)",
    },
};

const formatCompositionTooltip: Formatter<ValueType, string> = (value, name) => {
    const label = name ?? "";

    if (value === undefined) {
        return ["-", label];
    }

    if (Array.isArray(value)) {
        return [value.join(" / "), label];
    }

    if (label.includes("Body Fat") || label.includes("BF")) {
        return [`${value}%`, label];
    }

    return [`${value} kg`, label];
};

const formatCircumferenceTooltip: Formatter<ValueType, string> = (value, name) => {
    const label = name ?? "";

    if (value === undefined) {
        return ["-", label];
    }

    if (Array.isArray(value)) {
        return [`${value.join(" / ")} cm`, label];
    }

    return [`${value} cm`, label];
};

function TimeRangePicker({ range, onChange }: { range: TimeRange; onChange: (r: TimeRange) => void }) {
    return (
        <div className="flex gap-1 bg-slate-100 dark:bg-slate-800 rounded-lg p-1">
            {TIME_RANGES.map((r) => (
                <button
                    key={r.value}
                    onClick={() => onChange(r.value)}
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
    );
}

function CompositionChart({ data }: { data: ReturnType<typeof buildData> }) {
    const hasWeight = data.some((d) => d.weight !== null);
    const hasMuscle = data.some((d) => d.muscle !== null);
    const hasBodyFat = data.some((d) => d.bodyFat !== null);
    const hasGoalWeight = data.some((d) => d.goalWeight !== null);
    const hasGoalBodyFat = data.some((d) => d.goalBodyFat !== null);
    const hasGoalMuscle = data.some((d) => d.goalMuscle !== null);

    if (!hasWeight && !hasMuscle && !hasBodyFat) {
        return <EmptyChart message="No body composition data in this period." />;
    }

    return (
        <ResponsiveContainer width="100%" height={280}>
            <ComposedChart data={data} margin={{ top: 5, right: 30, left: 0, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
                <XAxis dataKey="date" tick={{ fontSize: 11 }} stroke="var(--muted-foreground)" />
                <YAxis
                    yAxisId="kg"
                    tick={{ fontSize: 11 }}
                    stroke="var(--muted-foreground)"
                    tickFormatter={(v) => `${v}kg`}
                />
                <YAxis
                    yAxisId="pct"
                    orientation="right"
                    tick={{ fontSize: 11 }}
                    stroke="var(--muted-foreground)"
                    tickFormatter={(v) => `${v}%`}
                />
                <Tooltip
                    {...chartStyle}
                    formatter={formatCompositionTooltip}
                />
                <Legend />
                {hasWeight && (
                    <Line yAxisId="kg" type="monotone" dataKey="weight" name="Weight" stroke="#2563eb" strokeWidth={2.5} dot={{ r: 3, fill: "#2563eb" }} connectNulls />
                )}
                {hasMuscle && (
                    <Line yAxisId="kg" type="monotone" dataKey="muscle" name="Muscle" stroke="#16a34a" strokeWidth={2.5} dot={{ r: 3, fill: "#16a34a" }} connectNulls />
                )}
                {hasBodyFat && (
                    <Line yAxisId="pct" type="monotone" dataKey="bodyFat" name="Body Fat" stroke="#dc2626" strokeWidth={2.5} dot={{ r: 3, fill: "#dc2626" }} connectNulls />
                )}
                {hasGoalWeight && (
                    <Line yAxisId="kg" type="stepAfter" dataKey="goalWeight" name="Weight Goal" stroke="#2563eb" strokeDasharray="6 3" strokeWidth={1.5} dot={false} connectNulls />
                )}
                {hasGoalMuscle && (
                    <Line yAxisId="kg" type="stepAfter" dataKey="goalMuscle" name="Muscle Goal" stroke="#16a34a" strokeDasharray="6 3" strokeWidth={1.5} dot={false} connectNulls />
                )}
                {hasGoalBodyFat && (
                    <Line yAxisId="pct" type="stepAfter" dataKey="goalBodyFat" name="BF Goal" stroke="#dc2626" strokeDasharray="6 3" strokeWidth={1.5} dot={false} connectNulls />
                )}
            </ComposedChart>
        </ResponsiveContainer>
    );
}

function CircumferenceChart({ data, active, onToggle }: {
    data: ReturnType<typeof buildData>;
    active: Set<CircKey>;
    onToggle: (k: CircKey) => void;
}) {
    const available = CIRCUMFERENCE_SERIES.filter((s) => data.some((d) => d[s.key] !== null));
    const visible = available.filter((s) => active.has(s.key));

    const coreAvail = available.filter((s) => s.group === "core");
    const limbsAvail = available.filter((s) => s.group === "limbs");

    if (available.length === 0) {
        return <EmptyChart message="No circumference data in this period." />;
    }

    return (
        <>
            {/* Grouped toggles */}
            {coreAvail.length > 0 && (
                <div className="mb-3">
                    <p className="text-[10px] uppercase tracking-wider text-slate-400 dark:text-slate-500 font-semibold mb-1.5">Core</p>
                    <div className="flex flex-wrap gap-2">
                        <ToggleGroup series={coreAvail} active={active} onToggle={onToggle} />
                    </div>
                </div>
            )}
            {limbsAvail.length > 0 && (
                <div className="mb-4">
                    <p className="text-[10px] uppercase tracking-wider text-slate-400 dark:text-slate-500 font-semibold mb-1.5">Limbs</p>
                    <div className="flex flex-wrap gap-2">
                        <ToggleGroup series={limbsAvail} active={active} onToggle={onToggle} />
                    </div>
                </div>
            )}

            {visible.length === 0 ? (
                <div className="text-center py-8 text-slate-500 dark:text-slate-400 text-sm">
                    Select a measurement above to display it.
                </div>
            ) : (
                <ResponsiveContainer width="100%" height={280}>
                    <LineChart data={data} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
                        <XAxis dataKey="date" tick={{ fontSize: 11 }} stroke="var(--muted-foreground)" />
                        <YAxis tick={{ fontSize: 11 }} stroke="var(--muted-foreground)" tickFormatter={(v) => `${v}cm`} />
                        <Tooltip
                            {...chartStyle}
                            formatter={formatCircumferenceTooltip}
                        />
                        <Legend />
                        {visible.map((s) => (
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
        </>
    );
}

function ToggleGroup({
    series,
    active,
    onToggle,
}: {
    series: readonly { key: CircKey; label: string; color: string }[];
    active: Set<CircKey>;
    onToggle: (k: CircKey) => void;
}) {
    return (
        <>
            {series.map((s) => {
                const on = active.has(s.key);
                return (
                    <button
                        key={s.key}
                        onClick={() => onToggle(s.key)}
                        className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium transition-all border ${
                            on
                                ? "text-white border-transparent"
                                : "bg-white dark:bg-slate-900 text-slate-500 dark:text-slate-400 border-slate-200 dark:border-slate-700"
                        }`}
                        style={on ? { backgroundColor: s.color, borderColor: s.color } : {}}
                    >
                        <span className="w-2 h-2 rounded-full inline-block" style={{ backgroundColor: on ? "white" : s.color }} />
                        {s.label}
                    </button>
                );
            })}
        </>
    );
}

function EmptyChart({ message }: { message: string }) {
    return (
        <div className="text-center py-10 text-slate-500 dark:text-slate-400 text-sm">{message}</div>
    );
}

function findGoalForDate(date: string, goalHistory: UserGoal[]): UserGoal | undefined {
    const d = new Date(date);
    for (const g of goalHistory) {
        if (new Date(g.createdAt) <= d) return g;
    }
    return undefined;
}

function buildData(measurements: BodyMeasurement[], goalHistory: UserGoal[] = []) {
    return measurements.map((m) => {
        const goal = findGoalForDate(m.date, goalHistory);
        return {
            date: new Date(m.date).toLocaleDateString(undefined, { month: "short", day: "numeric" }),
            weight:     m.weightKg ?? null,
            bodyFat:    m.bodyFatPercentage ?? null,
            muscle:     m.muscleMassKg ?? null,
            goalWeight: goal?.targetWeight ?? null,
            goalBodyFat: goal?.targetBodyFatPercentage ?? null,
            goalMuscle: goal?.targetMuscleMassKg ?? null,
            waist:      m.waistCm ?? null,
            hips:       m.hipsCm ?? null,
            chest:      m.chestCm ?? null,
            leftArm:    m.leftArmCm ?? null,
            rightArm:   m.rightArmCm ?? null,
            leftThigh:  m.leftThighCm ?? null,
            rightThigh: m.rightThighCm ?? null,
        };
    });
}

export default function MeasurementChart({ measurements, goalHistory = [] }: { measurements: BodyMeasurement[]; goalHistory?: UserGoal[] }) {
    const [range, setRange] = useState<TimeRange>("3M");
    const [tab, setTab] = useState<Tab>("composition");
    const [activeCirc, setActiveCirc] = useState<Set<CircKey>>(
        new Set(["waist", "hips", "chest"])
    );

    const toggleCirc = (key: CircKey) => {
        setActiveCirc((prev) => {
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

    const data = buildData(filtered, goalHistory);

    if (measurements.length < 2) {
        return (
            <div className="card p-6">
                <h2 className="section-title mb-4">Progress Chart</h2>
                <div className="text-center py-8 text-slate-500 dark:text-slate-400">
                    Add at least 2 measurements to see your progress chart.
                </div>
            </div>
        );
    }

    return (
        <div className="card p-6">
            {/* Header row */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-5">
                <div className="flex gap-1 bg-slate-100 dark:bg-slate-800 rounded-lg p-1 w-fit">
                    <button
                        onClick={() => setTab("composition")}
                        className={`px-4 py-1.5 text-xs font-semibold rounded-md transition-colors ${
                            tab === "composition"
                                ? "bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 shadow-sm"
                                : "text-slate-500 dark:text-slate-400 hover:text-slate-700"
                        }`}
                    >
                        Body Composition
                    </button>
                    <button
                        onClick={() => setTab("circumference")}
                        className={`px-4 py-1.5 text-xs font-semibold rounded-md transition-colors ${
                            tab === "circumference"
                                ? "bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 shadow-sm"
                                : "text-slate-500 dark:text-slate-400 hover:text-slate-700"
                        }`}
                    >
                        Circumference
                    </button>
                </div>
                <TimeRangePicker range={range} onChange={setRange} />
            </div>

            {/* Composition legend note */}
            {tab === "composition" && (
                <p className="text-[11px] text-slate-400 dark:text-slate-500 mb-4">
                    <span className="text-blue-600 font-medium">Weight</span> &amp; <span className="text-green-600 font-medium">Muscle</span> use the left axis (kg) · <span className="text-red-600 font-medium">Body Fat</span> uses the right axis (%) · Dashed lines = goals
                </p>
            )}

            {tab === "composition" ? (
                <CompositionChart data={data} />
            ) : (
                <CircumferenceChart data={data} active={activeCirc} onToggle={toggleCirc} />
            )}
        </div>
    );
}
