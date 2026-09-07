import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { serverApi } from "@/lib/api.server";
import { ApiError } from "@/lib/api";
import { getUserServer } from "@/helper/session";
import { dateInTimeZone, isLogDate } from "@/lib/log-date";
import type { WorkoutSummaryDto, WorkoutTemplateDto } from "@/types/workout";
import type { SocialProfileDto } from "@/types/social";
import WorkoutDashboard from "../../workouts/WorkoutDashboard";

export const metadata = { title: "Workout · Mizan" };

export default async function ActiveWorkoutPage({
  searchParams,
}: {
  searchParams: Promise<{ date?: string }>;
}) {
  const [user, params, history, templates, social] = await Promise.all([
    getUserServer(),
    searchParams,
    serverApi<{ items: WorkoutSummaryDto[]; totalCount: number }>(
      "/api/Workouts?page=1&pageSize=1&sortBy=date&sortOrder=desc",
    ),
    serverApi<WorkoutTemplateDto[]>("/api/WorkoutTemplates"),
    serverApi<SocialProfileDto>("/api/Social/profile", {
      expectedStatuses: [404],
    }).catch((error) => {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }),
  ]);
  const today = dateInTimeZone(user.timeZoneId || "UTC");
  const date =
    isLogDate(params.date) && params.date <= today ? params.date : today;
  return (
    <div className="log-page">
      <Link href={`/today?date=${date}`} className="text-link mb-4">
        <ArrowLeft size={15} /> Back to your log
      </Link>
      <WorkoutDashboard
        initialTab="log"
        initialHistory={history}
        initialTemplates={templates}
        defaultPublishWorkouts={social?.defaultPublishWorkouts ?? false}
        sessionOnly
        initialDate={date}
      />
    </div>
  );
}
