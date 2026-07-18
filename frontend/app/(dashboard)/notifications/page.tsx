import { serverApi } from "@/lib/api.server";
import NotificationList, { type NotificationDto } from "./NotificationList";

export const dynamic = "force-dynamic";

export default async function NotificationsPage() {
  const result = await serverApi<{ items: NotificationDto[]; unreadCount: number }>("/api/Notifications?page=1&pageSize=50");
  return <div className="space-y-6"><header><p className="eyebrow">Inbox</p><div className="mt-2 flex items-end justify-between"><div><h1 className="text-3xl font-semibold tracking-tight sm:text-4xl">Notifications</h1><p className="mt-2 text-sm text-charcoal-blue-500">{result.unreadCount} unread</p></div></div></header><NotificationList initial={result.items} /></div>;
}
