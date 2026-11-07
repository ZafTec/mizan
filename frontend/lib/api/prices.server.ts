import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import {
  aliasQuery,
  type FoodPriceQuote,
  type PriceAliasPage,
  type PriceAliasQuery,
  type PriceRegion,
  type PriceReportPage,
  type PriceReportStatus,
  type PriceSourceAdmin,
} from "./prices";

/**
 * Server-component reads for prices. They forward the session cookie and
 * answer with the generated types. The API tags these reads with `prices`
 * and the viewer's `price-region`, so a switch of region shows at once.
 */
const log = logger.createModuleLogger("prices");

/** Null when the call fails: the food page never breaks over its price. */
export async function getFoodPriceServer(
  foodId: string,
): Promise<FoodPriceQuote | null> {
  try {
    return await serverApi<FoodPriceQuote>(`/api/Foods/${foodId}/price`);
  } catch (error) {
    log.error("Failed to load the food price", {
      foodId,
      error: error instanceof Error ? error.message : String(error),
    });
    return null;
  }
}

/** Every region, the disabled ones too. */
export const listAdminPriceRegionsServer = () =>
  serverApi<PriceRegion[]>("/api/admin/prices/regions");

export const listPriceSourcesServer = () =>
  serverApi<PriceSourceAdmin[]>("/api/admin/prices/sources");

export const listPriceAliasesServer = (query: PriceAliasQuery) =>
  serverApi<PriceAliasPage>(`/api/admin/prices/aliases?${aliasQuery(query)}`);

export const listPriceReportsServer = (status: PriceReportStatus = "Open") =>
  serverApi<PriceReportPage>(
    `/api/admin/prices/reports?status=${encodeURIComponent(status)}&pageSize=50`,
  );
