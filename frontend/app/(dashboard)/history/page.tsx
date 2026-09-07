import Link from "next/link";
import { ArrowRight, ChevronLeft, ChevronRight } from "lucide-react";
import { getUserServer } from "@/helper/session";
import {
  getLogNutrition,
  getLogWorkouts,
  getLogMeasurements,
} from "@/data/log";
import {
  dateInTimeZone,
  isLogDate,
  shiftDate,
  formatLogDate,
} from "@/lib/log-date";
import DateNavigator from "@/components/log/DateNavigator";

export const metadata = { title: "History · Mizan" };

export default async function HistoryPage({
  searchParams,
}: {
  searchParams: Promise<{ end?: string }>;
}) {
  const [user, params] = await Promise.all([getUserServer(), searchParams]);
  const today = dateInTimeZone(user.timeZoneId || "UTC");
  const end = isLogDate(params.end) && params.end <= today ? params.end : today;
  const start = shiftDate(end, -13);
  const [nutrition, workouts, measurements] = await Promise.all([
    getLogNutrition(14, end),
    getLogWorkouts(start, end),
    getLogMeasurements(start, end),
  ]);
  const days = Array.from({ length: 14 }, (_, index) => shiftDate(end, -index));
  return (
    <div className="log-page">
      <header className="log-page-header">
        <div>
          <h1>History</h1>
          <p className="log-muted mt-2">
            Your meals, training, and measurements. Day by day.
          </p>
        </div>
        <div>
          <p className="text-xs log-muted mb-2">Jump to a day</p>
          <DateNavigator date={end} today={today} />
        </div>
      </header>
      <div className="history-period">
        <h2>
          {formatLogDate(start, { month: "short", day: "numeric" })} –{" "}
          {formatLogDate(end, {
            month: "short",
            day: "numeric",
            year: "numeric",
          })}
        </h2>
        <div className="flex gap-1">
          <Link
            href={`/history?end=${shiftDate(start, -1)}`}
            className="icon-button"
            aria-label="Previous two weeks"
          >
            <ChevronLeft size={18} />
          </Link>
          {end < today ? (
            <Link
              href={`/history?end=${shiftDate(end, 14) > today ? today : shiftDate(end, 14)}`}
              className="icon-button"
              aria-label="Next two weeks"
            >
              <ChevronRight size={18} />
            </Link>
          ) : (
            <button
              disabled
              className="icon-button"
              aria-label="Next two weeks"
            >
              <ChevronRight size={18} />
            </button>
          )}
        </div>
      </div>
      <div className="history-column-labels">
        <span>Day</span>
        <span>Nutrition</span>
        <span>Activity</span>
        <span />
      </div>
      <div>
        {days.map((date) => {
          const food = nutrition.find((day) => day.date.slice(0, 10) === date);
          const training = workouts.filter(
            (workout) => workout.workoutDate.slice(0, 10) === date,
          );
          const body = measurements.filter(
            (measurement) => measurement.date.slice(0, 10) === date,
          );
          return (
            <Link
              className="history-row"
              href={`/today?date=${date}`}
              key={date}
              aria-label={`View log for ${formatLogDate(date)}`}
            >
              <div>
                <p className="font-medium">
                  {date === today
                    ? "Today"
                    : formatLogDate(date, { weekday: "long" })}
                </p>
                <p className="text-xs log-muted mt-1">
                  {formatLogDate(date, { month: "short", day: "numeric" })}
                </p>
              </div>
              <div>
                {food ? (
                  <>
                    <p className="num text-sm">
                      {Math.round(food.calories).toLocaleString()}{" "}
                      <span className="log-muted">kcal</span>
                    </p>
                    <p className="text-xs log-muted mt-1 num">
                      {Math.round(food.protein)} g protein
                    </p>
                  </>
                ) : (
                  <p className="text-sm log-muted">No meals logged</p>
                )}
              </div>
              <div className="history-activity text-sm">
                {training.length > 0 && (
                  <span>
                    {training.length}{" "}
                    {training.length === 1 ? "workout" : "workouts"}
                  </span>
                )}
                {body.length > 0 && (
                  <span className="num">
                    {body[0].weightKg != null
                      ? `${body[0].weightKg.toLocaleString(undefined, { maximumFractionDigits: 1 })} kg`
                      : "Measurements logged"}
                  </span>
                )}
                {!training.length && !body.length && (
                  <span className="log-muted">—</span>
                )}
              </div>
              <ArrowRight size={16} className="log-muted" />
            </Link>
          );
        })}
      </div>
      <p className="text-xs log-muted mt-5">
        Open any day to review or add to its log.
      </p>
    </div>
  );
}
