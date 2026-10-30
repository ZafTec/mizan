"use client";

import { useCallback, useEffect, useId, useState } from "react";
import { useRouter } from "next/navigation";
import {
  clearFeatureFlagOverride,
  listFeatureFlagOverrides,
  searchHouseholds,
  setFeatureFlagDefault,
  setFeatureFlagOverride,
  type AdminHouseholdPage,
  type FeatureFlag,
  type FeatureFlagOverride,
} from "@/lib/api/admin-platform";
import { appToast, getErrorMessage } from "@/lib/toast";
import { ConfirmButton } from "@/components/ConfirmDialog";
import Switch from "./Switch";

/**
 * Every catalog flag with its default and the households that answer
 * differently (docs/ADMIN.md#feature-flags). Each write goes to the API and
 * the page is refreshed from it; nothing here decides what a household gets.
 */
export default function FeatureFlags({ flags }: { flags: FeatureFlag[] }) {
  return (
    <ul aria-label="Feature flags">
      {flags.map((flag) => (
        <FlagRow key={flag.key} flag={flag} />
      ))}
    </ul>
  );
}

function FlagRow({ flag }: { flag: FeatureFlag }) {
  const router = useRouter();
  const id = useId();
  const [busy, setBusy] = useState(false);
  const [open, setOpen] = useState(false);

  const toggleDefault = async (enabled: boolean) => {
    setBusy(true);
    try {
      await setFeatureFlagDefault(flag.key, enabled);
      appToast.success(`${flag.key} is ${enabled ? "on" : "off"} by default`);
      router.refresh();
    } catch (caught) {
      appToast.error(caught, "Could not change the default");
    } finally {
      setBusy(false);
    }
  };

  const differs =
    flag.overrideCount === 0
      ? "No household overrides"
      : `${flag.overrideCount === 1 ? "1 household overrides" : `${flag.overrideCount} households override`} it (${flag.enabledOverrides} on, ${flag.disabledOverrides} off)`;

  return (
    <li
      className="border-b border-border py-4"
      data-testid={`flag-${flag.key}`}
    >
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <div className="min-w-0 flex-1 basis-64">
          <p className="font-mono text-sm font-semibold break-words">
            {flag.key}
          </p>
          <p className="mt-0.5 text-sm text-muted-foreground">{flag.description}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {differs}
            {flag.defaultEnabled !== flag.catalogDefault &&
              ` · ships ${flag.catalogDefault ? "on" : "off"}`}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-xs text-muted-foreground">Default</span>
          <Switch
            checked={flag.defaultEnabled}
            label={`Default for ${flag.key}`}
            disabled={busy}
            onChange={toggleDefault}
          />
        </div>
        <button
          type="button"
          className="btn-ghost btn-sm min-h-11"
          aria-expanded={open}
          aria-controls={`${id}-overrides`}
          onClick={() => setOpen((value) => !value)}
        >
          {open ? "Hide overrides" : `Overrides (${flag.overrideCount})`}
        </button>
      </div>
      {open && (
        <div id={`${id}-overrides`} className="mt-3">
          <Overrides flag={flag} />
        </div>
      )}
    </li>
  );
}

