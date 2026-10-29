import Link from "next/link";
import { listCampaignsServer } from "@/lib/api/admin-notifications.server";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Notifications | Mizan admin",
  description: "Notification campaigns and lifecycle messages",
};

const PHASES: Record<string, string> = {
  draft: "Draft",
  scheduled: "Scheduled",
  live: "Live",
  sent: "Sent",
  paused: "Paused",
  cancelled: "Cancelled",
};

/**
 * /admin/notifications: every campaign and lifecycle message with its phase
 * and delivery totals, newest first (docs/ADMIN.md#notification-campaigns).
 */
export default async function AdminNotificationsPage() {
  const campaigns = await listCampaignsServer();

  return (
    <div className="log-page space-y-8" data-testid="admin-notifications-page">
      <header className="log-page-header block!">
        <p className="eyebrow">Administration</p>
        <div className="mt-1 flex flex-wrap items-center justify-between gap-3">
          <h1>Notifications</h1>
          <Link href="/admin/notifications/new" className="btn-primary">
            New campaign
          </Link>
        </div>
        <p className="text-muted-foreground mt-2">
          News and offers reach only people who turned them on. Lifecycle
          messages follow each person&apos;s quiet hours.
        </p>
      </header>

      {campaigns.length === 0 ? (
        <p className="text-muted-foreground">No campaigns yet.</p>
      ) : (
        <ul className="divide-y divide-charcoal-blue-200 border-y border-charcoal-blue-200 dark:divide-charcoal-blue-700 dark:border-charcoal-blue-700">
          {campaigns.map((c) => (
            <li key={c.id}>
              <Link
                href={`/admin/notifications/${c.id}`}
                className="flex flex-col gap-1 py-3 hover:bg-charcoal-blue-50 sm:flex-row sm:items-center sm:justify-between dark:hover:bg-charcoal-blue-900"
              >
                <span className="min-w-0">
                  <span className="block font-medium break-words">
                    {c.name}
                  </span>
                  <span className="block text-sm text-muted-foreground break-words">
                    {c.purpose === "marketing"
                      ? "News and offers"
                      : "Lifecycle"}{" "}
                    · {c.summary} · {c.audienceLabel}
                  </span>
                </span>
                <span className="flex shrink-0 gap-4 text-sm">
                  <span>{PHASES[c.phase] ?? c.phase}</span>
                  <span className="tabular-nums text-muted-foreground">
                    {c.totals.sent} sent
                  </span>
                  {c.totals.failed > 0 && (
                    <span className="tabular-nums text-burnt-peach-700 dark:text-burnt-peach-400">
                      {c.totals.failed} failed
                    </span>
                  )}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
