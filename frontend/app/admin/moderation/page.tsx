import Image from "next/image";
import Link from "next/link";
import { serverApi } from "@/lib/api.server";
import ModerationActions from "./ModerationActions";
import type { components } from "@/types/api.generated";

type Report = components["schemas"]["ContentReportDto"];
type Analytics = components["schemas"]["SocialAnalyticsDto"];

export default async function AdminModerationPage() {
  const [reports, analytics] = await Promise.all([
    serverApi<{ items: Report[]; totalCount: number }>(
      "/api/admin/social/reports?status=Open&pageSize=50",
    ),
    serverApi<Analytics>("/api/admin/social/analytics"),
  ]);
  return (
    <div className="space-y-6">
      <header className="flex items-end justify-between">
        <div>
          <p className="eyebrow">Moderation</p>
          <h1 className="mt-2 text-3xl font-semibold sm:text-4xl">
            Review queue
          </h1>
        </div>
        <Link href="/admin/exercises" className="btn-secondary">
          Exercise approvals
        </Link>
      </header>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
        {[
          ["Profiles", analytics.profiles],
          ["Followers", analytics.acceptedFollows],
          ["Feed items", analytics.feedItems],
          ["Open reports", analytics.openReports],
          ["Actioned", analytics.actionedReports],
        ].map(([label, value]) => (
          <div key={String(label)} className="card p-4">
            <p className="text-xs uppercase text-charcoal-blue-500">{label}</p>
            <p className="mt-2 text-2xl font-bold">{value}</p>
          </div>
        ))}
      </div>
      {reports.items.length === 0 ? (
        <div className="card p-10 text-center">
          <Image
            src="/illustrations/empty-moderation.svg"
            alt=""
            width={240}
            height={190}
            className="mx-auto h-auto w-60"
          />
          <h2 className="mt-4 text-xl font-semibold">Queue cleared</h2>
        </div>
      ) : (
        <div className="space-y-3">
          {reports.items.map((report) => (
            <article
              key={report.id}
              className="card flex flex-col gap-4 p-5 sm:flex-row sm:items-center"
            >
              <div className="flex-1">
                <p className="text-xs uppercase text-charcoal-blue-500">
                  {report.targetType} · {report.targetId}
                </p>
                <p className="mt-2">{report.reason}</p>
                <p className="mt-1 text-xs text-charcoal-blue-400">
                  {new Date(report.createdAt).toLocaleString()}
                </p>
              </div>
              <ModerationActions id={report.id} />
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
