import Link from "next/link";
import type { SystemHealth } from "@/lib/api/admin-ops";
import { HEALTH_CHECKS, HEALTH_STATUS } from "@/lib/admin-ops/labels";
import { Pill, type PillTone } from "@/components/ui/data-table";

/**
 * One ruled row per service from GET /api/admin/overview/health: its name, the
 * API's detail line and the status word with a dot. A row whose cause is fixed
 * elsewhere links there; a service with nothing configured says so, never a
 * number.
 */
export default function HealthGrid({
  health,
}: {
  health: SystemHealth | null;
}) {
  return (
    <section aria-labelledby="health-title" className="flex flex-col">
      <div className="log-section-header">
        <h2 id="health-title">System health</h2>
        {health && (
          <p className="text-[12px] text-muted-foreground">
            Measured{" "}
            {new Date(health.measuredAt).toLocaleTimeString("en-GB", {
              hour: "2-digit",
              minute: "2-digit",
              timeZone: "UTC",
            })}{" "}
            UTC
          </p>
        )}
      </div>
      {!health ? (
        <p role="alert" className="py-4 text-[14px] text-destructive">
          The health checks did not answer. The API may be down.
        </p>
      ) : (
        <ul>
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
                className="grid min-h-11 grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-0.5 border-b border-border py-2 sm:grid-cols-[150px_minmax(0,1fr)_auto]"
              >
                <span className="text-[14px] font-medium">{meta.label}</span>
                <span className="order-3 col-span-2 min-w-0 text-[12px] text-muted-foreground sm:order-none sm:col-span-1">
                  {check.detail}
                  {meta.href ? (
                    <>
                      {" "}
                      <Link href={meta.href} className="text-link">
                        {notConfigured
                          ? `Set up in ${meta.linkText}`
                          : meta.linkText}
                      </Link>
                    </>
                  ) : null}
                </span>
                <Pill
                  tone={status.tone as PillTone}
                  className="justify-self-end text-[12px] font-semibold"
                >
                  {status.label}
                </Pill>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
