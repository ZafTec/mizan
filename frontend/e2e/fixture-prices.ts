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
type PriceUnit = "g" | "kg" | "ml" | "l" | "piece";

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
export type StoredAlias = {
  id: string;
  sourceId: string;
  productName: string;
  productKey: string | null;
  foodId: string | null;
  quantity: number | null;
  unit: string | null;
  ignored: boolean;
  lastSeenPrice: number | null;
  lastSeenAt: string | null;
  lastSeenUrl: string | null;
  createdAt: string;
  updatedAt: string;
};
export type StoredReport = {
  id: string;
  userId: string;
  userName: string;
  foodId: string;
  regionCode: string;
  suggestedPrice: number;
  quantity: number;
  unit: string;
  note: string | null;
  shownPricePerKg: number | null;
  status: "Open" | "Accepted" | "Rejected";
  resolvedAt: string | null;
  createdAt: string;
};
/** What Run now answers: 202, 409 without a worker URL, 502 when the worker is down. */
export type RunOutcome = "started" | "unconfigured" | "unreachable";
export type PricesState = {
  /** The viewer's `priceRegionCode`. */
  regionCode: string;
  regions: PriceRegion[];
  sources: StoredSource[];
  prices: StoredPrice[];
  aliases: StoredAlias[];
  reports: StoredReport[];
  runOutcome: RunOutcome;
};
export type PricesOptions = {
  /** The viewer's region (default `ET-AA`). */
  regionCode?: string;
  /** What Run now answers this session (default `started`). */
  runOutcome?: RunOutcome;
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
export const OATS_ALIAS_ID = "f2000000-0000-4000-8000-000000000001";
export const BAG_ALIAS_ID = "f2000000-0000-4000-8000-000000000002";
export const OPEN_REPORT_ID = "f3000000-0000-4000-8000-000000000001";
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
    runOutcome: options.runOutcome ?? "started",
    aliases: [
      {
        id: OATS_ALIAS_ID,
        sourceId: ARADA_ID,
        productName: "Quaker oats 500g",
        productKey: "SKU-OATS-500",
        foodId: null,
        quantity: 500,
        unit: "g",
        ignored: false,
        lastSeenPrice: 145,
        lastSeenAt: stamp(1),
        lastSeenUrl: "https://aradamart.net/product-page/quaker-oats-500g",
        createdAt: stamp(5),
        updatedAt: stamp(1),
      },
      {
        id: BAG_ALIAS_ID,
        sourceId: ARADA_ID,
        productName: "Reusable shopping bag",
        productKey: "SKU-BAG",
        foodId: null,
        quantity: null,
        unit: null,
        ignored: false,
        lastSeenPrice: 40,
        lastSeenAt: stamp(1),
        lastSeenUrl: "https://aradamart.net/product-page/reusable-shopping-bag",
        createdAt: stamp(5),
        updatedAt: stamp(1),
      },
      {
        id: "f2000000-0000-4000-8000-000000000003",
        sourceId: ARADA_ID,
        productName: "Greek yogurt 500g",
        productKey: "SKU-YOG-500",
        foodId: YOGURT_ID,
        quantity: 500,
        unit: "g",
        ignored: false,
        lastSeenPrice: 180,
        lastSeenAt: stamp(1),
        lastSeenUrl: "https://aradamart.net/product-page/greek-yogurt-500g",
        createdAt: stamp(30),
        updatedAt: stamp(1),
      },
    ],
    reports: [
      {
        id: OPEN_REPORT_ID,
        userId: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
        userName: "Sam Rivera",
        foodId: YOGURT_ID,
        regionCode: "ET-AA",
        suggestedPrice: 200,
        quantity: 500,
        unit: "g",
        note: "Shola market price this week.",
        shownPricePerKg: 360,
        status: "Open",
        resolvedAt: null,
        createdAt: stamp(0),
      },
      {
        id: "f3000000-0000-4000-8000-000000000002",
        userId: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
        userName: "Sam Rivera",
        foodId: OATS_ID,
        regionCode: "ET-AA",
        suggestedPrice: 240,
        quantity: 1,
        unit: "kg",
        note: null,
        shownPricePerKg: 250,
        status: "Accepted",
        resolvedAt: stamp(10),
        createdAt: stamp(12),
      },
    ],
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
function grams(
  price: Pick<StoredPrice, "quantity" | "unit">,
  food: PricedFood,
): number | null {
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

type Json = (data: unknown, status?: number) => Response;
type HostFood = PricedFood & { name: string };
export type PricesHost = {
  user: { id: string; email: string; name: string; image: string | null };
  /** The role the API sees; admin routes answer 403 to anyone else. */
  role: string;
  prices: PricesState;
  foods: HostFood[];
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
  /** Calls the fixture refuses to count as handled. A write without an Idempotency-Key lands here. */
  unhandled: string[];
};

const UNITS = ["g", "kg", "ml", "l", "piece"];
const STAPLES = ["Teff flour", "Red lentils", "Rolled oats", "Greek yogurt"];

function parameter(url: URL, name: string) {
  return [...url.searchParams].find(
    ([key]) => key.toLowerCase() === name.toLowerCase(),
  )?.[1];
}

function paged<T>(items: T[], url: URL) {
  const page = Number(parameter(url, "page") ?? 1);
  const pageSize = Number(parameter(url, "pageSize") ?? 50);
  return {
    items: items.slice((page - 1) * pageSize, page * pageSize),
    totalCount: items.length,
    page,
    pageSize,
    totalPages: Math.ceil(items.length / pageSize),
  };
}

function quoteDto(
  prices: PricesState,
  food: PricedFood,
): Schemas["FoodPriceQuoteDto"] {
  const region = regionFor(prices);
  const quote = quoteFor(prices, food);
  if (!quote)
    return {
      foodId: food.id,
      regionCode: region.code,
      currency: region.currency,
      priced: false,
      pricePerGram: null,
      pricePerKg: null,
      price: null,
      quantity: null,
      unit: null,
      confidence: null,
      observedAt: null,
      sourceId: null,
      sourceName: null,
    };
  return {
    foodId: food.id,
    regionCode: region.code,
    currency: region.currency,
    priced: true,
    pricePerGram: quote.pricePerGram,
    pricePerKg: Math.round(quote.pricePerGram * 1000 * 100) / 100,
    price: quote.price.price,
    quantity: quote.price.quantity,
    unit: quote.price.unit,
    confidence: quote.confidence,
    observedAt: quote.price.observedAt,
    sourceId: quote.price.sourceId,
    sourceName: quote.sourceName,
  };
}

function perKg(
  price: number,
  quantity: number,
  unit: string,
  food: PricedFood,
): number | null {
  const each = grams({ quantity, unit }, food);
  return each ? Math.round((price / each) * 1000 * 100) / 100 : null;
}

function currencyOf(prices: PricesState, regionCode: string): string {
  return (
    prices.regions.find((region) => region.code === regionCode)?.currency ??
    "ETB"
  );
}

/** A source of the region by kind, created on first use like the API's Manual and User reports sources. */
function sourceFor(
  prices: PricesState,
  regionCode: string,
  kind: "Manual" | "Report",
  now: string,
): StoredSource {
  const existing = prices.sources.find(
    (source) => source.regionCode === regionCode && source.kind === kind,
  );
  if (existing) return existing;
  const source: StoredSource = {
    id: crypto.randomUUID(),
    name: kind === "Manual" ? "Manual" : "User reports",
    kind,
    url: null,
    scraperKey: null,
    regionCode,
    enabled: true,
    schedule: null,
    lastRunAt: null,
    lastStatus: null,
    lastError: null,
    lastObservations: 0,
    createdAt: now,
    updatedAt: now,
  };
  prices.sources.push(source);
  return source;
}

/**
 * The prices routes of the fixture API: the food price and its report, the
 * regions, the viewer's profile with `priceRegionCode`, and /api/admin/prices.
 * Answers null for a path it does not serve.
 */
export async function handlePrices(
  request: Request,
  url: URL,
  host: PricesHost,
  json: Json,
): Promise<Response | null> {
  const { prices, writes } = host;
  const path = url.pathname;
  const method = request.method;
  const now = new Date().toISOString();
  const body = async <T>() =>
    ((await request.text().then((text) => (text ? JSON.parse(text) : {}))) ??
      {}) as T;
  // Every price write carries a key, as the planning ones do (fixture-planning.ts).
  const record = (data: Record<string, unknown>) => {
    const key = request.headers.get("Idempotency-Key") ?? undefined;
    if (!key) host.unhandled.push(`${method} ${path} without Idempotency-Key`);
    writes.push({ path, body: data, key });
  };
  const invalid = (error: string) =>
    json({ errorCode: "domain_validation_failed", error }, 400);
  const foodById = (id: string | null | undefined) =>
    host.foods.find((food) => food.id === id);

  const foodPrice = path.match(/^\/api\/Foods\/([^/]+)\/price$/);
  if (foodPrice && method === "GET") {
    const food = foodById(foodPrice[1]);
    if (!food) return json({ errorCode: "not_found", error: "No food" }, 404);
    return json(quoteDto(prices, food));
  }
  const report = path.match(/^\/api\/Foods\/([^/]+)\/price-reports$/);
  if (report && method === "POST") {
    const food = foodById(report[1]);
    if (!food) return json({ errorCode: "not_found", error: "No food" }, 404);
    const input = await body<Schemas["CreatePriceReportRequest"]>();
    record(input);
    if (!(input.suggestedPrice > 0) || !(input.quantity > 0))
      return invalid("The price and the quantity must be above zero.");
    if (!UNITS.includes(input.unit))
      return invalid("The unit is g, kg, ml, l or piece.");
    const shown = quoteDto(prices, food);
    const stored: StoredReport = {
      id: crypto.randomUUID(),
      userId: host.user.id,
      userName: host.user.name,
      foodId: food.id,
      regionCode: shown.regionCode,
      suggestedPrice: input.suggestedPrice,
      quantity: input.quantity,
      unit: input.unit,
      note: input.note ?? null,
      shownPricePerKg: shown.pricePerKg ?? null,
      status: "Open",
      resolvedAt: null,
      createdAt: now,
    };
    prices.reports.unshift(stored);
    return json(
      { id: stored.id } satisfies Schemas["PriceReportCreatedResult"],
      201,
    );
  }
  if (path === "/api/Prices/regions" && method === "GET")
    return json(prices.regions.filter((region) => region.enabled));
  if (path === "/api/Users/me" && method === "GET")
    return json({
      id: host.user.id,
      email: host.user.email,
      name: host.user.name,
      image: host.user.image,
      createdAt: "2026-01-04T08:00:00Z",
      currentGoal: undefined,
      streakCount: 4,
      priceRegionCode: prices.regionCode,
    } satisfies Schemas["UserDto"]);
  if (path === "/api/Users/me" && method === "PUT") {
    const input = await body<Schemas["UpdateUserRequest"]>();
    record(input);
    if (input.priceRegionCode != null) {
      const region = prices.regions.find(
        (candidate) => candidate.code === input.priceRegionCode,
      );
      if (!region?.enabled)
        return invalid("That price region does not exist or is disabled.");
      prices.regionCode = region.code;
    }
    return json(null, 204);
  }

  if (!path.startsWith("/api/admin/prices/")) return null;
  if (host.role !== "admin")
    return json({ errorCode: "forbidden", error: "Administrators only." }, 403);

  if (path === "/api/admin/prices/regions" && method === "GET")
    return json(prices.regions);
  if (path === "/api/admin/prices/regions" && method === "POST") {
    const input = await body<Schemas["CreatePriceRegionRequest"]>();
    record(input);
    const region: PriceRegion = {
      code: input.code.trim().toUpperCase(),
      name: input.name.trim(),
      currency: input.currency.trim().toUpperCase(),
      enabled: input.enabled,
    };
    if (prices.regions.some((candidate) => candidate.code === region.code))
      return json(
        { errorCode: "conflict", error: "That region already exists." },
        409,
      );
    prices.regions.push(region);
    return json(region, 201);
  }
  const regionPath = path.match(/^\/api\/admin\/prices\/regions\/([^/]+)$/);
  if (regionPath && method === "PUT") {
    const input = await body<Schemas["UpdatePriceRegionRequest"]>();
    record(input);
    const region = prices.regions.find(
      (candidate) => candidate.code === decodeURIComponent(regionPath[1]),
    );
    if (!region)
      return json({ errorCode: "not_found", error: "No region" }, 404);
    Object.assign(region, {
      name: input.name,
      currency: input.currency.toUpperCase(),
      enabled: input.enabled,
    });
    return json(region);
  }

  if (path === "/api/admin/prices/sources" && method === "GET")
    return json(
      prices.sources.map((source): Schemas["PriceSourceAdminDto"] => ({
        ...source,
        currency: currencyOf(prices, source.regionCode),
        aliasCount: prices.aliases.filter(
          (alias) => alias.sourceId === source.id,
        ).length,
        unresolvedCount: prices.aliases.filter(
          (alias) =>
            alias.sourceId === source.id && !alias.foodId && !alias.ignored,
        ).length,
        priceCount: prices.prices.filter(
          (price) => price.sourceId === source.id,
        ).length,
      })),
    );
  const sourceInput = (input: Schemas["SavePriceSourceRequest"]) => ({
    name: input.name,
    kind: input.kind as StoredSource["kind"],
    regionCode: input.regionCode,
    url: input.url ?? null,
    scraperKey: input.scraperKey ?? null,
    schedule: input.schedule ?? null,
    enabled: input.enabled,
    updatedAt: now,
  });
  if (path === "/api/admin/prices/sources" && method === "POST") {
    const input = await body<Schemas["SavePriceSourceRequest"]>();
    record(input);
    if (!input.name?.trim()) return invalid("Name the source.");
    const source: StoredSource = {
      id: crypto.randomUUID(),
      lastRunAt: null,
      lastStatus: null,
      lastError: null,
      lastObservations: 0,
      createdAt: now,
      ...sourceInput(input),
    };
    prices.sources.push(source);
    return json({ id: source.id }, 201);
  }
  const run = path.match(/^\/api\/admin\/prices\/sources\/([^/]+)\/run$/);
  if (run && method === "POST") {
    record({});
    const source = prices.sources.find((item) => item.id === run[1]);
    if (!source)
      return json({ errorCode: "not_found", error: "No source" }, 404);
    if (source.kind !== "Scraper")
      return invalid(
        "Only a scraper source can run; manual and report sources are filled by people.",
      );
    if (prices.runOutcome === "unconfigured")
      return json(
        {
          errorCode: "conflict",
          error:
            "The prices worker is not configured. Set Prices:WorkerUrl on the API to use Run now.",
        },
        409,
      );
    if (prices.runOutcome === "unreachable")
      return json(
        {
          errorCode: "upstream_unavailable",
          error: "The prices worker did not answer. Check that it is running.",
        },
        502,
      );
    return json(
      {
        sourceId: source.id,
        status: "started",
      } satisfies Schemas["PriceSourceRunStartedDto"],
      202,
    );
  }
  const sourcePath = path.match(/^\/api\/admin\/prices\/sources\/([^/]+)$/);
  if (sourcePath && (method === "PUT" || method === "DELETE")) {
    const source = prices.sources.find((item) => item.id === sourcePath[1]);
    if (!source)
      return json({ errorCode: "not_found", error: "No source" }, 404);
    if (method === "DELETE") {
      record({});
      prices.sources = prices.sources.filter((item) => item !== source);
      return json(null, 204);
    }
    const input = await body<Schemas["SavePriceSourceRequest"]>();
    record(input);
    Object.assign(source, sourceInput(input));
    return json(null, 204);
  }

  if (path === "/api/admin/prices/aliases" && method === "GET") {
    const queue = parameter(url, "queue") === "true";
    const sourceId = parameter(url, "sourceId");
    const search = parameter(url, "search")?.toLowerCase();
    const items = prices.aliases
      .filter((alias) => !queue || (!alias.foodId && !alias.ignored))
      .filter((alias) => !sourceId || alias.sourceId === sourceId)
      .filter(
        (alias) => !search || alias.productName.toLowerCase().includes(search),
      )
      .map((alias): Schemas["PriceSourceAliasDto"] => {
        const source = prices.sources.find(
          (item) => item.id === alias.sourceId,
        );
        const regionCode = source?.regionCode ?? "ET-AA";
        return {
          ...alias,
          sourceName: source?.name ?? "Unknown",
          regionCode,
          currency: currencyOf(prices, regionCode),
          foodName: foodById(alias.foodId)?.name ?? null,
        };
      });
    return json(paged(items, url));
  }
  const aliasPath = path.match(/^\/api\/admin\/prices\/aliases\/([^/]+)$/);
  if (aliasPath && method === "PUT") {
    const input = await body<Schemas["UpdatePriceAliasRequest"]>();
    record(input);
    const alias = prices.aliases.find((item) => item.id === aliasPath[1]);
    if (!alias) return json({ errorCode: "not_found", error: "No alias" }, 404);
    if (input.ignored && input.foodId)
      return invalid("An ignored product has no food.");
    if ((input.quantity == null) !== (input.unit == null))
      return invalid("Give a quantity with its unit, or neither.");
    Object.assign(alias, {
      foodId: input.foodId ?? null,
      quantity: input.quantity ?? null,
      unit: input.unit ?? null,
      ignored: input.ignored,
      updatedAt: now,
    });
    return json(null, 204);
  }

  if (path === "/api/admin/prices/prices" && method === "POST") {
    const input = await body<Schemas["CreateManualPriceRequest"]>();
    record(input);
    const food = foodById(input.foodId);
    if (!food) return json({ errorCode: "not_found", error: "No food" }, 404);
    if (!UNITS.includes(input.unit))
      return invalid("The unit is g, kg, ml, l or piece.");
    const price: StoredPrice = {
      id: crypto.randomUUID(),
      foodId: food.id,
      regionCode: input.regionCode,
      sourceId: sourceFor(prices, input.regionCode, "Manual", now).id,
      price: input.price,
      quantity: input.quantity,
      unit: input.unit,
      confidence: input.confidence === "Observed" ? "Observed" : "Estimated",
      observedAt: input.observedAt ?? now,
    };
    prices.prices.push(price);
    return json({ id: price.id }, 201);
  }
  if (path === "/api/admin/prices/seed-staples" && method === "POST") {
    record({});
    const result: Schemas["SeedStaplePricesResult"] = {
      stored: 0,
      alreadyPriced: 0,
      missing: [],
    };
    for (const name of STAPLES) {
      const food = host.foods.find((item) => item.name === name);
      if (!food) {
        result.missing.push(name);
        continue;
      }
      const estimated = prices.prices.some(
        (price) =>
          price.foodId === food.id &&
          price.regionCode === "ET-AA" &&
          price.confidence === "Estimated",
      );
      if (estimated) {
        result.alreadyPriced += 1;
        continue;
      }
      prices.prices.push({
        id: crypto.randomUUID(),
        foodId: food.id,
        regionCode: "ET-AA",
        sourceId: sourceFor(prices, "ET-AA", "Manual", now).id,
        price: 400,
        quantity: 1,
        unit: "kg" satisfies PriceUnit,
        confidence: "Estimated",
        observedAt: now,
      });
      result.stored += 1;
    }
    return json(result);
  }

  if (path === "/api/admin/prices/reports" && method === "GET") {
    const status = parameter(url, "status") ?? "Open";
    const items = prices.reports
      .filter((item) => status === "all" || item.status === status)
      .map((item): Schemas["PriceReportDto"] => {
        const food = foodById(item.foodId);
        return {
          ...item,
          foodName: food?.name ?? null,
          currency: currencyOf(prices, item.regionCode),
          suggestedPricePerKg: food
            ? perKg(item.suggestedPrice, item.quantity, item.unit, food)
            : null,
          resolvedByUserId: item.resolvedAt ? host.user.id : null,
          resolutionNote: null,
        };
      });
    return json(paged(items, url));
  }
  const resolve = path.match(
    /^\/api\/admin\/prices\/reports\/([^/]+)\/(accept|reject)$/,
  );
  if (resolve && method === "POST") {
    record(await body<Record<string, unknown>>());
    const item = prices.reports.find(
      (candidate) => candidate.id === resolve[1],
    );
    if (!item) return json({ errorCode: "not_found", error: "No report" }, 404);
    if (item.status !== "Open")
      return invalid("This report is already resolved.");
    item.status = resolve[2] === "accept" ? "Accepted" : "Rejected";
    item.resolvedAt = now;
    if (item.status === "Accepted")
      prices.prices.push({
        id: crypto.randomUUID(),
        foodId: item.foodId,
        regionCode: item.regionCode,
        sourceId: sourceFor(prices, item.regionCode, "Report", now).id,
        price: item.suggestedPrice,
        quantity: item.quantity,
        unit: item.unit,
        confidence: "Reported",
        observedAt: now,
      });
    return json(null, 204);
  }
  return null;
}
