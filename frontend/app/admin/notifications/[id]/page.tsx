import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError } from "@/lib/api";
import { getCampaignServer } from "@/lib/api/admin-notifications.server";
import CampaignControls from "@/components/admin/notifications/CampaignControls";
import CampaignEditor from "@/components/admin/notifications/CampaignEditor";
import { CHANNELS } from "@/lib/admin-notifications/schema";

export const dynamic = "force-dynamic";

const STATUS: Record<string, string> = {
  draft: "Draft",
  scheduled: "Scheduled",
  live: "Live",
  sent: "Sent",
  paused: "Paused",
  cancelled: "Cancelled",
};

const when = (value?: string | null) =>
  value
    ? new Date(value).toLocaleString("en-GB", {
        dateStyle: "medium",
        timeStyle: "short",
        timeZone: "UTC",
      }) + " UTC"
    : "None";

/**
 * /admin/notifications/[id]: one campaign's report and controls. A draft or
 * a paused campaign can also be changed here.
 */
export default async function CampaignPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  let campaign;
  try {
    campaign = await getCampaignServer(id);
  } catch (error) {
    if (
      error instanceof ApiError &&
      (error.status === 404 || error.status === 400)
    )
      notFound();
    throw error;
  }
  const editable = campaign.status === "draft" || campaign.status === "paused";

  return (
    <div className="log-page space-y-8" data-testid="admin-campaign-page">
      <header className="log-page-header block! space-y-2">
        <Link
          href="/admin/notifications"
          className="text-xs text-charcoal-blue-500 hover:text-verdigris-600 dark:text-charcoal-blue-400"
        >
          Notifications
        </Link>
        <h1 className="break-words">{campaign.name}</h1>
        <p className="text-muted-foreground">
          {STATUS[campaign.phase] ?? campaign.phase} ·{" "}
          {campaign.purpose === "marketing" ? "News and offers" : "Lifecycle"} ·{" "}
          {campaign.summary}
        </p>
      </header>

      <CampaignControls campaign={campaign} />

      <section aria-labelledby="campaign-report" className="space-y-3">
        <h2 id="campaign-report" className="text-lg font-semibold">
          Delivery
        </h2>
        <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm sm:grid-cols-4">
          <div>
            <dt className="text-muted-foreground">Audience confirmed</dt>
            <dd className="tabular-nums">
              {campaign.confirmedAudience ?? "Not started"}
            </dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Started</dt>
            <dd>{when(campaign.activatedAt)}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Last sent</dt>
            <dd>{when(campaign.lastRunAt)}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Next</dt>
            <dd>{when(campaign.nextRunAt)}</dd>
          </div>
        </dl>
        <div className="overflow-x-auto">
          <table
            className="w-full min-w-[28rem] text-sm"
            data-testid="campaign-report"
          >
            <thead>
              <tr className="text-left text-muted-foreground">
                <th className="py-2 pr-4 font-normal">Channel</th>
                <th className="py-2 pr-4 text-right font-normal">Queued</th>
                <th className="py-2 pr-4 text-right font-normal">Sent</th>
                <th className="py-2 pr-4 text-right font-normal">Failed</th>
                <th className="py-2 pr-4 text-right font-normal">Skipped</th>
                <th className="py-2 text-right font-normal">Opened</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-charcoal-blue-200 dark:divide-charcoal-blue-700">
              {campaign.report.map((r) => (
                <tr key={r.channel}>
                  <td className="py-2 pr-4">
                    {CHANNELS.find((c) => c.value === r.channel)?.label ??
                      r.channel}
                  </td>
                  <td className="py-2 pr-4 text-right tabular-nums">
                    {r.queued}
                  </td>
                  <td className="py-2 pr-4 text-right tabular-nums">
                    {r.sent}
                  </td>
                  <td className="py-2 pr-4 text-right tabular-nums">
                    {r.failed}
                  </td>
                  <td className="py-2 pr-4 text-right tabular-nums">
                    {r.skipped}
                  </td>
                  <td className="py-2 text-right tabular-nums">
                    {r.opened ?? "Not reported"}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="font-medium">
                <td className="py-2 pr-4">Total</td>
                <td className="py-2 pr-4 text-right tabular-nums">
                  {campaign.totals.queued}
                </td>
                <td className="py-2 pr-4 text-right tabular-nums">
                  {campaign.totals.sent}
                </td>
                <td className="py-2 pr-4 text-right tabular-nums">
                  {campaign.totals.failed}
                </td>
                <td className="py-2 pr-4 text-right tabular-nums">
                  {campaign.totals.skipped}
                </td>
                <td className="py-2 text-right tabular-nums">
                  {campaign.totals.opened}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
        <p className="text-xs text-muted-foreground">
          Skipped is a person whose choices left the channel out when it went.
          Only the app reports opened.
        </p>
      </section>

      {editable ? (
        <section aria-labelledby="campaign-edit" className="space-y-4">
          <h2 id="campaign-edit" className="text-lg font-semibold">
            Edit
          </h2>
          <CampaignEditor campaign={campaign} />
        </section>
      ) : (
        <p className="text-sm text-muted-foreground">
          {campaign.status === "active"
            ? "Pause the campaign to change it."
            : "A cancelled campaign cannot be changed."}
        </p>
      )}
    </div>
  );
}
