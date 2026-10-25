import { clientApi } from "@/lib/api.client";
import type { components, paths } from "@/types/api.generated";

/**
 * Browser calls for prices: the food price and its report, the viewer's
 * region, and the administrator's regions, sources, review queue, manual
 * prices and reports. Every type comes from the generated OpenAPI document.
 * Every write carries an Idempotency-Key, like mealPlans.ts
 * (docs/PLANNING.md#api-step-3-as-built). Nothing here does price arithmetic.
 */
type Schemas = components["schemas"];

export type FoodPriceQuote = Schemas["FoodPriceQuoteDto"];
export type PriceRegion = Schemas["PriceRegionDto"];
export type PriceSourceAdmin = Schemas["PriceSourceAdminDto"];
export type PriceSourceAlias = Schemas["PriceSourceAliasDto"];
export type PriceAliasPage = Schemas["PriceSourceAliasDtoPagedResult"];
export type PriceReport = Schemas["PriceReportDto"];
export type PriceReportPage = Schemas["PriceReportDtoPagedResult"];
export type PriceSourceRunStarted = Schemas["PriceSourceRunStartedDto"];
export type SeedStaplePricesResult = Schemas["SeedStaplePricesResult"];
export type CreatePriceReportRequest = Schemas["CreatePriceReportRequest"];
export type CreateManualPriceRequest = Schemas["CreateManualPriceRequest"];
export type CreatePriceRegionRequest = Schemas["CreatePriceRegionRequest"];
export type UpdatePriceRegionRequest = Schemas["UpdatePriceRegionRequest"];
export type SavePriceSourceRequest = Schemas["SavePriceSourceRequest"];
export type UpdatePriceAliasRequest = Schemas["UpdatePriceAliasRequest"];
export type PriceAliasQuery = NonNullable<
  paths["/api/admin/prices/aliases"]["get"]["parameters"]["query"]
>;
export type PriceReportStatus = "Open" | "Accepted" | "Rejected" | "all";

export const SOURCE_KINDS = ["Manual", "Scraper", "Report"] as const;
export const REPORT_STATUSES = ["Open", "Accepted", "Rejected"] as const;

function idempotent(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

export function aliasQuery(query: PriceAliasQuery): string {
  const params = new URLSearchParams();
  if (query.queue) params.set("queue", "true");
  if (query.sourceId) params.set("sourceId", query.sourceId);
  if (query.search) params.set("search", query.search);
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  return params.toString();
}

export const getFoodPrice = (foodId: string) =>
  clientApi<FoodPriceQuote>(`/api/Foods/${foodId}/price`);

/** Records the price the person was shown and notifies every administrator. */
export const reportFoodPrice = (
  foodId: string,
  body: CreatePriceReportRequest,
) =>
  clientApi<Schemas["PriceReportCreatedResult"]>(
    `/api/Foods/${foodId}/price-reports`,
    { method: "POST", body, headers: idempotent() },
  );

/** The enabled regions a person can choose. */
export const listPriceRegions = () =>
  clientApi<PriceRegion[]>("/api/Prices/regions");

/** The viewer's profile; `priceRegionCode` is their region. */
export { getMyProfile, type UserProfile } from "@/lib/api/users";

/** The API refuses a region that is missing or disabled. */
export const setPriceRegion = (priceRegionCode: string) =>
  clientApi<void>("/api/Users/me", {
    method: "PUT",
    body: { priceRegionCode } satisfies Schemas["UpdateUserRequest"],
    headers: idempotent(),
  });

// ---- Administrators ------------------------------------------------------

export const createPriceRegion = (body: CreatePriceRegionRequest) =>
  clientApi<PriceRegion>("/api/admin/prices/regions", {
    method: "POST",
    body,
    headers: idempotent(),
  });

export const updatePriceRegion = (
  code: string,
  body: UpdatePriceRegionRequest,
) =>
  clientApi<PriceRegion>(
    `/api/admin/prices/regions/${encodeURIComponent(code)}`,
    { method: "PUT", body, headers: idempotent() },
  );

export const createPriceSource = (body: SavePriceSourceRequest) =>
  clientApi<Schemas["PriceSourceCreatedResult"]>("/api/admin/prices/sources", {
    method: "POST",
    body,
    headers: idempotent(),
  });

export const updatePriceSource = (id: string, body: SavePriceSourceRequest) =>
  clientApi<void>(`/api/admin/prices/sources/${id}`, {
    method: "PUT",
    body,
    headers: idempotent(),
  });

export const deletePriceSource = (id: string) =>
  clientApi<void>(`/api/admin/prices/sources/${id}`, {
    method: "DELETE",
    headers: idempotent(),
  });

/**
 * Run now. 202 when the worker took the run; 409 when the API has no worker
 * URL and 502 when the worker does not answer, each with the API's message.
 */
export const runPriceSource = (id: string) =>
  clientApi<PriceSourceRunStarted>(`/api/admin/prices/sources/${id}/run`, {
    method: "POST",
    headers: idempotent(),
    expectedStatuses: [400, 409, 502],
  });

export const listPriceAliases = (query: PriceAliasQuery) =>
  clientApi<PriceAliasPage>(`/api/admin/prices/aliases?${aliasQuery(query)}`);

export const updatePriceAlias = (id: string, body: UpdatePriceAliasRequest) =>
  clientApi<void>(`/api/admin/prices/aliases/${id}`, {
    method: "PUT",
    body,
    headers: idempotent(),
  });

export const createManualPrice = (body: CreateManualPriceRequest) =>
  clientApi<Schemas["FoodPriceCreatedResult"]>("/api/admin/prices/prices", {
    method: "POST",
    body,
    headers: idempotent(),
  });

/** Writes the Addis Ababa staples as Estimated prices; answers the names it could not match. */
export const seedStaplePrices = () =>
  clientApi<SeedStaplePricesResult>("/api/admin/prices/seed-staples", {
    method: "POST",
    headers: idempotent(),
  });

export const acceptPriceReport = (id: string, note?: string) =>
  clientApi<void>(`/api/admin/prices/reports/${id}/accept`, {
    method: "POST",
    body: { note: note ?? null } satisfies Schemas["ResolvePriceReportRequest"],
    headers: idempotent(),
  });

export const rejectPriceReport = (id: string, note?: string) =>
  clientApi<void>(`/api/admin/prices/reports/${id}/reject`, {
    method: "POST",
    body: { note: note ?? null } satisfies Schemas["ResolvePriceReportRequest"],
    headers: idempotent(),
  });
