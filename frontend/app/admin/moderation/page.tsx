import { serverApi } from "@/lib/api.server";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import AdminFacts from "@/components/admin/AdminFacts";
import ModerationActions from "./ModerationActions";
import type { components } from "@/types/api.generated";

type Report = components["schemas"]["ContentReportDto"];
type Analytics = components["schemas"]["SocialAnalyticsDto"];

export const metadata = {
  title: "Moderation | Mizan admin",
  description: "Reported posts and comments from the social feed",
};

export default async function AdminModerationPage() {
  const [reports, analytics] = await Promise.all([
    serverApi<{ items: Report[]; totalCount: number }>(
      "/api/admin/social/reports?status=Open&pageSize=50",
    ),
    serverApi<Analytics>("/api/admin/social/analytics"),
  ]);
  return (
    <div className="flex flex-col gap-10 [&>header]:mb-0">
      <AdminPageHeader
        title="Moderation"
        lead="Reported posts and comments from the social feed."
      />

      <AdminFacts
        facts={[
          { label: "Open reports", value: analytics.openReports },
          { label: "Actioned", value: analytics.actionedReports },
          {
            label: "Profiles",
            value: analytics.profiles,
            note: `${analytics.acceptedFollows} follows · ${analytics.feedItems} feed items`,
          },
        ]}
      />

      <section aria-labelledby="reports-title" className="flex flex-col">
        <div className="log-section-header">
          <h2 id="reports-title" className="text-[20px] font-semibold">
            Open reports
          </h2>
        </div>
        {reports.items.length === 0 ? (
          <p className="log-empty text-[14px] text-muted-foreground">
            Nothing is waiting for review.
          </p>
        ) : (
          <ul>
            {reports.items.map((report) => (
              <li
                key={report.id}
                className="flex flex-col gap-3 border-b border-border py-3 sm:flex-row sm:items-center"
              >
                <div className="min-w-0 flex-1">
                  <p className="text-[12px] break-all text-muted-foreground">
                    {report.targetType} · {report.targetId}
                  </p>
                  <p className="mt-1 text-[14px] break-words">
                    {report.reason}
                  </p>
                  <p className="mt-1 text-[12px] text-muted-foreground tabular-nums">
                    {new Date(report.createdAt).toLocaleString()}
                  </p>
                </div>
                <ModerationActions id={report.id} />
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
