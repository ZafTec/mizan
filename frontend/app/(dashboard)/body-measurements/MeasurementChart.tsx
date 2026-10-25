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
import type {
  Formatter,
  ValueType,
  NameType,
} from "recharts/types/component/DefaultTooltipContent";
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
  { key: "waist", label: "Waist", color: "var(--chart-1)", group: "core" },
  { key: "hips", label: "Hips", color: "var(--chart-4)", group: "core" },
  { key: "chest", label: "Chest", color: "var(--chart-5)", group: "core" },
  {
    key: "leftArm",
    label: "Left Arm",
    color: "var(--chart-2)",
    group: "limbs",
  },
  {
    key: "rightArm",
    label: "Right Arm",
    color: "var(--chart-3)",
    group: "limbs",
  },
  {
    key: "leftThigh",
    label: "Left Thigh",
    color: "var(--foreground)",
    group: "limbs",
  },
  {
    key: "rightThigh",
    label: "Right Thigh",
    color: "var(--muted-foreground)",
    group: "limbs",
  },
] as const;

type CircKey = (typeof CIRCUMFERENCE_SERIES)[number]["key"];

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

const legendFormatter = (value: string) => (
  <span
    style={{ color: "var(--foreground)", fontSize: "12px", fontWeight: 600 }}
  >
    {value}
  </span>
);

const formatCompositionTooltip: Formatter<ValueType, NameType> = (
  value,
  name,
) => {
  const label = String(name ?? "");

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

const formatCircumferenceTooltip: Formatter<ValueType, NameType> = (
  value,
  name,
) => {
  const label = String(name ?? "");

  if (value === undefined) {
    return ["-", label];
  }

  if (Array.isArray(value)) {
    return [`${value.join(" / ")} cm`, label];
  }

  return [`${value} cm`, label];
};

function TimeRangePicker({
  range,
  onChange,
}: {
  range: TimeRange;
  onChange: (r: TimeRange) => void;
}) {
  return (
    <div role="group" aria-label="Period" className="flex gap-4">
      {TIME_RANGES.map((r) => (
        <button
          key={r.value}
          type="button"
          onClick={() => onChange(r.value)}
          aria-pressed={range === r.value}
          className={`num min-h-11 text-sm transition-colors ${range === r.value ? "font-semibold text-foreground" : "log-muted hover:text-foreground"}`}
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
      <ComposedChart
        data={data}
        margin={{ top: 5, right: 30, left: 0, bottom: 5 }}
      >
        <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
        <XAxis
          dataKey="date"
          tick={{ fontSize: 11 }}
          stroke="var(--muted-foreground)"
        />
        <YAxis
          yAxisId="kg"
          domain={["dataMin - 1", "dataMax + 1"]}
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
        <Tooltip {...chartStyle} formatter={formatCompositionTooltip} />
        <Legend formatter={legendFormatter} />
        {hasWeight && (
          <Line
            yAxisId="kg"
            type="monotone"
            dataKey="weight"
            name="Weight"
            stroke="var(--foreground)"
            strokeWidth={2.5}
            dot={{ r: 3, fill: "var(--foreground)" }}
            connectNulls
          />
        )}
        {hasMuscle && (
          <Line
            yAxisId="kg"
            type="monotone"
            dataKey="muscle"
            name="Muscle"
            stroke="var(--chart-1)"
            strokeWidth={2.5}
            dot={{ r: 3, fill: "var(--chart-1)" }}
            connectNulls
          />
        )}
        {hasBodyFat && (
          <Line
            yAxisId="pct"
            type="monotone"
            dataKey="bodyFat"
            name="Body Fat"
            stroke="var(--chart-4)"
            strokeWidth={2.5}
            dot={{ r: 3, fill: "var(--chart-4)" }}
            connectNulls
          />
        )}
        {hasGoalWeight && (
          <Line
            yAxisId="kg"
            type="stepAfter"
            dataKey="goalWeight"
            name="Weight Goal"
            stroke="var(--foreground)"
            strokeDasharray="6 3"
            strokeWidth={1.5}
            dot={false}
            connectNulls
          />
        )}
        {hasGoalMuscle && (
          <Line
            yAxisId="kg"
            type="stepAfter"
            dataKey="goalMuscle"
            name="Muscle Goal"
            stroke="var(--chart-1)"
            strokeDasharray="6 3"
            strokeWidth={1.5}
            dot={false}
            connectNulls
          />
        )}
        {hasGoalBodyFat && (
          <Line
            yAxisId="pct"
            type="stepAfter"
            dataKey="goalBodyFat"
            name="BF Goal"
            stroke="var(--chart-4)"
            strokeDasharray="6 3"
            strokeWidth={1.5}
            dot={false}
            connectNulls
          />
        )}
      </ComposedChart>
    </ResponsiveContainer>
  );
}

function CircumferenceChart({
  data,
  active,
  onToggle,
}: {
  data: ReturnType<typeof buildData>;
  active: Set<CircKey>;
  onToggle: (k: CircKey) => void;
}) {
  const available = CIRCUMFERENCE_SERIES.filter((s) =>
    data.some((d) => d[s.key] !== null),
  );
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
          <p className="text-[10px] uppercase tracking-wider text-charcoal-blue-400 dark:text-charcoal-blue-500 font-semibold mb-1.5">
            Core
          </p>
          <div className="flex flex-wrap gap-2">
            <ToggleGroup
              series={coreAvail}
              active={active}
              onToggle={onToggle}
            />
          </div>
        </div>
      )}
      {limbsAvail.length > 0 && (
        <div className="mb-4">
          <p className="text-[10px] uppercase tracking-wider text-charcoal-blue-400 dark:text-charcoal-blue-500 font-semibold mb-1.5">
            Limbs
          </p>
          <div className="flex flex-wrap gap-2">
            <ToggleGroup
              series={limbsAvail}
              active={active}
              onToggle={onToggle}
            />
          </div>
        </div>
      )}

      {visible.length === 0 ? (
        <div className="text-center py-8 text-charcoal-blue-500 dark:text-charcoal-blue-400 text-sm">
          Select a measurement above to display it.
        </div>
      ) : (
        <ResponsiveContainer width="100%" height={280}>
          <LineChart
            data={data}
            margin={{ top: 5, right: 20, left: 0, bottom: 5 }}
          >
            <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
            <XAxis
              dataKey="date"
              tick={{ fontSize: 11 }}
              stroke="var(--muted-foreground)"
            />
            <YAxis
              tick={{ fontSize: 11 }}
              stroke="var(--muted-foreground)"
              tickFormatter={(v) => `${v}cm`}
            />
            <Tooltip {...chartStyle} formatter={formatCircumferenceTooltip} />
            <Legend formatter={legendFormatter} />
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
            type="button"
            aria-pressed={on}
            onClick={() => onToggle(s.key)}
            className={`inline-flex min-h-9 items-center gap-1.5 text-xs transition-colors ${on ? "font-medium text-foreground" : "log-muted hover:text-foreground"}`}
          >
            <span
              aria-hidden="true"
              className={`inline-block h-2 w-2 rounded-full ${on ? "" : "opacity-40"}`}
              style={{ backgroundColor: s.color }}
            />
            {s.label}
          </button>
        );
      })}
    </>
  );
}

