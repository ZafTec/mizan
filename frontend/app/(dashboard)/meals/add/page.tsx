import { redirect } from "next/navigation";
import { isLogDate } from "@/lib/log-date";
export default async function AddMealPage({
  searchParams,
}: {
  searchParams: Promise<{ date?: string }>;
}) {
  const { date } = await searchParams;
  redirect(`/today?log=meal${isLogDate(date) ? `&date=${date}` : ""}`);
}
