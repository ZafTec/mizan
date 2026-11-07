import { redirect } from "next/navigation";

/**
 * Notifications are a side panel in the app shell, opened from the bell. `/notifications` stays a
 * working link (Telegram, email, the More list): it opens the panel over Today.
 */
export default function NotificationsPage() {
  redirect("/today?panel=notifications");
}
