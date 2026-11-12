import RuntimeSettingsAdmin from "@/components/admin/settings/RuntimeSettingsAdmin";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import { getRuntimeSettingsServer } from "@/lib/api/admin-settings.server";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Settings | Mizan admin",
  description:
    "Email, the AI provider and its limits, Telegram, push and billing",
};

/**
 * /admin/settings: runtime settings an administrator changes here instead of
 * in the environment (docs/ADMIN.md#runtime-settings). A value set here wins
 * over configuration; clearing it goes back.
 */
export default async function AdminSettingsPage() {
  const settings = await getRuntimeSettingsServer();

  return (
    <div data-testid="admin-settings-page">
      <AdminPageHeader
        title="Settings"
        lead="A change applies on every server without a redeploy; clearing a value goes back to its default, and secrets are stored encrypted and never shown."
      />
      <RuntimeSettingsAdmin settings={settings} />
    </div>
  );
}
