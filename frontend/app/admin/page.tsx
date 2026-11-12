import Link from "next/link";
import { getAuditLogs } from "@/data/audit";
import {
  getAdminOverviewServer,
  getSystemHealthServer,
} from "@/lib/api/admin-ops.server";
import type { AdminAttention } from "@/lib/api/admin-ops";
import { Icon } from "@/components/ui/icon";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import HealthGrid from "./HealthGrid";

export const metadata = {
  title: "Admin overview | Mizan",
  description: "What needs an administrator, and how the system is doing",
};

const count = new Intl.NumberFormat("en-GB");

function waited(since: string | null | undefined): string | undefined {
  if (!since) return undefined;
  const minutes = Math.max(
    0,
    Math.round((Date.now() - new Date(since).getTime()) / 60000),
  );
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.round(minutes / 60);
  if (hours < 48) return `${hours} h`;
  return `${Math.round(hours / 24)} days`;
}

/** The waiting work, oldest first where the API says how old; empty rows are left out. */
function needs(attention: AdminAttention) {
  const deadFor = waited(attention.oldestDeadJobAt);
  const contactFor = waited(attention.oldestContactMessageAt);
  return [
    {
      href: "/admin/jobs?status=DeadLettered",
      title: "Jobs that gave up",
      detail: deadFor ? `Oldest gave up ${deadFor} ago` : "Out of attempts",
      count: attention.deadJobs,
    },
    {
      href: "/admin/mail?status=Failed",
      title: "Emails that failed",
      detail: "In the last 24 hours",
      count: attention.failedEmails,
    },
    {
      href: "/admin/contact?status=New",
      title: "New contact messages",
      detail: contactFor ? `Oldest waiting ${contactFor}` : "Not handled yet",
      count: attention.newContactMessages,
    },
    {
      href: "/admin/prices",
      title: "Price reports to review",
      detail: "Reported by people, not yet accepted or rejected",
      count: attention.openPriceReports,
    },
    {
      href: "/admin/moderation",
      title: "Reported posts and comments",
      detail: "Social feed",
      count: attention.openContentReports,
    },
  ].filter((row) => row.count > 0);
}

export default async function AdminOverviewPage() {
  const [overview, health, audit] = await Promise.all([
    getAdminOverviewServer(),
    getSystemHealthServer(),
    getAuditLogs({ pageSize: 6 }),
  ]);

  const waiting = overview ? needs(overview.attention) : [];

  return (
    <div className="flex flex-col gap-10 [&>header]:mb-0">
      <AdminPageHeader
        title="Overview"
        lead="What needs you first, then how each part of the system is doing."
      />

      {overview ? (
        <div className="grid grid-cols-3 border-t border-b border-t-foreground border-b-border">
          <Fact
            label="People"
            value={count.format(overview.totalUsers)}
            note={`${count.format(overview.joinedThisWeek)} joined this week`}
            href="/admin/users"
          />
          <Fact
            label="Seen in 24 hours"
            value={count.format(overview.seenLastDay)}
            note={`${count.format(overview.activeSessions)} open sessions`}
            href="/admin/sessions"
          />
          <Fact
            label="Suspended"
            value={count.format(overview.bannedUsers)}
            note={`${count.format(overview.activeTrainers)} coaches`}
            href="/admin/users?banned=true"
          />
        </div>
      ) : (
        <p role="alert" className="text-[14px] text-destructive">
          The overview did not load. The API may be down.
        </p>
      )}

      <div className="grid gap-10 lg:grid-cols-2">
        <section aria-labelledby="needs-title" className="flex flex-col">
          <div className="log-section-header">
            <h2 id="needs-title">Needs you</h2>
          </div>
          {waiting.length === 0 ? (
            <p className="log-empty text-[14px] text-muted-foreground">
              Nothing is waiting for you.
            </p>
          ) : (
            <ul>
              {waiting.map((row) => (
                <li key={row.href}>
                  <Link
                    href={row.href}
                    className="ledger-row transition-colors duration-150 hover:bg-muted/60"
                  >
                    <span className="flex min-w-0 flex-1 flex-col">
                      <span className="text-[14px] font-medium">
                        {row.title}
                      </span>
                      <span className="text-[12px] text-muted-foreground">
                        {row.detail}
                      </span>
                    </span>
                    <span className="text-[16px] font-semibold tabular-nums">
                      {count.format(row.count)}
                    </span>
                    <Icon
                      name="arrowRight"
                      size={16}
                      className="text-muted-foreground"
                    />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>

        <HealthGrid health={health} />
      </div>

      <section aria-labelledby="changes-title" className="flex flex-col">
        <div className="log-section-header">
          <h2 id="changes-title">Recent changes</h2>
          <Link href="/admin/audit-logs" className="text-link text-[13px]">
            Audit log
          </Link>
        </div>
        {audit.logs.length === 0 ? (
          <p className="log-empty text-[14px] text-muted-foreground">
            Nothing has changed yet.
          </p>
        ) : (
          <ul>
            {audit.logs.map((entry) => (
              <li
                key={entry.id}
                className="grid grid-cols-[88px_minmax(0,1fr)] gap-x-4 gap-y-0.5 border-b border-border py-2.5 text-[14px] sm:grid-cols-[88px_220px_minmax(0,1fr)]"
              >
                <time
                  dateTime={entry.timestamp}
                  className="text-[12px] text-muted-foreground tabular-nums"
                >
                  {new Date(entry.timestamp).toLocaleString("en-GB", {
                    day: "numeric",
                    month: "short",
                    hour: "2-digit",
                    minute: "2-digit",
                    timeZone: "UTC",
                  })}
                </time>
                <span className="truncate font-medium">
                  {entry.userEmail ?? "System"}
                </span>
                <span className="col-start-2 text-charcoal-blue-700 sm:col-start-auto dark:text-charcoal-blue-200">
                  {entry.action} {entry.entityType}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function Fact({
  label,
  value,
  note,
  href,
}: {
  label: string;
  value: string;
  note: string;
  href: string;
}) {
  return (
    <Link
      href={href}
      className="flex flex-col gap-0.5 border-r border-border py-3.5 pr-4 pl-4 transition-colors duration-150 first:pl-0 last:border-r-0 hover:bg-muted/60"
    >
      <span className="text-[12px] text-muted-foreground">{label}</span>
      <span className="text-[26px] leading-tight font-semibold tabular-nums max-sm:text-[22px]">
        {value}
      </span>
      <span className="text-[12px] text-muted-foreground">{note}</span>
    </Link>
  );
}
