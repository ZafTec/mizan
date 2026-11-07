import { clientApi } from "@/lib/api.client";
import { resolvePublicApiOrigin } from "@/lib/api-base";
import { ApiError } from "@/lib/api";
import type { components } from "@/types/api.generated";

/**
 * Browser calls for the platform administration pages: feature flags and
 * their household overrides, AI allowance overrides, and the usage export
 * (docs/ADMIN.md). Every type comes from the generated OpenAPI document. The
 * API owns every rule and every number; these only send what the
 * administrator chose. Writes carry an Idempotency-Key so a retry is safe.
 */
type Schemas = components["schemas"];

export type FeatureFlag = Schemas["FeatureFlagDto"];
export type FeatureFlagOverride = Schemas["FeatureFlagOverrideDto"];
export type UsageLimitSubject = Schemas["UsageLimitSubjectDto"];
export type UsageLimitOverride = Schemas["UsageLimitOverrideDto"];
export type EffectiveUsageLimit = Schemas["EffectiveUsageLimitDto"];
export type SourcedLimit = Schemas["SourcedLimitDto"];
export type SetUsageLimitRequest = Schemas["SetUsageLimitRequest"];
export type PlatformUsage = Schemas["PlatformUsageDto"];
export type PlatformUsageRow = Schemas["PlatformUsageRowDto"];
export type AdminHouseholdPage = Schemas["AdminHouseholdSummaryPagedResult"];
export type AdminUserPage = Schemas["AdminUserDtoPagedResult"];

export const USAGE_SCOPES = ["Plan", "Household", "User"] as const;
export type UsageScope = (typeof USAGE_SCOPES)[number];

/** The allowance lines, in the order the API lists them. */
export const USAGE_LINES = [
  "Personal",
  "Planning",
  "Onboarding",
  "TrainerClient",
  "Eval",
] as const;

export const USAGE_GROUPS = ["household", "user", "plan", "feature"] as const;
export type UsageGroup = (typeof USAGE_GROUPS)[number];

function idempotent(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

const flagPath = (key: string) =>
  `/api/admin/feature-flags/${encodeURIComponent(key)}`;

export const setFeatureFlagDefault = (key: string, enabled: boolean) =>
  clientApi<FeatureFlag>(flagPath(key), {
    method: "PUT",
    body: { enabled } satisfies Schemas["SetFeatureFlagRequest"],
    headers: idempotent(),
  });

export const listFeatureFlagOverrides = (key: string) =>
  clientApi<FeatureFlagOverride[]>(`${flagPath(key)}/overrides`);

export const setFeatureFlagOverride = (
  key: string,
  householdId: string,
  enabled: boolean,
) =>
  clientApi<FeatureFlagOverride>(`${flagPath(key)}/overrides/${householdId}`, {
    method: "PUT",
    body: { enabled } satisfies Schemas["SetFeatureFlagRequest"],
    headers: idempotent(),
  });

export const clearFeatureFlagOverride = (key: string, householdId: string) =>
  clientApi<void>(`${flagPath(key)}/overrides/${householdId}`, {
    method: "DELETE",
    headers: idempotent(),
  });

/** Households by name, for adding an override. */
export const searchHouseholds = (term: string) =>
  clientApi<AdminHouseholdPage>(
    `/api/Households/admin/all?search=${encodeURIComponent(term)}&pageSize=8`,
  );

/** Accounts by name or email, for adding a user override. */
export const searchUsers = (term: string) =>
  clientApi<AdminUserPage>(
    `/api/admin/users?search=${encodeURIComponent(term)}&pageSize=8`,
  );

const limitPath = (scope: UsageScope, subjectId: string) =>
  `/api/admin/usage-limits/${scope}/${subjectId}`;

export const getUsageLimitSubject = (scope: UsageScope, subjectId: string) =>
  clientApi<UsageLimitSubject>(limitPath(scope, subjectId));

export const setUsageLimit = (
  scope: UsageScope,
  subjectId: string,
  body: SetUsageLimitRequest,
) =>
  clientApi<UsageLimitSubject>(limitPath(scope, subjectId), {
    method: "PUT",
    body,
    headers: idempotent(),
  });

export const clearUsageLimit = (
  scope: UsageScope,
  subjectId: string,
  line: string | null,
) =>
  clientApi<void>(
    `${limitPath(scope, subjectId)}${line ? `?line=${encodeURIComponent(line)}` : ""}`,
    { method: "DELETE", headers: idempotent() },
  );

export type UsageFilters = {
  from?: string;
  to?: string;
  groupBy?: UsageGroup;
  householdId?: string;
  userId?: string;
};

export function usageQuery(filters: UsageFilters): string {
  const params = new URLSearchParams();
  if (filters.groupBy) params.set("groupBy", filters.groupBy);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.householdId) params.set("householdId", filters.householdId);
  if (filters.userId) params.set("userId", filters.userId);
  return params.toString();
}

/**
 * The CSV the API writes for these filters, fetched with the session cookie
 * and handed to the browser as a download under the API's own file name.
 */
export async function downloadUsageCsv(filters: UsageFilters): Promise<void> {
  const response = await fetch(
    `${resolvePublicApiOrigin()}/api/admin/usage/export?${usageQuery(filters)}`,
    { credentials: "include" },
  );
  if (!response.ok) {
    let body: unknown = null;
    try {
      body = await response.json();
    } catch {
      // Not JSON; the status says enough.
    }
    throw new ApiError(response.status, response.statusText, body);
  }
  const name =
    /filename="?([^";]+)"?/.exec(
      response.headers.get("Content-Disposition") ?? "",
    )?.[1] ?? "mizan-usage.csv";
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = name;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
