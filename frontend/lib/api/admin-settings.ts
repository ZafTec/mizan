import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";

/**
 * Browser calls for /admin/settings (docs/ADMIN.md#runtime-settings). The
 * catalog, the checks and the secrets live in the API: the page sends what the
 * administrator typed and shows what comes back, including the API's message
 * when a value is refused. A secret is never in a response.
 */
type Schemas = components["schemas"];

export type RuntimeSettings = Schemas["RuntimeSettingsDto"];
export type RuntimeSettingGroup = Schemas["RuntimeSettingGroupDto"];
export type RuntimeSetting = Schemas["RuntimeSettingDto"];
export type SetRuntimeSettingRequest = Schemas["SetRuntimeSettingRequest"];
export type SmtpTestResult = Schemas["SmtpTestResult"];
export type AiProviderTestResult = Schemas["AiProviderTestResult"];

function idempotent(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

const settingPath = (key: string) =>
  `/api/admin/settings/${encodeURIComponent(key)}`;

export const setRuntimeSetting = (key: string, value: string) =>
  clientApi<RuntimeSetting>(settingPath(key), {
    method: "PUT",
    body: { value } satisfies SetRuntimeSettingRequest,
    headers: idempotent(),
    // A refused value is the API answering the form, not a failure to log.
    expectedStatuses: [400],
  });

export const clearRuntimeSetting = (key: string) =>
  clientApi<RuntimeSetting>(settingPath(key), {
    method: "DELETE",
    headers: idempotent(),
  });

/** Queues one message to the signed-in administrator; the outbox sends it. */
export const testSmtp = () =>
  clientApi<SmtpTestResult>("/api/admin/settings/smtp/test", {
    method: "POST",
    headers: idempotent(),
  });

/** One tiny metered call on the evaluation line. */
export const testAiProvider = () =>
  clientApi<AiProviderTestResult>("/api/admin/settings/ai/test", {
    method: "POST",
    headers: idempotent(),
  });
