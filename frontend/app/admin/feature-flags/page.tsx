import FeatureFlags from "@/components/admin/platform/FeatureFlags";
import { listFeatureFlagsServer } from "@/lib/api/admin-platform.server";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Feature flags | Mizan admin",
  description: "Turn features on or off for everyone or for one household",
};

/**
 * /admin/feature-flags: the catalog flags, their defaults and the households
 * that answer differently (docs/ADMIN.md#feature-flags).
 */
export default async function AdminFeatureFlagsPage() {
  const flags = await listFeatureFlagsServer();
  const changed = flags.filter((flag) => !flag.defaultEnabled).length;
  const overrides = flags.reduce((sum, flag) => sum + flag.overrideCount, 0);

  return (
    <div
      className="flex flex-col gap-8 [&>header]:mb-0 [&_h2]:text-[20px] [&_h2]:leading-[1.3] [&_h2]:font-semibold"
      data-testid="admin-feature-flags-page"
    >
      <AdminPageHeader
        title="Feature flags"
        lead="A household's override wins over the default; a feature that is off answers every surface with the same message."
      />

      <section aria-label="Flag summary" className="facts-strip">
        <div>
          <p className="text-sm text-muted-foreground">Flags</p>
          <p className="tabular-nums nutrition-value">{flags.length}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Off by default</p>
          <p className="tabular-nums nutrition-value">{changed}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Household overrides</p>
          <p className="tabular-nums nutrition-value">{overrides}</p>
        </div>
      </section>

      <FeatureFlags flags={flags} />
    </div>
  );
}
