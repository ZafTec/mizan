"use client";

import { useId, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import FoodSearch from "@/components/planning/FoodSearch";
import {
  acceptPriceAliasSuggestions,
  updatePriceAlias,
  type AcceptPriceAliasSuggestionsResult,
  type PriceSourceAlias,
} from "@/lib/api/prices";
import { formatMoney, formatPriceDate } from "@/lib/prices/format";
import {
  parseAliasMapping,
  PRICE_UNITS,
  type AliasMappingForm,
} from "@/lib/prices/schema";
import type { FoodChoice } from "@/lib/planning/rules";
import { appToast, getErrorMessage } from "@/lib/toast";
import { Select } from "@/components/ui/select";
import { cn } from "@/lib/utils";

function MapForm({
  alias,
  onDone,
}: {
  alias: PriceSourceAlias;
  onDone: () => void;
}) {
  const router = useRouter();
  const id = useId();
  const [food, setFood] = useState<FoodChoice | null>(null);
  const [quantity, setQuantity] = useState(
    alias.quantity != null ? String(alias.quantity) : "",
  );
  const [unit, setUnit] = useState(alias.unit ?? "");
  const [errors, setErrors] = useState<
    Partial<Record<keyof AliasMappingForm, string>>
  >({});
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    const parsed = parseAliasMapping({
      foodId: food?.id ?? "",
      quantity,
      unit,
    });
    if (parsed.errors) {
      setErrors(parsed.errors);
      return;
    }
    setErrors({});
    setFailure(null);
    setBusy(true);
    try {
      await updatePriceAlias(alias.id, {
        foodId: parsed.value.foodId,
        quantity: parsed.value.quantity,
        unit: parsed.value.unit,
        ignored: false,
      });
      appToast.success(`${alias.productName} mapped to ${food?.name}`);
      onDone();
      router.refresh();
    } catch (error) {
      setFailure(getErrorMessage(error, "Could not map the product"));
      setBusy(false);
    }
  };

  return (
    <form
      onSubmit={save}
      aria-label={`Map ${alias.productName}`}
      className="mt-3 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto_auto_auto] sm:items-end"
    >
      <FoodSearch
        label="Food"
        value={food ? [food] : []}
        onChange={(next) => setFood(next.at(-1) ?? null)}
        error={errors.foodId}
      />
      <div className="grid grid-cols-2 gap-3 sm:contents">
        <div className="sm:w-24">
          <label htmlFor={`${id}-quantity`} className="label">
            Quantity
          </label>
          <input
            id={`${id}-quantity`}
            type="number"
            inputMode="decimal"
            min={0}
            step="any"
            className="input num w-full"
            value={quantity}
            onChange={(event) => setQuantity(event.target.value)}
            aria-invalid={errors.quantity ? true : undefined}
          />
        </div>
        <div className="sm:w-24">
          <label htmlFor={`${id}-unit`} className="label">
            Unit
          </label>
          <Select
            id={`${id}-unit`}
            value={unit}
            onChange={setUnit}
            options={[
              { value: "", label: "None" },
              ...PRICE_UNITS.map((value) => ({ value, label: value })),
            ]}
          />
        </div>
      </div>
      <div className="flex gap-2">
        <button
          type="submit"
          className="btn-primary btn-sm min-h-11"
          disabled={busy}
        >
          Save
        </button>
        <button
          type="button"
          className="btn-ghost btn-sm min-h-11"
          onClick={onDone}
        >
          Cancel
        </button>
      </div>
      {(errors.quantity || failure) && (
        <p role="alert" className="text-sm text-destructive sm:col-span-4">
          {errors.quantity ?? failure}
        </p>
      )}
    </form>
  );
}

const SKIP_REASONS: Record<string, string> = {
  not_found: "It is no longer in the queue.",
  already_reviewed: "Someone mapped or ignored it already.",
  no_suggestion: "It has no suggestion to accept.",
};

const FILTERS = [
  { key: "all", label: "All" },
  { key: "suggested", label: "Has a suggestion" },
  { key: "unmatched", label: "No match" },
] as const;
type Filter = (typeof FILTERS)[number]["key"];

const hasSuggestion = (alias: PriceSourceAlias) => !!alias.suggestedFoodId;

