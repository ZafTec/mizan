"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  acceptPriceReport,
  REPORT_STATUSES,
  rejectPriceReport,
  type PriceReport,
} from "@/lib/api/prices";
import { formatMoney, formatPriceDate } from "@/lib/prices/format";
import { appToast } from "@/lib/toast";

const FILTER_LABELS: Record<(typeof REPORT_STATUSES)[number], string> = {
  Open: "Open",
  Accepted: "Accepted",
  Rejected: "Rejected",
};

/**
 * What people say a food costs, beside the price they were shown. Accepting
 * writes a Reported price; the reporter hears back either way.
 */
export default function PriceReports({
  reports,
  status,
}: {
  reports: PriceReport[];
  status: (typeof REPORT_STATUSES)[number];
}) {
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const section = useRef<HTMLElement>(null);

  // A notification links to /admin/prices#reports. The page streams in after
  // its loading state, so the router's own jump to the fragment finds nothing;
  // the section brings itself into view once it is there.
  useEffect(() => {
    if (window.location.hash === "#reports")
      section.current?.scrollIntoView({ block: "start" });
  }, []);

  const resolve = async (report: PriceReport, accept: boolean) => {
    setBusy(report.id);
    try {
      if (accept) await acceptPriceReport(report.id);
      else await rejectPriceReport(report.id);
      appToast.success(
        `Report on ${report.foodName ?? "the food"} ${accept ? "accepted" : "rejected"}`,
      );
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not resolve the report");
    } finally {
      setBusy(null);
    }
  };

  return (
    <section
      ref={section}
      id="reports"
      aria-labelledby="reports-title"
      className="scroll-mt-6"
    >
      <header className="log-section-header">
        <h2 id="reports-title">Reports</h2>
        <nav aria-label="Report status" className="flex gap-1 text-sm">
          {REPORT_STATUSES.map((value) => (
            <Link
              key={value}
              href={
                value === "Open"
                  ? "/admin/prices#reports"
                  : `/admin/prices?reports=${value}#reports`
              }
              scroll={false}
              aria-current={value === status ? "page" : undefined}
              className={`flex min-h-9 items-center px-2 ${value === status ? "font-semibold text-foreground" : "text-muted-foreground hover:text-foreground"}`}
            >
              {FILTER_LABELS[value]}
            </Link>
          ))}
        </nav>
      </header>
      {reports.length === 0 ? (
        <p className="log-empty">
          {status === "Open"
            ? "No open reports."
            : `No ${status.toLowerCase()} reports.`}
        </p>
      ) : (
        <ul>
          {reports.map((report) => (
            <li
              key={report.id}
              className="border-b border-border py-3"
              aria-label={`Report on ${report.foodName ?? "a food"}`}
            >
              <div className="flex flex-wrap items-start gap-3">
                <span className="min-w-0 flex-1 basis-56">
                  <Link
                    href={`/ingredients/${report.foodId}`}
                    className="break-words text-link"
                  >
                    {report.foodName ?? "Food"}
                  </Link>
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {[
                      report.userName,
                      report.regionCode,
                      formatPriceDate(report.createdAt),
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                  <span className="tabular-nums mt-2 block text-sm">
                    <span className="text-muted-foreground">Shown </span>
                    {report.shownPricePerKg != null
                      ? `${formatMoney(report.shownPricePerKg, report.currency)} per kg`
                      : "no price"}
                    <span className="text-muted-foreground"> · Suggested </span>
                    {report.suggestedPricePerKg != null
                      ? `${formatMoney(report.suggestedPricePerKg, report.currency)} per kg`
                      : `${formatMoney(report.suggestedPrice, report.currency)} for ${report.quantity} ${report.unit}`}
                  </span>
                  {report.suggestedPricePerKg != null && (
                    <span className="tabular-nums block text-xs text-muted-foreground">
                      {formatMoney(report.suggestedPrice, report.currency)} for{" "}
                      {report.quantity} {report.unit}
                    </span>
                  )}
                  {report.note && (
                    <span className="mt-1 block text-sm break-words">
                      {report.note}
                    </span>
                  )}
                  {report.status !== "Open" && (
                    <span className="mt-1 block text-xs text-muted-foreground">
                      {report.status}
                      {report.resolvedAt
                        ? ` ${formatPriceDate(report.resolvedAt)}`
                        : ""}
                    </span>
                  )}
                </span>
                {report.status === "Open" && (
                  <span className="flex shrink-0 gap-1">
                    <button
                      type="button"
                      className="btn-primary btn-sm min-h-9"
                      aria-label={`Accept the report on ${report.foodName ?? "the food"}`}
                      disabled={busy !== null}
                      onClick={() => resolve(report, true)}
                    >
                      Accept
                    </button>
                    <button
                      type="button"
                      className="btn-ghost btn-sm min-h-9"
                      aria-label={`Reject the report on ${report.foodName ?? "the food"}`}
                      disabled={busy !== null}
                      onClick={() => resolve(report, false)}
                    >
                      Reject
                    </button>
                  </span>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
