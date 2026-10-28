"use client";

import { useId, useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { reportFoodPrice } from "@/lib/api/prices";
import {
  parsePriceReport,
  PRICE_UNITS,
  type PriceReportForm,
} from "@/lib/prices/schema";
import { getErrorMessage } from "@/lib/toast";
import { Select } from "@/components/ui/select";

type Errors = Partial<Record<keyof PriceReportForm, string>>;

/**
 * Report a wrong price: what the person paid, for how much, and a note. The
 * API keeps the price that was shown beside it and tells the administrators.
 */
export default function ReportPriceDialog({
  foodId,
  foodName,
  currency,
  quantity,
  unit,
}: {
  foodId: string;
  foodName: string;
  currency: string;
  quantity?: number | null;
  unit?: string | null;
}) {
  const id = useId();
  const blank = (): PriceReportForm => ({
    price: "",
    quantity: quantity != null ? String(quantity) : "1",
    unit:
      unit && (PRICE_UNITS as readonly string[]).includes(unit) ? unit : "kg",
    note: "",
  });
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(blank);
  const [errors, setErrors] = useState<Errors>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);

  const change = (key: keyof PriceReportForm, value: string) =>
    setForm((current) => ({ ...current, [key]: value }));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const parsed = parsePriceReport(form);
    if (parsed.errors) {
      setErrors(parsed.errors);
      return;
    }
    setErrors({});
    setFailure(null);
    setBusy(true);
    try {
      await reportFoodPrice(foodId, {
        suggestedPrice: parsed.value.price,
        quantity: parsed.value.quantity,
        unit: parsed.value.unit,
        note: parsed.value.note,
      });
      setSent(true);
    } catch (error) {
      setFailure(getErrorMessage(error, "Could not send the report"));
    } finally {
      setBusy(false);
    }
  };

  const field = (key: keyof PriceReportForm) => ({
    id: `${id}-${key}`,
    "aria-invalid": errors[key] ? true : undefined,
    "aria-describedby": errors[key] ? `${id}-${key}-error` : undefined,
  });
  const error = (key: keyof PriceReportForm) =>
    errors[key] ? (
      <p id={`${id}-${key}-error`} className="mt-1 text-sm text-destructive">
        {errors[key]}
      </p>
    ) : null;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (next) {
          setForm(blank());
          setErrors({});
          setFailure(null);
          setSent(false);
        }
      }}
    >
      <DialogTrigger asChild>
        <button type="button" className="btn-secondary btn-sm min-h-11">
          Report a wrong price
        </button>
      </DialogTrigger>
      <DialogContent className="max-h-[90dvh] w-[calc(100%-2rem)] overflow-y-auto">
        <DialogTitle>Report a wrong price</DialogTitle>
        <DialogDescription>
          What {foodName} costs where you shop. An administrator compares it
          with the price shown.
        </DialogDescription>
        {sent ? (
          <div className="space-y-4">
            <p role="status">Thanks, an administrator will check it.</p>
            <button
              type="button"
              className="btn-primary min-h-11"
              onClick={() => setOpen(false)}
            >
              Close
            </button>
          </div>
        ) : (
          <form
            onSubmit={submit}
            noValidate
            aria-label="Price report"
            className="space-y-4"
          >
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
              <div className="col-span-2 sm:col-span-1">
                <label htmlFor={`${id}-price`} className="label">
                  Price ({currency})
                </label>
                <input
                  {...field("price")}
                  type="number"
                  inputMode="decimal"
                  min={0}
                  step="any"
                  className="input tabular-nums w-full"
                  value={form.price}
                  onChange={(event) => change("price", event.target.value)}
                />
                {error("price")}
              </div>
              <div>
                <label htmlFor={`${id}-quantity`} className="label">
                  Quantity
                </label>
                <input
                  {...field("quantity")}
                  type="number"
                  inputMode="decimal"
                  min={0}
                  step="any"
                  className="input tabular-nums w-full"
                  value={form.quantity}
                  onChange={(event) => change("quantity", event.target.value)}
                />
                {error("quantity")}
              </div>
              <div>
                <label htmlFor={`${id}-unit`} className="label">
                  Unit
                </label>
                <Select
                  {...field("unit")}
                  value={form.unit}
                  onChange={(next) => change("unit", next)}
                  options={PRICE_UNITS.map((value) => ({
                    value,
                    label: value,
                  }))}
                />
                {error("unit")}
              </div>
            </div>
            <div>
              <label htmlFor={`${id}-note`} className="label">
                Note (optional)
              </label>
              <textarea
                {...field("note")}
                rows={3}
                className="input w-full"
                value={form.note}
                onChange={(event) => change("note", event.target.value)}
                placeholder="Where you bought it"
              />
              {error("note")}
            </div>
            {failure && (
              <p role="alert" className="text-sm text-destructive">
                {failure}
              </p>
            )}
            <button
              type="submit"
              className="btn-primary min-h-11 w-full sm:w-auto"
              disabled={busy}
            >
              Send report
            </button>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
