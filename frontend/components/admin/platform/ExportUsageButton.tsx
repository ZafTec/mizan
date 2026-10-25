"use client";

import { useState, useSyncExternalStore } from "react";
import { downloadUsageCsv, type UsageFilters } from "@/lib/api/admin-platform";
import { getErrorMessage } from "@/lib/toast";

const noSubscription = () => () => {};

/**
 * Downloads the API's CSV for the filters on screen, with the session cookie.
 * Disabled until the page is interactive, so an early click is not lost.
 */
export default function ExportUsageButton({
  filters,
}: {
  filters: UsageFilters;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const hydrated = useSyncExternalStore(
    noSubscription,
    () => true,
    () => false,
  );

  const download = async () => {
    setBusy(true);
    setError(null);
    try {
      await downloadUsageCsv(filters);
    } catch (caught) {
      setError(getErrorMessage(caught, "Could not export the usage."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col items-start gap-1">
      <button
        type="button"
        className="btn-secondary btn-sm min-h-11"
        onClick={download}
        disabled={busy || !hydrated}
      >
        {busy ? "Exporting…" : "Export CSV"}
      </button>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
