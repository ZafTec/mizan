"use client";

import { useEffect, useState } from "react";
import { stopImpersonation } from "@/lib/auth-client";
import { appToast } from "@/lib/toast";
import type { User } from "@/lib/auth";

type Impersonation = NonNullable<User["impersonation"]>;

function minutesLeft(expiresAt: string) {
  return Math.max(0, Math.ceil((new Date(expiresAt).getTime() - Date.now()) / 60000));
}

/**
 * Shown on every screen while an administrator is viewing the site as someone else. It says whose account this is,
 * how long the view lasts, and gives one way out. It cannot be dismissed, because forgetting that changes are real
 * is the mistake it exists to prevent.
 */
export default function ImpersonationBanner({
  impersonation,
  userName,
}: {
  impersonation: Impersonation;
  userName: string;
}) {
  const [left, setLeft] = useState(() => minutesLeft(impersonation.expiresAt));
  const [leaving, setLeaving] = useState(false);

  useEffect(() => {
    const timer = window.setInterval(() => setLeft(minutesLeft(impersonation.expiresAt)), 30_000);
    return () => window.clearInterval(timer);
  }, [impersonation.expiresAt]);

  async function leave() {
    setLeaving(true);
    try {
      const { restored } = await stopImpersonation();
      // A full load, so the server renders as the administrator again.
      window.location.assign(restored ? "/admin/users" : "/login");
    } catch (error) {
      appToast.error(error, "Could not leave this view");
      setLeaving(false);
    }
  }

  return (
    <div
      role="status"
      className="sticky top-0 z-[60] flex flex-wrap items-center justify-between gap-x-4 gap-y-2 border-b border-burnt-peach-300 bg-burnt-peach-50 px-4 py-2 text-sm text-burnt-peach-900 dark:border-burnt-peach-500/40 dark:bg-burnt-peach-950 dark:text-burnt-peach-100"
    >
      <p className="min-w-0">
        <strong className="font-semibold">Viewing as {userName}.</strong>{" "}
        What you do here is real and is recorded under your name.{" "}
        <span className="tabular-nums">
          {left > 0 ? `${left} min left.` : "This view has ended."}
        </span>
      </p>
      <button
        type="button"
        onClick={leave}
        disabled={leaving}
        className="btn-secondary min-h-9 shrink-0 px-3 py-1"
      >
        {leaving ? "Leaving…" : "Back to admin"}
      </button>
    </div>
  );
}
