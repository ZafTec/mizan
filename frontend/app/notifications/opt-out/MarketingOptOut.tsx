"use client";

import Link from "next/link";
import { useState } from "react";
import { optOutOfMarketing } from "@/lib/api/admin-notifications";
import { getErrorMessage } from "@/lib/toast";

export default function MarketingOptOut({ token }: { token: string | null }) {
  const [state, setState] = useState<"ready" | "busy" | "done">("ready");
  const [error, setError] = useState<string | null>(
    token
      ? null
      : "This link is missing its code. Turn news and offers off in Settings, Notifications.",
  );

  const optOut = async () => {
    if (!token) return;
    setState("busy");
    setError(null);
    try {
      await optOutOfMarketing(token);
      setState("done");
    } catch (e) {
      setError(getErrorMessage(e, "Could not turn them off"));
      setState("ready");
    }
  };

  if (state === "done") {
    return (
      <p role="status" data-testid="opt-out-done">
        News and offers are off, in the app, in Telegram, on your phone and by
        email. Turn them back on any time in{" "}
        <Link href="/profile/settings" className="underline">
          Settings
        </Link>
        .
      </p>
    );
  }

  return (
    <div className="space-y-4">
      <p className="text-muted-foreground">
        Stop product news and offers from Mizan on every channel. Reminders and
        account messages are not affected.
      </p>
      {error && (
        <p
          role="alert"
          className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
        >
          {error}
        </p>
      )}
      <button
        type="button"
        className="btn-primary w-full"
        disabled={!token || state === "busy"}
        onClick={optOut}
      >
        {state === "busy" ? "Turning off…" : "Turn off news and offers"}
      </button>
    </div>
  );
}
