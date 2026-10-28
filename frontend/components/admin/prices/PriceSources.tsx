"use client";

import { useId, useState } from "react";
import { useRouter } from "next/navigation";
import { DeleteConfirmModal } from "@/components/DeleteConfirmModal";
import {
  createPriceSource,
  deletePriceSource,
  runPriceSource,
  SOURCE_KINDS,
  updatePriceSource,
  type PriceRegion,
  type PriceSourceAdmin,
  type SavePriceSourceRequest,
} from "@/lib/api/prices";
import { formatPriceDate } from "@/lib/prices/format";
import { appToast, getErrorMessage } from "@/lib/toast";
import { Select } from "@/components/ui/select";

type SourceForm = {
  name: string;
  kind: string;
  regionCode: string;
  url: string;
  scraperKey: string;
  schedule: string;
  enabled: boolean;
};
type RunOutcome = { ok: boolean; message: string };

const text = (value: string) => value.trim() || null;

function SourceFields({
  initial,
  regions,
  title,
  onSubmit,
  onCancel,
}: {
  initial: SourceForm;
  regions: PriceRegion[];
  title: string;
  onSubmit: (body: SavePriceSourceRequest) => Promise<void>;
  onCancel: () => void;
}) {
  const id = useId();
  const [form, setForm] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const set = <Key extends keyof SourceForm>(
    key: Key,
    value: SourceForm[Key],
  ) => setForm((current) => ({ ...current, [key]: value }));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form.name.trim()) {
      setError("Name the source.");
      return;
    }
    if (form.kind === "Scraper" && !form.scraperKey.trim()) {
      setError("A scraper source needs its scraper key.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await onSubmit({
        name: form.name.trim(),
        kind: form.kind,
        regionCode: form.regionCode,
        url: text(form.url),
        scraperKey: text(form.scraperKey),
        schedule: text(form.schedule),
        enabled: form.enabled,
      });
    } catch (caught) {
      setError(getErrorMessage(caught, "Could not save the source"));
      setBusy(false);
    }
  };

  return (
    <form
      onSubmit={submit}
      aria-label={title}
      className="grid gap-3 border-b border-border py-4 sm:grid-cols-2"
    >
      <div>
        <label htmlFor={`${id}-name`} className="label">
          Name
        </label>
        <input
          id={`${id}-name`}
          className="input w-full"
          value={form.name}
          onChange={(event) => set("name", event.target.value)}
        />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label htmlFor={`${id}-kind`} className="label">
            Kind
          </label>
          <Select
            id={`${id}-kind`}
            value={form.kind}
            onChange={(next) => set("kind", next)}
            options={SOURCE_KINDS.map((kind) => ({ value: kind, label: kind }))}
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
              label: region.code,
            }))}
          />
        </div>
      </div>
      <div>
        <label htmlFor={`${id}-url`} className="label">
          Website
        </label>
        <input
          id={`${id}-url`}
          type="url"
          className="input w-full"
          value={form.url}
          onChange={(event) => set("url", event.target.value)}
          placeholder="https://aradamart.net"
        />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label htmlFor={`${id}-key`} className="label">
            Scraper key
          </label>
          <input
            id={`${id}-key`}
            className="input w-full"
            value={form.scraperKey}
            onChange={(event) => set("scraperKey", event.target.value)}
            placeholder="aradamart"
          />
        </div>
        <div>
          <label htmlFor={`${id}-schedule`} className="label">
            Schedule (cron, UTC)
          </label>
          <input
            id={`${id}-schedule`}
            className="input num w-full"
            value={form.schedule}
            onChange={(event) => set("schedule", event.target.value)}
            placeholder="0 5 * * *"
          />
        </div>
      </div>
      <label className="flex min-h-11 items-center gap-2 text-sm">
        <input
          type="checkbox"
          className="h-5 w-5 accent-brand-700"
          checked={form.enabled}
          onChange={(event) => set("enabled", event.target.checked)}
        />
        Enabled
      </label>
      <div className="flex gap-2 sm:justify-end">
        <button
          type="submit"
          className="btn-primary btn-sm min-h-11"
          disabled={busy}
        >
          Save source
        </button>
        <button
          type="button"
          className="btn-ghost btn-sm min-h-11"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive sm:col-span-2">
          {error}
        </p>
      )}
    </form>
  );
}

function lastRun(source: PriceSourceAdmin): string {
  if (!source.lastRunAt) return "Never run";
  const parts = [`Last run ${formatPriceDate(source.lastRunAt)}`];
  if (source.lastStatus) parts.push(source.lastStatus);
  parts.push(
    `${source.lastObservations} ${source.lastObservations === 1 ? "price" : "prices"}`,
  );
  return parts.join(" · ");
}

/**
 * Where prices come from. A scraper source runs on its schedule in the
 * prices worker; Run now asks the worker to start one at once.
 */
