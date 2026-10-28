import ManualPrices from "@/components/admin/prices/ManualPrices";
import PriceRegions from "@/components/admin/prices/PriceRegions";
import PriceReports from "@/components/admin/prices/PriceReports";
import PriceReviewQueue from "@/components/admin/prices/PriceReviewQueue";
import PriceSources from "@/components/admin/prices/PriceSources";
import { REPORT_STATUSES } from "@/lib/api/prices";
import {
  listAdminPriceRegionsServer,
  listPriceAliasesServer,
  listPriceReportsServer,
  listPriceSourcesServer,
} from "@/lib/api/prices.server";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Prices | Mizan admin",
  description: "Regions, sources, the review queue, manual prices and reports",
};

/**
 * /admin/prices: everything that feeds the price book
 * (docs/PLANNING.md#prices). Each section writes through lib/api/prices and
 * refreshes this page, so every list is the API's after a change.
 */
export default async function AdminPricesPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;
  const status =
    REPORT_STATUSES.find((value) => value === params.reports) ?? "Open";
  const [regions, sources, queue, reports] = await Promise.all([
    listAdminPriceRegionsServer(),
    listPriceSourcesServer(),
    listPriceAliasesServer({ queue: true, pageSize: 50 }),
    listPriceReportsServer(status),
  ]);

  return (
    <div className="log-page space-y-10" data-testid="admin-prices-page">
      <header className="log-page-header block!">
        <p className="eyebrow">Administration</p>
        <h1 className="mt-1">Prices</h1>
        <p className="text-muted-foreground mt-2">
          Where prices come from, the products waiting for a food, and what
          people report.
        </p>
      </header>

      <section aria-label="Price summary" className="facts-strip">
        <div>
          <p className="text-sm text-muted-foreground">Sources</p>
          <p className="tabular-nums nutrition-value">{sources.length}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">To review</p>
          <p className="tabular-nums nutrition-value">{queue.totalCount}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">{status} reports</p>
          <p className="tabular-nums nutrition-value">{reports.totalCount}</p>
        </div>
      </section>

      <PriceRegions regions={regions} />
      <PriceSources sources={sources} regions={regions} />
      <PriceReviewQueue aliases={queue.items} total={queue.totalCount} />
      <ManualPrices regions={regions} />
      <PriceReports reports={reports.items} status={status} />
    </div>
  );
}
