import type { components } from "../types/api.generated";

/**
 * The prices half of the UI fixture: regions, sources, the alias review
 * queue, food prices and price reports, typed from the generated OpenAPI
 * schema. `quoteFor` is the fixture's PriceBook; the planning fixture costs
 * plans and lists with it, so the numbers on every page agree the way the
 * API's do. State is per session and every write lands in `writes[]`.
 */
type Schemas = components["schemas"];
type PriceRegion = Schemas["PriceRegionDto"];

export type StoredPrice = {
  id: string;
  foodId: string;
  regionCode: string;
  sourceId: string;
  price: number;
  quantity: number;
  unit: string;
  confidence: "Observed" | "Reported" | "Estimated";
  observedAt: string;
};
export type StoredSource = {
  id: string;
  name: string;
  kind: "Manual" | "Scraper" | "Report";
  url: string | null;
  scraperKey: string | null;
  regionCode: string;
  enabled: boolean;
  schedule: string | null;
  lastRunAt: string | null;
  lastStatus: string | null;
  lastError: string | null;
  lastObservations: number;
  createdAt: string;
  updatedAt: string;
};
export type PricesState = {
  /** The viewer's `priceRegionCode`. */
  regionCode: string;
  regions: PriceRegion[];
  sources: StoredSource[];
  prices: StoredPrice[];
};
export type PricesOptions = {
  /** The viewer's region (default `ET-AA`). */
  regionCode?: string;
};
export type PriceQuote = {
  pricePerGram: number;
  confidence: StoredPrice["confidence"];
  price: StoredPrice;
  sourceName: string;
};
type PricedFood = { id: string; servingSize?: number | null };

export const ARADA_ID = "f1000000-0000-4000-8000-000000000001";
export const ASBEZA_ID = "f1000000-0000-4000-8000-000000000002";
export const MANUAL_AA_ID = "f1000000-0000-4000-8000-000000000003";
export const REPORTS_AA_ID = "f1000000-0000-4000-8000-000000000004";
export const MANUAL_NBO_ID = "f1000000-0000-4000-8000-000000000005";
const YOGURT_ID = "11111111-1111-4111-8111-111111111111";
const OATS_ID = "22222222-2222-4222-8222-222222222222";

function daysAgo(today: string, days: number, time = "06:00:00"): string {
  const date = new Date(`${today}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() - days);
  return `${date.toISOString().slice(0, 10)}T${time}Z`;
}

export function initialPrices(
  today: string,
  options: PricesOptions = {},
): PricesState {
  const stamp = (days: number) => daysAgo(today, days);
  const source = (
    partial: Partial<StoredSource> & Pick<StoredSource, "id" | "name">,
  ): StoredSource => ({
    kind: "Manual",
    url: null,
    scraperKey: null,
    regionCode: "ET-AA",
    enabled: true,
    schedule: null,
    lastRunAt: null,
    lastStatus: null,
    lastError: null,
    lastObservations: 0,
    createdAt: stamp(40),
    updatedAt: stamp(40),
    ...partial,
  });
  return {
    regionCode: options.regionCode ?? "ET-AA",
    regions: [
      { code: "ET-AA", name: "Addis Ababa", currency: "ETB", enabled: true },
      { code: "ET-DD", name: "Dire Dawa", currency: "ETB", enabled: true },
      { code: "KE-NBO", name: "Nairobi", currency: "KES", enabled: true },
      { code: "ET-BD", name: "Bahir Dar", currency: "ETB", enabled: false },
    ],
    sources: [
      source({
        id: ARADA_ID,
        name: "Arada Mart",
        kind: "Scraper",
        url: "https://aradamart.net",
        scraperKey: "aradamart",
        schedule: "0 5 * * *",
        lastRunAt: stamp(1),
        lastStatus: "succeeded",
        lastObservations: 312,
      }),
      source({
        id: ASBEZA_ID,
        name: "Asbeza",
        kind: "Scraper",
        url: "https://asbeza.net",
        scraperKey: "asbeza",
        enabled: false,
        schedule: "0 6 * * 1",
        lastRunAt: stamp(8),
        lastStatus: "failed",
        lastError: "The store renders prices in the browser; no price found.",
      }),
      source({ id: MANUAL_AA_ID, name: "Manual" }),
      source({ id: REPORTS_AA_ID, name: "User reports", kind: "Report" }),
      source({ id: MANUAL_NBO_ID, name: "Manual", regionCode: "KE-NBO" }),
    ],
    prices: [
      {
        id: "p1000000-0000-4000-8000-000000000001",
        foodId: YOGURT_ID,
        regionCode: "ET-AA",
        sourceId: ARADA_ID,
        price: 180,
        quantity: 500,
        unit: "g",
        confidence: "Observed",
        observedAt: stamp(1),
      },
      {
        id: "p1000000-0000-4000-8000-000000000002",
        foodId: OATS_ID,
        regionCode: "ET-AA",
        sourceId: MANUAL_AA_ID,
        price: 250,
        quantity: 1,
        unit: "kg",
        confidence: "Estimated",
        observedAt: stamp(20),
      },
      {
        id: "p1000000-0000-4000-8000-000000000003",
        foodId: YOGURT_ID,
        regionCode: "KE-NBO",
        sourceId: MANUAL_NBO_ID,
        price: 220,
        quantity: 500,
        unit: "g",
        confidence: "Observed",
        observedAt: stamp(3),
      },
    ],
  };
}

const CONFIDENCE_RANK = { Estimated: 0, Reported: 1, Observed: 2 } as const;

/** The viewer's region, falling back to Addis Ababa when theirs is gone or disabled. */
export function regionFor(prices: PricesState): PriceRegion {
  return (
    prices.regions.find(
      (region) => region.code === prices.regionCode && region.enabled,
    ) ?? prices.regions.find((region) => region.code === "ET-AA")!
  );
}

/** Grams in one listed quantity, or null when the unit does not convert for the food. */
function grams(price: StoredPrice, food: PricedFood): number | null {
  const factor: Record<string, number | null | undefined> = {
    g: 1,
    kg: 1000,
    ml: 1,
    l: 1000,
    piece: food.servingSize,
  };
  const each = factor[price.unit];
  return each ? price.quantity * each : null;
}

/** The fixture's PriceBook: newest Observed, else Reported, else Estimated, as price per gram. */
export function quoteFor(
  prices: PricesState,
  food: PricedFood,
): PriceQuote | null {
  const region = regionFor(prices);
  const best = prices.prices
    .filter(
      (price) =>
        price.foodId === food.id &&
        price.regionCode === region.code &&
        grams(price, food) !== null,
    )
    .sort(
      (a, b) =>
        CONFIDENCE_RANK[b.confidence] - CONFIDENCE_RANK[a.confidence] ||
        b.observedAt.localeCompare(a.observedAt),
    )[0];
  if (!best) return null;
  return {
    pricePerGram: best.price / grams(best, food)!,
    confidence: best.confidence,
    price: best,
    sourceName:
      prices.sources.find((source) => source.id === best.sourceId)?.name ??
      "Unknown",
  };
}

/** The lower of two confidences; the total of a plan carries the weakest price it used. */
export function lowerConfidence(
  a: StoredPrice["confidence"] | null,
  b: StoredPrice["confidence"],
): StoredPrice["confidence"] {
  return a === null || CONFIDENCE_RANK[b] < CONFIDENCE_RANK[a] ? b : a;
}
