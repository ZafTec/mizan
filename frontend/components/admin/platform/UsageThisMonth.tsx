import Link from "next/link";
import { getPlatformUsageServer } from "@/lib/api/admin-platform.server";
import { usageQuery, type UsageFilters } from "@/lib/api/admin-platform";

/**
 * A short summary of this month's AI usage for one household or one user,
 * linking to the usage page with the same filter (docs/ADMIN.md#usage).
 */
export default async function UsageThisMonth({
  householdId,
  userId,
}: {
  householdId?: string;
  userId?: string;
}) {
  const filters: UsageFilters = {
    groupBy: householdId ? "user" : "feature",
    householdId,
    userId,
  };
  const usage = await getPlatformUsageServer(filters).catch(() => null);

  return (
    <section
      aria-labelledby="usage-this-month"
      className="card p-5 sm:p-6"
      data-testid="usage-this-month"
    >
      <h2
        id="usage-this-month"
        className="mb-3 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50"
      >
        Usage this month
      </h2>
      {usage ? (
        <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
          <div>
            <dt className="text-muted-foreground">Requests</dt>
            <dd className="tabular-nums font-medium">
              {usage.total.requests.toLocaleString()}
            </dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Tokens</dt>
            <dd className="tabular-nums font-medium">
              {usage.total.tokens.toLocaleString()}
            </dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Cost</dt>
            <dd className="tabular-nums font-medium">
              {usage.total.cost.toLocaleString(undefined, {
                maximumFractionDigits: 4,
              })}
            </dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Refused</dt>
            <dd className="tabular-nums font-medium">
              {usage.total.refusals.toLocaleString()}
            </dd>
          </div>
        </dl>
      ) : (
        <p className="text-sm text-muted-foreground">Usage could not be loaded.</p>
      )}
      <Link
        href={`/admin/usage?${usageQuery(filters)}`}
        className="text-link mt-3"
      >
        See usage in detail
      </Link>
    </section>
  );
}