function Overrides({ flag }: { flag: FeatureFlag }) {
  const router = useRouter();
  const id = useId();
  const [overrides, setOverrides] = useState<FeatureFlagOverride[] | null>(
    null,
  );
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [term, setTerm] = useState("");
  const [results, setResults] = useState<AdminHouseholdPage | null>(null);
  const [searchError, setSearchError] = useState<string | null>(null);

  const load = useCallback(
    () =>
      listFeatureFlagOverrides(flag.key).then(
        (list) => {
          setOverrides(list);
          setLoadError(null);
        },
        (caught: unknown) =>
          setLoadError(
            getErrorMessage(caught, "Could not load the overrides."),
          ),
      ),
    [flag.key],
  );

  useEffect(() => {
    void load();
  }, [load]);

  const afterWrite = async (message: string) => {
    appToast.success(message);
    await load();
    router.refresh();
  };

  const set = async (householdId: string, name: string, enabled: boolean) => {
    setBusy(householdId);
    try {
      await setFeatureFlagOverride(flag.key, householdId, enabled);
      await afterWrite(`${flag.key} is ${enabled ? "on" : "off"} for ${name}`);
      setResults(null);
      setTerm("");
    } catch (caught) {
      appToast.error(caught, "Could not save the override");
    } finally {
      setBusy(null);
    }
  };

  const clear = async (override: FeatureFlagOverride) => {
    setBusy(override.householdId);
    try {
      await clearFeatureFlagOverride(flag.key, override.householdId);
      await afterWrite(`${override.householdName} follows the default again`);
    } finally {
      setBusy(null);
    }
  };

  const search = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!term.trim()) {
      setSearchError("Type part of a household's name.");
      return;
    }
    setSearchError(null);
    try {
      setResults(await searchHouseholds(term.trim()));
    } catch (caught) {
      setSearchError(getErrorMessage(caught, "Could not search households."));
    }
  };

  const opposite = !flag.defaultEnabled;

  return (
    <div className="space-y-4 border-l-2 border-border pl-4">
      {loadError && (
        <p role="alert" className="text-sm text-destructive">
          {loadError}
        </p>
      )}
      {overrides === null && !loadError ? (
        <p className="text-sm text-muted-foreground">Loading overrides…</p>
      ) : overrides && overrides.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Every household follows the default.
        </p>
      ) : (
        <ul aria-label={`Overrides for ${flag.key}`}>
          {overrides?.map((override) => (
            <li
              key={override.householdId}
              className="ledger-row flex-wrap"
            >
              <span className="min-w-0 flex-1 basis-40">
                <span className="block font-medium break-words">
                  {override.householdName}
                </span>
                <span className="mt-0.5 block text-xs text-muted-foreground">
                  {override.memberCount} member
                  {override.memberCount === 1 ? "" : "s"}
                  {override.updatedByName
                    ? ` · set by ${override.updatedByName}`
                    : ""}
                </span>
              </span>
              <Switch
                checked={override.enabled}
                label={`${flag.key} for ${override.householdName}`}
                disabled={busy !== null}
                onChange={(enabled) =>
                  set(override.householdId, override.householdName, enabled)
                }
              />
              <ConfirmButton
                className="btn-ghost-destructive btn-sm min-h-11"
                aria-label={`Remove the override for ${override.householdName}`}
                disabled={busy !== null}
                title={`Remove the ${flag.key} override for ${override.householdName}?`}
                description="The household follows the flag's default again."
                confirmLabel="Remove override"
                pendingLabel="Removing…"
                errorFallback="Could not remove the override"
                onConfirm={() => clear(override)}
              >
                Remove
              </ConfirmButton>
            </li>
          ))}
        </ul>
      )}

      <form
        onSubmit={search}
        role="search"
        aria-label={`Find a household for ${flag.key}`}
        className="flex flex-wrap items-end gap-2"
      >
        <div className="min-w-0 flex-1 basis-48">
          <label htmlFor={`${id}-search`} className="label">
            Find a household
          </label>
          <input
            id={`${id}-search`}
            className="input w-full"
            value={term}
            onChange={(event) => setTerm(event.target.value)}
            placeholder="Household name"
            autoComplete="off"
          />
        </div>
        <button type="submit" className="btn-secondary btn-sm min-h-11">
          Search
        </button>
        {searchError && (
          <p role="alert" className="basis-full text-sm text-destructive">
            {searchError}
          </p>
        )}
      </form>
      {results && (
        <ul aria-label="Households found">
          {results.items.length === 0 ? (
            <li className="text-sm text-muted-foreground">No household matches.</li>
          ) : (
            results.items.map((household) => (
              <li key={household.id} className="ledger-row flex-wrap">
                <span className="min-w-0 flex-1 basis-40">
                  <span className="block font-medium break-words">
                    {household.name}
                  </span>
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {household.memberCount} member
                    {household.memberCount === 1 ? "" : "s"}
                  </span>
                </span>
                <button
                  type="button"
                  className="btn-secondary btn-sm min-h-11"
                  disabled={busy !== null}
                  onClick={() => set(household.id, household.name, opposite)}
                >
                  Turn {opposite ? "on" : "off"} for {household.name}
                </button>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
