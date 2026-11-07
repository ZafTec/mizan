import Link from "next/link";
import CampaignEditor from "@/components/admin/notifications/CampaignEditor";

export const metadata = {
  title: "New campaign | Mizan admin",
  description: "Write a notification campaign or lifecycle message",
};

/** /admin/notifications/new: a campaign is saved as a draft and started from its own page. */
export default function NewCampaignPage() {
  return (
    <div className="log-page space-y-6">
      <header className="log-page-header block!">
        <Link
          href="/admin/notifications"
          className="text-xs text-charcoal-blue-500 hover:text-verdigris-600 dark:text-charcoal-blue-400"
        >
          Notifications
        </Link>
        <h1 className="mt-1">New campaign</h1>
      </header>
      <CampaignEditor />
    </div>
  );
}
