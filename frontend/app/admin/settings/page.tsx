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
        <p className="log-muted mt-2">
          A value set here replaces the environment&apos;s from the next email,
          call or webhook, on every server. Clear it to go back to
          configuration. Secrets are stored encrypted and never shown.
        </p>
      </header>
      <RuntimeSettingsAdmin settings={settings} />
    </div>
  );
}
