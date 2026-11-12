import Link from "next/link";
import { listCampaignsServer } from "@/lib/api/admin-notifications.server";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Campaigns | Mizan admin",
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
    <div data-testid="admin-notifications-page">
      <AdminPageHeader
        title="Campaigns"
        lead="News and offers reach only people who turned them on; lifecycle messages follow each person's quiet hours."
        action={
          <Link href="/admin/notifications/new" className="btn-primary">
            New campaign
          </Link>
        }
      />

      {campaigns.length === 0 ? (
        <p className="log-empty border-t border-foreground text-[14px] text-muted-foreground">
          No campaigns yet.
        </p>
      ) : (
        <ul className="border-t border-foreground">
          {campaigns.map((c) => (
            <li key={c.id}>
              <Link
                href={`/admin/notifications/${c.id}`}
                className="flex min-h-14 flex-col gap-1 border-b border-border py-3 transition-colors duration-150 hover:bg-muted/60 sm:flex-row sm:items-center sm:justify-between"
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
                    <span className="tabular-nums text-destructive">
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