function EmptyChart({ message }: { message: string }) {
  return (
    <div className="text-center py-10 text-charcoal-blue-500 dark:text-charcoal-blue-400 text-sm">
      {message}
    </div>
  );
}

function findGoalForDate(
  date: string,
  goalHistory: UserGoal[],
): UserGoal | undefined {
  const d = new Date(date);
  for (const g of goalHistory) {
    if (new Date(g.createdAt) <= d) return g;
  }
  return undefined;
}

function buildData(
  measurements: BodyMeasurement[],
  goalHistory: UserGoal[] = [],
) {
  return measurements.map((m) => {
    const goal = findGoalForDate(m.date, goalHistory);
    return {
      date: new Date(m.date).toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
      }),
      weight: m.weightKg ?? null,
      bodyFat: m.bodyFatPercentage ?? null,
      muscle: m.muscleMassKg ?? null,
      goalWeight: goal?.targetWeight ?? null,
      goalBodyFat: goal?.targetBodyFatPercentage ?? null,
      goalMuscle: goal?.targetMuscleMassKg ?? null,
      waist: m.waistCm ?? null,
      hips: m.hipsCm ?? null,
      chest: m.chestCm ?? null,
      leftArm: m.leftArmCm ?? null,
      rightArm: m.rightArmCm ?? null,
      leftThigh: m.leftThighCm ?? null,
      rightThigh: m.rightThighCm ?? null,
    };
  });
}

export default function MeasurementChart({
  measurements,
  goalHistory = [],
}: {
  measurements: BodyMeasurement[];
  goalHistory?: UserGoal[];
}) {
  const [range, setRange] = useState<TimeRange>("3M");
  const [tab, setTab] = useState<Tab>("composition");
  const [activeCirc, setActiveCirc] = useState<Set<CircKey>>(
    new Set(["waist", "hips", "chest"]),
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
      <section aria-labelledby="trend-title">
        <header className="log-section-header">
          <h2 id="trend-title">Trend</h2>
        </header>
        <p className="log-empty log-muted text-sm">
          A second measurement draws the first line.
        </p>
      </section>
    );
  }

  return (
    <section aria-labelledby="trend-title" className="space-y-4">
      <header className="log-section-header">
        <div role="tablist" aria-label="Measurement" className="flex gap-5">
          {(
            [
              { id: "composition", label: "Composition" },
              { id: "circumference", label: "Circumference" },
            ] as const
          ).map((item) => (
            <button
              key={item.id}
              type="button"
              role="tab"
              aria-selected={tab === item.id}
              onClick={() => setTab(item.id)}
              id={item.id === "composition" ? "trend-title" : undefined}
              className={`min-h-11 text-xl font-semibold transition-colors ${tab === item.id ? "text-foreground" : "log-muted hover:text-foreground"}`}
            >
              {item.label}
            </button>
          ))}
        </div>
      </header>
      <TimeRangePicker range={range} onChange={setRange} />

      {tab === "composition" && (
        <p className="text-xs log-muted">
          Weight and muscle on the left axis in kg, body fat on the right in
          percent. A dashed line is a goal.
        </p>
      )}

      {tab === "composition" ? (
        <CompositionChart data={data} />
      ) : (
        <CircumferenceChart
          data={data}
          active={activeCirc}
          onToggle={toggleCirc}
        />
      )}
    </section>
  );
}
