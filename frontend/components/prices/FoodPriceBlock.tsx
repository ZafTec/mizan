import type { FoodPriceQuote } from "@/lib/api/prices";
import {
  confidenceLabel,
  formatMoney,
  formatPriceDate,
} from "@/lib/prices/format";
import ReportPriceDialog from "./ReportPriceDialog";

/**
 * The food's price in the viewer's region, exactly as the API quotes it:
 * the listed price and amount, the price per kilogram, how sure it is, the
 * source and the day it was seen. A person can report it as wrong.
 */
export default function FoodPriceBlock({
  quote,
  foodName,
}: {
  quote: FoodPriceQuote;
  foodName: string;
}) {
  const facts = quote.priced
    ? [
        { name: "Confidence", value: confidenceLabel(quote.confidence) },
        { name: "Source", value: quote.sourceName ?? "" },
        {
          name: "Seen",
          value: quote.observedAt ? formatPriceDate(quote.observedAt) : "",
        },
      ].filter((fact) => fact.value)
    : [];
  return (
    <section
      aria-labelledby="food-price-title"
      className="card p-6"
      data-testid="food-price"
    >
      <h2
        id="food-price-title"
        className="font-semibold text-card-foreground mb-4"
      >
        Price
      </h2>
      {quote.priced && quote.price != null ? (
        <>
          <p className="num text-3xl font-semibold tracking-tight text-foreground">
            {formatMoney(quote.price, quote.currency)}
            {quote.quantity != null && quote.unit && (
              <span className="text-sm font-normal text-muted-foreground">
                {" "}
                for {quote.quantity.toLocaleString("en-US")} {quote.unit}
              </span>
            )}
          </p>
          {quote.pricePerKg != null && (
            <p className="num mt-1 text-sm text-muted-foreground">
              {formatMoney(quote.pricePerKg, quote.currency)} per kg
            </p>
          )}
          {facts.length > 0 && (
            <dl className="mt-4 divide-y divide-border border-y border-border text-sm">
              {facts.map((fact) => (
                <div
                  key={fact.name}
                  className="flex justify-between gap-4 py-2"
                >
                  <dt className="text-muted-foreground">{fact.name}</dt>
                  <dd className="min-w-0 text-right break-words">
                    {fact.value}
                  </dd>
                </div>
              ))}
            </dl>
          )}
        </>
      ) : (
        <p>No price for your region yet.</p>
      )}
      <div className="mt-4">
        <ReportPriceDialog
          foodId={quote.foodId}
          foodName={foodName}
          currency={quote.currency}
          quantity={quote.quantity}
          unit={quote.unit}
        />
      </div>
    </section>
  );
}
