import Link from "next/link";
import UsageLimits from "@/components/admin/platform/UsageLimits";
import { USAGE_SCOPES, type UsageScope } from "@/lib/api/admin-platform";
import { listUsageLimitsServer } from "@/lib/api/admin-platform.server";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

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
    <div
      className="flex flex-col gap-8 [&>header]:mb-0 [&_h2]:text-[20px] [&_h2]:leading-[1.3] [&_h2]:font-semibold"
      data-testid="admin-usage-limits-page"
    >
      <AdminPageHeader
        title="Usage limits"
        lead="A cap comes from the person, then the household, the plan and the tier default; the assistant enforces exactly these."
      />

      <nav aria-label="Limit scopes" className="period-tabs mb-0!">
        {USAGE_SCOPES.map((value) => (
          <Link
            key={value}
            href={`/admin/usage-limits?scope=${value}`}
            aria-current={value === scope ? "page" : undefined}
          >
            {TAB_LABEL[value]}
          </Link>
        ))}
      </nav>

      <UsageLimits key={scope} scope={scope} subjects={subjects} />
    </div>
  );
}
