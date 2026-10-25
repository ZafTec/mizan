import FeatureFlags from "@/components/admin/platform/FeatureFlags";
import { listFeatureFlagsServer } from "@/lib/api/admin-platform.server";

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
    <div className="log-page space-y-8" data-testid="admin-feature-flags-page">
      <header className="log-page-header block!">
        <p className="eyebrow">Administration</p>
        <h1 className="mt-1">Feature flags</h1>
        <p className="log-muted mt-2">
          A household&apos;s override wins over the default. A feature that is
          off answers every surface with the same message.
        </p>
      </header>

      <section aria-label="Flag summary" className="facts-strip">
        <div>
          <p className="text-sm log-muted">Flags</p>
          <p className="num nutrition-value">{flags.length}</p>
        </div>
        <div>
          <p className="text-sm log-muted">Off by default</p>
          <p className="num nutrition-value">{changed}</p>
        </div>
        <div>
          <p className="text-sm log-muted">Household overrides</p>
          <p className="num nutrition-value">{overrides}</p>
        </div>
      </section>

      <FeatureFlags flags={flags} />
    </div>
  );
}
