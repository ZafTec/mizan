import { serverApi } from "@/lib/api.server";
import {
  usageQuery,
  type FeatureFlag,
  type PlatformUsage,
  type UsageFilters,
  type UsageLimitSubject,
  type UsageScope,
} from "./admin-platform";

/**
 * Server-component reads for the platform administration pages. They forward
 * the session cookie and answer with the generated types.
 */
export const listFeatureFlagsServer = () =>
  serverApi<FeatureFlag[]>("/api/admin/feature-flags");

export const listUsageLimitsServer = (scope: UsageScope) =>
  serverApi<UsageLimitSubject[]>(
    `/api/admin/usage-limits?scope=${encodeURIComponent(scope)}`,
  );

export const getPlatformUsageServer = (filters: UsageFilters) =>
  serverApi<PlatformUsage>(`/api/admin/usage?${usageQuery(filters)}`);
