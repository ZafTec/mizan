"use client";

import { useId, useState } from "react";
import { useRouter } from "next/navigation";
import {
  createPriceRegion,
  updatePriceRegion,
  type PriceRegion,
} from "@/lib/api/prices";
import { appToast, getErrorMessage } from "@/lib/toast";

/** Regions: each has a currency; a disabled one is offered to nobody. */
export default function PriceRegions({ regions }: { regions: PriceRegion[] }) {
  const router = useRouter();
  const id = useId();
  const [adding, setAdding] = useState(false);
  const [form, setForm] = useState({ code: "", name: "", currency: "" });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const toggle = async (region: PriceRegion) => {
    setBusy(region.code);
    try {
      await updatePriceRegion(region.code, {
        name: region.name,
        currency: region.currency,
        enabled: !region.enabled,
      });
      appToast.success(
        `${region.name} ${region.enabled ? "disabled" : "enabled"}`,
      );
      router.refresh();
    } catch (caught) {
      appToast.error(caught, "Could not change the region");
    } finally {
      setBusy(null);
    }
  };

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form.code.trim() || !form.name.trim()) {
      setError("Give the region a code and a name.");
      return;
    }
    if (!/^[A-Za-z]{3}$/.test(form.currency.trim())) {
      setError("The currency is a three-letter code like ETB.");
      return;
    }
    setBusy("new");
    setError(null);
    try {
      await createPriceRegion({
        code: form.code.trim(),
        name: form.name.trim(),
        currency: form.currency.trim(),
        enabled: true,
      });
      appToast.success(`${form.name.trim()} added`);
      setAdding(false);
      setForm({ code: "", name: "", currency: "" });
      router.refresh();
    } catch (caught) {
      setError(getErrorMessage(caught, "Could not add the region"));
    } finally {
      setBusy(null);
    }
  };

  return (
    <section
      id="regions"
      aria-labelledby="regions-title"
      className="scroll-mt-6"
    >
      <header className="log-section-header">
        <h2 id="regions-title">Regions</h2>
        {!adding && (
          <button
            type="button"
            className="btn-secondary btn-sm min-h-9"
            onClick={() => setAdding(true)}
          >
            New region
          </button>
        )}
      </header>
      {adding && (
        <form
          onSubmit={create}
          aria-label="New region"
          className="flex flex-wrap items-end gap-3 border-b border-border py-4"
        >
          <div className="w-28">
            <label htmlFor={`${id}-code`} className="label">
              Code
            </label>
            <input
              id={`${id}-code`}
              className="input w-full uppercase"
              value={form.code}
              onChange={(event) =>
                setForm({ ...form, code: event.target.value })
              }
              placeholder="ET-AA"
            />
          </div>
          <div className="min-w-0 flex-1 basis-40">
            <label htmlFor={`${id}-name`} className="label">
              Name
            </label>
            <input
              id={`${id}-name`}
              className="input w-full"
              value={form.name}
              onChange={(event) =>
                setForm({ ...form, name: event.target.value })
              }
              placeholder="Addis Ababa"
            />
          </div>
          <div className="w-24">
            <label htmlFor={`${id}-currency`} className="label">
              Currency
            </label>
            <input
              id={`${id}-currency`}
              className="input w-full uppercase"
              value={form.currency}
              maxLength={3}
              onChange={(event) =>
                setForm({ ...form, currency: event.target.value })
              }
              placeholder="ETB"
            />
          </div>
          <div className="flex gap-2">
            <button
              type="submit"
              className="btn-primary btn-sm min-h-11"
              disabled={busy !== null}
            >
              Save region
            </button>
            <button
              type="button"
              className="btn-ghost btn-sm min-h-11"
              onClick={() => {
                setAdding(false);
                setError(null);
              }}
            >
              Cancel
            </button>
          </div>
          {error && (
            <p role="alert" className="basis-full text-sm text-destructive">
              {error}
            </p>
          )}
        </form>
      )}
      {regions.length === 0 ? (
        <p className="log-empty">No regions yet.</p>
      ) : (
        <ul>
          {regions.map((region) => (
            <li key={region.code} className="ledger-row">
              <span className="min-w-0 flex-1">
                <span className="block font-medium break-words">
                  {region.name}
                </span>
                <span className="mt-0.5 block text-xs log-muted">
                  {region.code} · {region.currency}
                  {region.enabled ? "" : " · Disabled"}
                </span>
              </span>
              <button
                type="button"
                className="btn-ghost btn-sm min-h-9"
                aria-label={`${region.enabled ? "Disable" : "Enable"} ${region.name}`}
                disabled={busy !== null}
                onClick={() => toggle(region)}
              >
                {region.enabled ? "Disable" : "Enable"}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
