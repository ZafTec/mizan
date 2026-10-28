"use client";

import { useId, useState } from "react";
import { useRouter } from "next/navigation";
import FoodSearch from "@/components/planning/FoodSearch";
import { updatePriceAlias, type PriceSourceAlias } from "@/lib/api/prices";
import { formatMoney, formatPriceDate } from "@/lib/prices/format";
import {
  parseAliasMapping,
  PRICE_UNITS,
  type AliasMappingForm,
} from "@/lib/prices/schema";
import type { FoodChoice } from "@/lib/planning/rules";
import { appToast, getErrorMessage } from "@/lib/toast";
import { Select } from "@/components/ui/select";

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

/**
 * Products a scraper saw that no food is mapped to yet. Map one to a food,
 * with the listed amount when the name does not carry it, or ignore it.
 */
export default function PriceReviewQueue({
  aliases,
  total,
}: {
  aliases: PriceSourceAlias[];
  total: number;
}) {
  const router = useRouter();
  const [mapping, setMapping] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

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
      {aliases.length === 0 ? (
        <p className="log-empty">Every product is mapped.</p>
      ) : (
        <ul>
          {aliases.map((alias) => (
            <li
              key={alias.id}
              className="border-b border-border py-3"
              aria-label={alias.productName}
            >
              <div className="flex flex-wrap items-center gap-3">
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
                      Map
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
      )}
    </section>
  );
}
