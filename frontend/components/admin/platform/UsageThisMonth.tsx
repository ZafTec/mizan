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

  const facts = usage
    ? [
        { label: "Requests", value: usage.total.requests.toLocaleString() },
        { label: "Tokens", value: usage.total.tokens.toLocaleString() },
        {
          label: "Cost",
          value: usage.total.cost.toLocaleString(undefined, {
            maximumFractionDigits: 4,
          }),
        },
        { label: "Refused", value: usage.total.refusals.toLocaleString() },
      ]
    : [];

  return (
    <section
      aria-labelledby="usage-this-month"
      className="flex flex-col"
      data-testid="usage-this-month"
    >
      <div className="log-section-header">
        <h2 id="usage-this-month" className="text-[20px] font-semibold">
          Usage this month
        </h2>
        <Link
          href={`/admin/usage?${usageQuery(filters)}`}
          className="text-link"
        >
          See usage in detail
        </Link>
      </div>
      {usage ? (
        <dl className="grid grid-cols-2 border-b border-border sm:grid-cols-4">
          {facts.map((fact) => (
            <div key={fact.label} className="flex flex-col gap-0.5 py-3.5 pr-4">
              <dt className="text-[12px] text-muted-foreground">
                {fact.label}
              </dt>
              <dd className="text-[20px] leading-tight font-semibold tabular-nums">
                {fact.value}
              </dd>
            </div>
          ))}
        </dl>
      ) : (
        <p className="log-empty text-[14px] text-muted-foreground">
          Usage could not be loaded.
        </p>
      )}
    </section>
  );
}
