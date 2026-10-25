import { serverApi } from "@/lib/api.server";
import type { RuntimeSettings } from "./admin-settings";

/** The settings page's read. It forwards the session cookie; the API checks the role. */
export const getRuntimeSettingsServer = () =>
  serverApi<RuntimeSettings>("/api/admin/settings");
