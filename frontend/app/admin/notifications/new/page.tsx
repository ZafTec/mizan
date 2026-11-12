import CampaignEditor from "@/components/admin/notifications/CampaignEditor";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export const metadata = {
  title: "New campaign | Mizan admin",
  description: "Write a notification campaign or lifecycle message",
};

/** /admin/notifications/new: a campaign is saved as a draft and started from its own page. */
export default function NewCampaignPage() {
  return (
    <div className="[&_h2]:text-[20px] [&_h2]:leading-[1.3] [&_h2]:font-semibold">
      <AdminPageHeader
        title="New campaign"
        lead="It is saved as a draft and started from its own page."
        back={{ href: "/admin/notifications", label: "Campaigns" }}
      />
      <CampaignEditor />
    </div>
  );
}
