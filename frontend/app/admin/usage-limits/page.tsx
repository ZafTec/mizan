import Link from "next/link";
import UsageLimits from "@/components/admin/platform/UsageLimits";
import { USAGE_SCOPES, type UsageScope } from "@/lib/api/admin-platform";
import { listUsageLimitsServer } from "@/lib/api/admin-platform.server";
import { cn } from "@/lib/utils";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Usage limits | Mizan admin",
  description: "AI allowance overrides by plan, household or user",
};

const TAB_LABEL: Record<UsageScope, string> = {
  Plan: "Plans",
  Household: "Households",
  User: "Users",
};

/**
 * /admin/usage-limits: allowance overrides, one tab per scope
 * (docs/ADMIN.md#usage-limits). A user's override wins over their
 * household's, which wins over their plan's, which wins over the tier.
 */
export default async function AdminUsageLimitsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;
  const scope = USAGE_SCOPES.find((value) => value === params.scope) ?? "Plan";
  const subjects = await listUsageLimitsServer(scope);

  return (
    <div className="log-page space-y-8" data-testid="admin-usage-limits-page">
      <header className="log-page-header block!">
        <p className="eyebrow">Administration</p>
        <h1 className="mt-1">Usage limits</h1>
        <p className="log-muted mt-2">
          Each cap comes from the user, then the household, then the plan, then
          the tier default. The assistant enforces exactly these.
        </p>
      </header>

      <nav
        aria-label="Limit scopes"
        className="flex gap-1 border-b border-border"
      >
        {USAGE_SCOPES.map((value) => (
          <Link
            key={value}
            href={`/admin/usage-limits?scope=${value}`}
            aria-current={value === scope ? "page" : undefined}
            className={cn(
              "-mb-px min-h-11 border-b-2 px-3 py-2.5 text-sm font-medium",
              value === scope
                ? "border-charcoal-blue-900 text-charcoal-blue-900 dark:border-charcoal-blue-50 dark:text-charcoal-blue-50"
                : "border-transparent log-muted hover:text-charcoal-blue-800 dark:hover:text-charcoal-blue-100",
            )}
          >
            {TAB_LABEL[value]}
          </Link>
        ))}
      </nav>

      <UsageLimits key={scope} scope={scope} subjects={subjects} />
    </div>
  );
}
