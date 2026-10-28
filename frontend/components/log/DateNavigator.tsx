"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { shiftDate } from "@/lib/log-date";

export default function DateNavigator({
  date,
  today,
}: {
  date: string;
  today: string;
}) {
  const router = useRouter();
  return (
    <div className="inline-flex items-center rounded-[3px] border border-border bg-card">
      <Link
        href={`/today?date=${shiftDate(date, -1)}`}
        className="icon-button"
        aria-label="Previous day"
      >
        <ChevronLeft size={18} />
      </Link>
      <label className="sr-only" htmlFor="log-date">
        Log date
      </label>
      <input
        id="log-date"
        type="date"
        value={date}
        max={today}
        className="min-h-11 w-[140px] min-w-0 bg-transparent px-2 text-[13px]"
        onChange={(event) => {
          if (event.target.value && event.target.value <= today)
            router.push(`/today?date=${event.target.value}`);
        }}
      />
      {date < today ? (
        <Link
          href={`/today?date=${shiftDate(date, 1)}`}
          className="icon-button"
          aria-label="Next day"
        >
          <ChevronRight size={18} />
        </Link>
      ) : (
        <button className="icon-button" disabled aria-label="Next day">
          <ChevronRight size={18} />
        </button>
      )}
    </div>
  );
}
