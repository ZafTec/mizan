import { redirect } from "next/navigation";
import { isLogDate } from "@/lib/log-date";
export default async function MealsPage({
  searchParams,
}: {
  searchParams: Promise<{ date?: string }>;
}) {
  const { date } = await searchParams;
  redirect(isLogDate(date) ? `/today?date=${date}` : "/history");
}
