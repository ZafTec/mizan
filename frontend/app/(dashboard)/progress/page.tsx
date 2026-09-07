import Link from "next/link";
import { getUserServer } from "@/helper/session";
import {
  getLogNutrition,
  getLogMeasurements,
  getLogWorkouts,
} from "@/data/log";
import { dateInTimeZone, shiftDate, formatLogDate } from "@/lib/log-date";
import ProgressCharts from "@/components/log/ProgressCharts";

export const metadata = { title: "Progress · Mizan" };

export default async function ProgressPage({
  searchParams,
}: {
  searchParams: Promise<{ days?: string }>;
}) {
  const [user, params] = await Promise.all([getUserServer(), searchParams]);
  const days = [7, 30, 90].includes(Number(params.days))
    ? Number(params.days)
    : 30;
  const end = dateInTimeZone(user.timeZoneId || "UTC");
  const start = shiftDate(end, -(days - 1));
  const [nutrition, measurements, workouts] = await Promise.all([
    getLogNutrition(days, end),
    getLogMeasurements(start, end),
    getLogWorkouts(start, end),
  ]);
  return (
    <div className="log-page">
      <header className="log-page-header">
        <div>
          <h1>Progress</h1>
          <p className="log-muted mt-2">
            {formatLogDate(start)} – {formatLogDate(end)}. A longer view of your
            log.
          </p>
        </div>
        <Link href="/goal" className="text-link">
          Manage targets
        </Link>
      </header>
      <nav className="period-tabs" aria-label="Progress period">
        {[7, 30, 90].map((value) => (
          <Link
            key={value}
            href={`/progress?days=${value}`}
            aria-current={days === value ? "page" : undefined}
          >
            {value} days
          </Link>
        ))}
      </nav>
      <ProgressCharts
        nutrition={nutrition}
        measurements={measurements}
        workouts={workouts}
        days={days}
        end={end}
      />
    </div>
  );
}
