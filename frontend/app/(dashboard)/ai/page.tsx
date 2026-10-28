import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";

/**
 * The assistant is a panel in the app shell now, not a page. `/ai` stays a working link: it opens
 * the panel over Today, on the named conversation when `?thread=` says which.
 */
export default async function AiPage({
  searchParams,
}: {
  searchParams: Promise<{ thread?: string | string[] }>;
}) {
  const { thread } = await searchParams;
  const id = Array.isArray(thread) ? thread[0] : thread;
  const query = new URLSearchParams({ assistant: "open" });
  if (id) query.set("thread", id);
  redirect(`/today?${query}`);
}
