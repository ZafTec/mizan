/**
 * How prices read on every page. The API computes every amount; these only
 * turn its numbers into text, so the web, MCP and Telegram show the same
 * figures (docs/PLANNING.md#where-prices-show).
 */

/** "ETB 1,250.50": the currency code, then the amount with two decimals. */
export function formatMoney(amount: number, currency: string): string {
  const code = currency.trim().toUpperCase();
  const figure = Math.abs(amount).toLocaleString("en-US", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
  return `${amount < 0 ? "-" : ""}${code} ${figure}`;
}

export const CONFIDENCE_LABELS: Record<string, string> = {
  Observed: "Observed",
  Reported: "Reported",
  Estimated: "Estimated, a rough guess",
};

/** The confidence as a word; Estimated says that it is a rough guess. */
export function confidenceLabel(confidence: string | null | undefined): string {
  if (!confidence) return "";
  return CONFIDENCE_LABELS[confidence] ?? confidence;
}

/** The share of items the API priced, as a whole percent: 0.8 is "80%". */
export function formatCoverage(share: number): string {
  return `${Math.round(share * 100)}%`;
}

/** "1 item" or "3 items". */
export function itemCount(count: number): string {
  return `${count} ${count === 1 ? "item" : "items"}`;
}

/** "24 Oct 2026" in UTC, so the server and the browser print the same day. */
export function formatPriceDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  });
}