function suggestionText(alias: PriceSourceAlias): string {
  const size = [alias.suggestedQuantity, alias.suggestedUnit]
    .filter((part) => part != null && part !== "")
    .join(" ");
  return [alias.suggestedFoodName, size].filter(Boolean).join(" · ");
}

/**
 * Products a scraper saw that no food is mapped to yet (design board
 * PriceReview.dc.html). The API suggests a food and the package size from the
 * name; tick the ones that are right and accept them together, map one by
 * hand, or ignore it. Nothing is mapped until it is accepted.
 */
export default function PriceReviewQueue({
  aliases,
  total,
}: {
  aliases: PriceSourceAlias[];
  total: number;
}) {
  const router = useRouter();
  const id = useId();
  const [mapping, setMapping] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [filter, setFilter] = useState<Filter>("all");
  const [selected, setSelected] = useState<Set<string>>(() => new Set());
  const [accepting, setAccepting] = useState(false);
  const [report, setReport] =
    useState<AcceptPriceAliasSuggestionsResult | null>(null);
  // Names of the products sent, so the report can name the skipped ones after the refresh.
  const [sent, setSent] = useState<Map<string, string>>(() => new Map());

  const counts = useMemo(
    () => ({
      all: aliases.length,
      suggested: aliases.filter(hasSuggestion).length,
      unmatched: aliases.filter((alias) => !hasSuggestion(alias)).length,
    }),
    [aliases],
  );
  const shown = aliases.filter((alias) =>
    filter === "all"
      ? true
      : filter === "suggested"
        ? hasSuggestion(alias)
        : !hasSuggestion(alias),
  );
  // A product that left the queue on refresh is no longer selectable.
  const chosen = aliases.filter(
    (alias) => hasSuggestion(alias) && selected.has(alias.id),
  );

  const toggle = (aliasId: string, on: boolean) =>
    setSelected((current) => {
      const next = new Set(current);
      if (on) next.add(aliasId);
      else next.delete(aliasId);
      return next;
    });

  const accept = async () => {
    if (chosen.length === 0) return;
    setAccepting(true);
    setReport(null);
    setSent(new Map(chosen.map((alias) => [alias.id, alias.productName])));
    try {
      const result = await acceptPriceAliasSuggestions(
        chosen.map((alias) => alias.id),
      );
      setReport(result);
      setSelected(new Set());
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not accept the suggestions");
    } finally {
      setAccepting(false);
    }
  };

  const ignore = async (alias: PriceSourceAlias) => {
    setBusy(alias.id);
    try {
      await updatePriceAlias(alias.id, {
        foodId: null,
        quantity: null,
        unit: null,
        ignored: true,
      });
      appToast.success(`${alias.productName} ignored`);
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not ignore the product");
    } finally {
      setBusy(null);
    }
  };

  return (
    <section id="queue" aria-labelledby="queue-title" className="scroll-mt-6">
      <header className="log-section-header">
        <h2 id="queue-title">Review queue</h2>
        <span className="num text-sm log-muted">{total} to review</span>
      </header>

      {report && (
        <div
          role="status"
          className="mt-3 border-l-2 border-brand-700 pl-3 text-sm dark:border-brand-300"
          data-testid="accept-report"
        >
          <p>
            {report.accepted.length === 1
              ? "1 product mapped."
              : `${report.accepted.length} products mapped.`}
          </p>
          {report.skipped.length > 0 && (
            <>
              <p className="mt-1 log-muted">
                {report.skipped.length === 1
                  ? "1 was not:"
                  : `${report.skipped.length} were not:`}
              </p>
              <ul className="mt-1">
                {report.skipped.map((skip) => (
                  <li key={skip.aliasId}>
                    <span className="font-medium">
                      {sent.get(skip.aliasId) ?? "A product"}
                    </span>{" "}
                    <span className="log-muted">
                      {SKIP_REASONS[skip.reason] ?? skip.reason}
                    </span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      )}

      {aliases.length === 0 ? (
        <p className="log-empty">Every product is mapped.</p>
      ) : (
        <>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
            <div
              role="tablist"
              aria-label="Filter the queue"
              className="flex flex-wrap gap-1"
            >
              {FILTERS.map((option) => (
                <button
                  key={option.key}
                  type="button"
                  role="tab"
                  id={`${id}-${option.key}`}
                  aria-selected={filter === option.key}
                  aria-controls={`${id}-list`}
                  onClick={() => setFilter(option.key)}
                  className={cn(
                    "inline-flex min-h-10 items-center gap-1.5 rounded-[3px] border px-3 text-sm",
                    filter === option.key
                      ? "border-foreground font-medium"
                      : "border-border text-muted-foreground hover:bg-hair",
                  )}
                >
                  {option.label}
                  <span className="num text-xs log-muted">
                    {counts[option.key]}
                  </span>
                </button>
              ))}
            </div>
            <button
              type="button"
              className="btn-primary btn-sm min-h-11"
              disabled={chosen.length === 0 || accepting}
              onClick={accept}
            >
              {accepting
                ? "Accepting…"
                : chosen.length === 1
                  ? "Accept 1 suggestion"
                  : `Accept ${chosen.length} suggestions`}
            </button>
          </div>

          <ul
            id={`${id}-list`}
            role="tabpanel"
            aria-labelledby={`${id}-${filter}`}
            className="mt-2"
          >
            {shown.length === 0 && (
              <li className="log-empty">
                {filter === "suggested"
                  ? "No product here has a suggestion."
                  : "Every product here has a suggestion."}
              </li>
            )}
            {shown.map((alias) => (
              <li
                key={alias.id}
                className="border-b border-border py-3"
                aria-label={alias.productName}
              >
                <div className="flex flex-wrap items-center gap-3">
                  {hasSuggestion(alias) ? (
                    <input
                      type="checkbox"
                      className="h-5 w-5 shrink-0 accent-brand-700"
                      aria-label={`Select ${alias.productName}`}
                      checked={selected.has(alias.id)}
                      onChange={(event) =>
                        toggle(alias.id, event.target.checked)
                      }
                    />
                  ) : (
                    <span aria-hidden="true" className="w-5 shrink-0" />
                  )}
                  <span className="min-w-0 flex-1 basis-48">
                    <span className="block font-medium break-words">
                      {alias.productName}
                    </span>
                    <span className="num mt-0.5 block text-xs log-muted">
                      {[
                        alias.sourceName,
                        alias.lastSeenPrice != null
                          ? formatMoney(alias.lastSeenPrice, alias.currency)
                          : null,
                        alias.lastSeenAt
                          ? `seen ${formatPriceDate(alias.lastSeenAt)}`
                          : null,
                      ]
                        .filter(Boolean)
                        .join(" · ")}
                    </span>
                    <span className="mt-1 block text-sm">
                      {hasSuggestion(alias) ? (
                        <>
                          <span className="log-muted">Suggested: </span>
                          <span className="num">{suggestionText(alias)}</span>
                        </>
                      ) : (
                        <span className="log-muted">No match</span>
                      )}
                    </span>
                  </span>
                  <span className="flex shrink-0 flex-wrap items-center gap-1">
                    {alias.lastSeenUrl && (
                      <a
                        href={alias.lastSeenUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="text-link px-2 text-sm"
                        aria-label={`Listing for ${alias.productName}`}
                      >
                        Listing
                      </a>
                    )}
                    {mapping !== alias.id && (
                      <button
                        type="button"
                        className="btn-secondary btn-sm min-h-9"
                        aria-label={`Map ${alias.productName}`}
                        onClick={() => setMapping(alias.id)}
                      >
                        {hasSuggestion(alias) ? "Change" : "Map"}
                      </button>
                    )}
                    <button
                      type="button"
                      className="btn-ghost btn-sm min-h-9"
                      aria-label={`Ignore ${alias.productName}`}
                      disabled={busy !== null}
                      onClick={() => ignore(alias)}
                    >
                      Ignore
                    </button>
                  </span>
                </div>
                {mapping === alias.id && (
                  <MapForm alias={alias} onDone={() => setMapping(null)} />
                )}
              </li>
            ))}
          </ul>
          <p className="mt-3 text-xs log-muted">
            Suggestions match product names to catalogue foods and read the
            package size from the name. Nothing is mapped until you accept.
          </p>
        </>
      )}
    </section>
  );
}
