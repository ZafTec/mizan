"use client";

import { useId, useState } from "react";
import { useRouter } from "next/navigation";
import FoodSearch from "@/components/planning/FoodSearch";
import {
  createManualPrice,
  importStarterFoods,
  seedStaplePrices,
  type ImportStarterFoodsResult,
  type PriceRegion,
  type SeedStaplePricesResult,
} from "@/lib/api/prices";
import { formatMoney } from "@/lib/prices/format";
import {
  MANUAL_CONFIDENCES,
  parseManualPrice,
  PRICE_UNITS,
  type ManualPriceForm,
} from "@/lib/prices/schema";
import type { FoodChoice } from "@/lib/planning/rules";
import { getErrorMessage } from "@/lib/toast";
import { Select } from "@/components/ui/select";

type Errors = Partial<Record<keyof ManualPriceForm, string>>;

/** Adds the starter foods to the shared catalogue, so plans and the staple prices have foods to use. */
function ImportStarterFoods() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<ImportStarterFoodsResult | null>(null);
  const [failure, setFailure] = useState<string | null>(null);

  const run = async () => {
    setBusy(true);
    setFailure(null);
    try {
      setResult(await importStarterFoods());
      router.refresh();
    } catch (error) {
      setFailure(getErrorMessage(error, "Could not import the starter foods"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-2 border-b border-border py-4">
      <button
        type="button"
        className="btn-secondary btn-sm min-h-11"
        disabled={busy}
        onClick={run}
      >
        {busy ? "Importing…" : "Import starter foods"}
      </button>
      <p className="text-xs log-muted">
        Adds generic foods and estimated Ethiopian staples to the shared
        catalogue. Foods already there are left as they are, so it is safe to
        repeat.
      </p>
      {result && (
        <div role="status" className="text-sm">
          <p className="num">
            {result.created} added, {result.existing} already in the catalogue.
          </p>
          {result.createdNames.length > 0 && (
            <p className="mt-1 break-words">
              <span className="log-muted">Added: </span>
              {result.createdNames.join(", ")}
            </p>
          )}
        </div>
      )}
      {failure && (
        <p role="alert" className="text-sm text-destructive">
          {failure}
        </p>
      )}
    </div>
  );
}

function SeedStaples() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<SeedStaplePricesResult | null>(null);
  const [failure, setFailure] = useState<string | null>(null);

  const seed = async () => {
    setBusy(true);
    setFailure(null);
    try {
      setResult(await seedStaplePrices());
      router.refresh();
    } catch (error) {
      setFailure(getErrorMessage(error, "Could not seed the staples"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-2 border-b border-border py-4">
      <button
        type="button"
        className="btn-secondary btn-sm min-h-11"
        disabled={busy}
        onClick={seed}
      >
        Seed Addis Ababa staples
      </button>
      <p className="text-xs log-muted">
        Writes the staple prices as Estimated for the catalogue foods whose
        names match. Foods with an estimate keep it.
      </p>
      {result && (
        <div role="status" className="text-sm">
          <p>
            {result.stored} stored, {result.alreadyPriced} already priced.
          </p>
          {result.missing.length > 0 && (
            <p className="mt-1 break-words">
              <span className="log-muted">No food matched: </span>
              {result.missing.join(", ")}
            </p>
          )}
        </div>
      )}
      {failure && (
        <p role="alert" className="text-sm text-destructive">
          {failure}
        </p>
      )}
    </div>
  );
}

/** A price an administrator enters by hand, as a rough estimate or as seen in a shop. */
export default function ManualPrices({ regions }: { regions: PriceRegion[] }) {
  const router = useRouter();
  const id = useId();
  const blank = (): ManualPriceForm => ({
    foodId: "",
    regionCode: regions.find((region) => region.enabled)?.code ?? "",
    price: "",
    quantity: "1",
    unit: "kg",
    confidence: "Estimated",
  });
  const [form, setForm] = useState(blank);
  const [food, setFood] = useState<FoodChoice | null>(null);
  const [errors, setErrors] = useState<Errors>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const set = (key: keyof ManualPriceForm, value: string) =>
    setForm((current) => ({ ...current, [key]: value }));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const parsed = parseManualPrice({ ...form, foodId: food?.id ?? "" });
    if (parsed.errors) {
      setErrors(parsed.errors);
      return;
    }
    setErrors({});
    setFailure(null);
    setBusy(true);
    try {
      await createManualPrice(parsed.value);
      const currency =
        regions.find((region) => region.code === parsed.value.regionCode)
          ?.currency ?? "";
      setSaved(
        `Saved ${food?.name}: ${formatMoney(parsed.value.price, currency)} for ${parsed.value.quantity} ${parsed.value.unit}, ${parsed.value.confidence}.`,
      );
      setForm(blank());
      setFood(null);
      router.refresh();
    } catch (error) {
      setFailure(getErrorMessage(error, "Could not save the price"));
    } finally {
      setBusy(false);
    }
  };

  const message = (key: keyof ManualPriceForm) =>
    errors[key] ? (
      <p className="mt-1 text-sm text-destructive">{errors[key]}</p>
    ) : null;

  return (
    <section id="manual" aria-labelledby="manual-title" className="scroll-mt-6">
      <header className="log-section-header">
        <h2 id="manual-title">Manual prices</h2>
      </header>
      <ImportStarterFoods />
      <SeedStaples />
      <form
        onSubmit={submit}
        noValidate
        aria-label="Manual price"
        className="grid gap-3 py-4 sm:grid-cols-2"
      >
        <div className="sm:col-span-2">
          <FoodSearch
            label="Food"
            value={food ? [food] : []}
            onChange={(next) => setFood(next.at(-1) ?? null)}
            error={errors.foodId}
          />
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label htmlFor={`${id}-price`} className="label">
              Price
            </label>
            <input
              id={`${id}-price`}
              type="number"
              inputMode="decimal"
              min={0}
              step="any"
              className="input num w-full"
              value={form.price}
              onChange={(event) => set("price", event.target.value)}
              aria-invalid={errors.price ? true : undefined}
            />
          </div>
          <div>
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
              value={form.quantity}
              onChange={(event) => set("quantity", event.target.value)}
              aria-invalid={errors.quantity ? true : undefined}
            />
          </div>
          <div>
            <label htmlFor={`${id}-unit`} className="label">
              Unit
            </label>
            <Select
              id={`${id}-unit`}
              value={form.unit}
              onChange={(next) => set("unit", next)}
              options={PRICE_UNITS.map((value) => ({ value, label: value }))}
            />
          </div>
          <div className="col-span-3">
            {message("price")}
            {message("quantity")}
            {message("unit")}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label htmlFor={`${id}-confidence`} className="label">
              Confidence
            </label>
            <Select
              id={`${id}-confidence`}
              value={form.confidence}
              onChange={(next) => set("confidence", next)}
              options={MANUAL_CONFIDENCES.map((value) => ({
                value,
                label: value,
              }))}
            />
          </div>
          <div>
            <label htmlFor={`${id}-region`} className="label">
              Region
            </label>
            <Select
              id={`${id}-region`}
              searchable
              value={form.regionCode}
              onChange={(next) => set("regionCode", next)}
              options={regions.map((region) => ({
                value: region.code,
                label: region.name,
                hint: region.currency,
              }))}
            />
            {message("regionCode")}
          </div>
        </div>
        <div className="sm:col-span-2">
          <button
            type="submit"
            className="btn-primary min-h-11 w-full sm:w-auto"
            disabled={busy}
          >
            Save price
          </button>
        </div>
        {failure && (
          <p role="alert" className="text-sm text-destructive sm:col-span-2">
            {failure}
          </p>
        )}
        {saved && (
          <p role="status" className="text-sm sm:col-span-2">
            {saved}
          </p>
        )}
      </form>
    </section>
  );
}
