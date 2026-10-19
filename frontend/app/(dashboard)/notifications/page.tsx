import { serverApi } from "@/lib/api.server";
import NotificationList, { type NotificationDto } from "./NotificationList";

export const dynamic = "force-dynamic";

export default async function NotificationsPage() {
  const result = await serverApi<{ items: NotificationDto[]; unreadCount: number }>("/api/Notifications?page=1&pageSize=50");
  return (
    <div className="log-page space-y-8">
      <header className="log-page-header block!">
        <h1>Notifications</h1>
        <p className="log-muted mt-2">What happened while you were away.</p>
      </header>
      <NotificationList initial={result.items} />
    </div>
  );
}
