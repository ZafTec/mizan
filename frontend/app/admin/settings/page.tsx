import RuntimeSettingsAdmin from "@/components/admin/settings/RuntimeSettingsAdmin";
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
    <div className="log-page space-y-10" data-testid="admin-settings-page">
      <header className="log-page-header block!">
        <p className="eyebrow">Administration</p>
        <h1 className="mt-1">Settings</h1>
        <p className="text-muted-foreground mt-2">
          Email, storage, AI, Telegram, billing and the rest are set here, not
          in the environment. A change applies from the next email, call or
          webhook, on every server and service. Clear a value to go back to
          its default. Secrets are stored encrypted and never shown.
        </p>
      </header>
      <RuntimeSettingsAdmin settings={settings} />
    </div>
  );
}
