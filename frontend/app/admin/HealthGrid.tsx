import Link from "next/link";
import { Pill, type PillTone } from "@/components/ui/data-table";
import type { SystemHealth } from "@/lib/api/admin-ops";
import { HEALTH_CHECKS, HEALTH_STATUS } from "@/lib/admin-ops/labels";

/**
 * One tile per service from GET /api/admin/overview/health. A tile shows the
 * status word and the API's detail line, and links to the page where its cause
 * is fixed. A service with nothing configured says so, never a number.
 */
export default function HealthGrid({
  health,
}: {
  health: SystemHealth | null;
}) {
  return (
    <section aria-labelledby="health-title" className="space-y-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2
          id="health-title"
          className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50"
        >
          System health
        </h2>
        {health && (
          <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Measured{" "}
            {new Date(health.measuredAt).toLocaleTimeString("en-GB", {
              hour: "2-digit",
              minute: "2-digit",
              second: "2-digit",
              timeZone: "UTC",
            })}{" "}
            UTC, at most 30 seconds old
          </p>
        )}
      </div>
      {!health ? (
        <p
          role="alert"
          className="border border-burnt-peach-200 p-4 text-sm text-burnt-peach-700 dark:border-burnt-peach-800 dark:text-burnt-peach-400"
        >
          The health checks did not answer. The API may be down.
        </p>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {health.checks.map((check) => {
            const meta = HEALTH_CHECKS[check.name] ?? { label: check.name };
            const status = HEALTH_STATUS[check.status] ?? {
              label: check.status,
              tone: "neutral" as const,
            };
            const notConfigured = check.status === "notConfigured";
            return (
              <li
                key={check.name}
                aria-label={meta.label}
                className="flex flex-col gap-2 border border-charcoal-blue-200 p-4 dark:border-charcoal-blue-700"
              >
                <div className="flex items-center justify-between gap-3">
                  <h3 className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
                    {meta.label}
                  </h3>
                  <Pill tone={status.tone as PillTone}>{status.label}</Pill>
                </div>
                <p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
                  {check.detail}
                </p>
                {meta.href && (
                  <Link
                    href={meta.href}
                    className="mt-auto text-sm text-brand-700 hover:underline dark:text-brand-300"
                  >
                    {notConfigured
                      ? `Set up in ${meta.linkText}`
                      : meta.linkText}
                  </Link>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
