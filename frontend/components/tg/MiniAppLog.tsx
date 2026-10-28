"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getSession } from "@/lib/auth-client";
import MealLogForm from "@/components/logging/MealLogForm";

/** The meal form in the Mini App; saving says so and leads back to /tg. */
export default function MiniAppLog() {
  const [timeZone, setTimeZone] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);
  const [, setBusy] = useState(false);

  useEffect(() => {
    getSession()
      .then((user) => setTimeZone(user.timeZoneId || "UTC"))
      .catch(() => setTimeZone("UTC"));
  }, []);

  return (
    <div className="flex flex-col gap-5" data-testid="tg-log">
      <header className="flex flex-col gap-1">
        <Link href="/tg" className="text-link min-h-11 self-start text-sm">
          Today
        </Link>
        <h1 className="text-2xl font-semibold">Log a meal</h1>
      </header>
      {saved ? (
        <section className="space-y-4">
          <p role="status" className="text-base">
            {saved}
          </p>
          <div className="flex flex-wrap gap-3">
            <Link href="/tg" className="btn-primary min-h-11">
              Back to today
            </Link>
            <button
              type="button"
              className="btn-secondary min-h-11"
              onClick={() => setSaved(null)}
            >
              Log another
            </button>
          </div>
        </section>
      ) : timeZone ? (
        <MealLogForm
          timeZone={timeZone}
          onSaved={setSaved}
          onBusyChange={setBusy}
        />
      ) : (
        <p role="status" className="log-muted">
          Loading…
        </p>
      )}
    </div>
  );
}