export default function PriceSources({
  sources,
  regions,
}: {
  sources: PriceSourceAdmin[];
  regions: PriceRegion[];
}) {
  const router = useRouter();
  const [adding, setAdding] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<PriceSourceAdmin | null>(null);
  const [running, setRunning] = useState<string | null>(null);
  const [outcomes, setOutcomes] = useState<Record<string, RunOutcome>>({});

  const blank: SourceForm = {
    name: "",
    kind: "Scraper",
    regionCode: regions[0]?.code ?? "ET-AA",
    url: "",
    scraperKey: "",
    schedule: "",
    enabled: true,
  };
  const toForm = (source: PriceSourceAdmin): SourceForm => ({
    name: source.name,
    kind: source.kind,
    regionCode: source.regionCode,
    url: source.url ?? "",
    scraperKey: source.scraperKey ?? "",
    schedule: source.schedule ?? "",
    enabled: source.enabled,
  });

  const run = async (source: PriceSourceAdmin) => {
    setRunning(source.id);
    try {
      const result = await runPriceSource(source.id);
      setOutcomes((current) => ({
        ...current,
        [source.id]: {
          ok: true,
          message: `Run ${result.status}. The result shows here as the last run when the worker finishes.`,
        },
      }));
    } catch (error) {
      setOutcomes((current) => ({
        ...current,
        [source.id]: {
          ok: false,
          message: getErrorMessage(error, "Could not start the run"),
        },
      }));
    } finally {
      setRunning(null);
    }
  };

  const remove = async () => {
    if (!deleting) return;
    try {
      await deletePriceSource(deleting.id);
      appToast.success(`${deleting.name} deleted`);
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not delete the source");
    }
  };

  return (
    <section
      id="sources"
      aria-labelledby="sources-title"
      className="scroll-mt-6"
    >
      <header className="log-section-header">
        <h2 id="sources-title">Sources</h2>
        {!adding && (
          <button
            type="button"
            className="btn-secondary btn-sm min-h-9"
            onClick={() => setAdding(true)}
          >
            New source
          </button>
        )}
      </header>
      {adding && (
        <SourceFields
          title="New source"
          initial={blank}
          regions={regions}
          onCancel={() => setAdding(false)}
          onSubmit={async (body) => {
            await createPriceSource(body);
            appToast.success(`${body.name} added`);
            setAdding(false);
            router.refresh();
          }}
        />
      )}
      {sources.length === 0 ? (
        <p className="log-empty">No sources yet.</p>
      ) : (
        <ul>
          {sources.map((source) =>
            editingId === source.id ? (
              <li key={source.id}>
                <SourceFields
                  title={`Edit ${source.name}`}
                  initial={toForm(source)}
                  regions={regions}
                  onCancel={() => setEditingId(null)}
                  onSubmit={async (body) => {
                    await updatePriceSource(source.id, body);
                    appToast.success(`${body.name} saved`);
                    setEditingId(null);
                    router.refresh();
                  }}
                />
              </li>
            ) : (
              <li
                key={source.id}
                className="border-b border-border py-3"
                aria-label={`${source.name}, ${source.regionCode}`}
              >
                <div className="flex flex-wrap items-center gap-3">
                  <span className="min-w-0 flex-1 basis-48">
                    <span className="block font-medium break-words">
                      {source.name}
                    </span>
                    <span className="mt-0.5 block text-xs log-muted">
                      {[
                        source.kind,
                        source.regionCode,
                        source.schedule,
                        source.enabled ? null : "Disabled",
                      ]
                        .filter(Boolean)
                        .join(" · ")}
                    </span>
                    <span className="num mt-0.5 block text-xs log-muted">
                      {lastRun(source)}
                    </span>
                    {source.lastError && (
                      <span className="mt-1 block text-sm text-destructive break-words">
                        {source.lastError}
                      </span>
                    )}
                  </span>
                  <span className="flex shrink-0 flex-wrap gap-1">
                    {source.kind === "Scraper" && (
                      <button
                        type="button"
                        className="btn-secondary btn-sm min-h-9"
                        aria-label={`Run ${source.name} now`}
                        disabled={running !== null}
                        onClick={() => run(source)}
                      >
                        Run now
                      </button>
                    )}
                    <button
                      type="button"
                      className="btn-ghost btn-sm min-h-9"
                      aria-label={`Edit ${source.name}`}
                      onClick={() => setEditingId(source.id)}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className="btn-ghost btn-sm min-h-9 text-destructive"
                      aria-label={`Delete ${source.name}`}
                      onClick={() => setDeleting(source)}
                    >
                      Delete
                    </button>
                  </span>
                </div>
                {outcomes[source.id] && (
                  <p
                    role={outcomes[source.id].ok ? "status" : "alert"}
                    className={`mt-2 text-sm ${outcomes[source.id].ok ? "" : "text-destructive"}`}
                  >
                    {outcomes[source.id].message}
                  </p>
                )}
              </li>
            ),
          )}
        </ul>
      )}
      <DeleteConfirmModal
        isOpen={deleting !== null}
        onClose={() => setDeleting(null)}
        onConfirm={remove}
        itemName={deleting?.name}
      />
    </section>
  );
}
