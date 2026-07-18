import { serverApi } from "@/lib/api.server";
import type { WorkoutStatsDto, WorkoutSummaryDto, WorkoutTemplateDto } from "@/types/workout";
import WorkoutDashboard from "./WorkoutDashboard";

export const dynamic = "force-dynamic";

type HistoryResult = { items: WorkoutSummaryDto[]; totalCount: number; page: number; pageSize: number; totalPages: number };

export default async function WorkoutsPage({ searchParams }: { searchParams: Promise<{ tab?: string }> }) {
  const params = await searchParams;
  const tab = params.tab === "log" || params.tab === "stats" || params.tab === "templates" ? params.tab : "history";
  const [history, templates, stats] = await Promise.all([
    serverApi<HistoryResult>("/api/Workouts?page=1&pageSize=20&sortBy=date&sortOrder=desc"),
    serverApi<WorkoutTemplateDto[]>("/api/WorkoutTemplates"),
    serverApi<WorkoutStatsDto>("/api/Workouts/stats"),
  ]);

  return <WorkoutDashboard initialTab={tab} initialHistory={history} initialTemplates={templates} initialStats={stats} />;
}
