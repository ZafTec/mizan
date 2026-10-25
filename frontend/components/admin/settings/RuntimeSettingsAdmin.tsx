"use client";

import { useId, useState } from "react";
import Link from "next/link";
import { Pill, type PillTone } from "@/components/ui/data-table";
import {
  clearRuntimeSetting,
  setRuntimeSetting,
  testAiProvider,
  testSmtp,
  type RuntimeSetting,
  type RuntimeSettings,
} from "@/lib/api/admin-settings";
import { appToast, getErrorMessage } from "@/lib/toast";

const SOURCE: Record<string, { label: string; tone: PillTone }> = {
  database: { label: "Set here", tone: "info" },
  configuration: { label: "Configuration", tone: "neutral" },
  unset: { label: "Unset", tone: "warn" },
};

/** What the row shows: a secret is only ever "Set" or "Not set". */
function shown(setting: RuntimeSetting): string {
  if (setting.isSecret) return setting.hasValue ? "Set" : "Not set";
  if (setting.source === "unset") return "Not set";
  return setting.value === "" ? "Empty" : (setting.value ?? "Not set");
}

/**
 * /admin/settings: the runtime settings in their sections
 * (docs/ADMIN.md#runtime-settings). The rows, rules and messages come from
 * the API's catalog; a value the API refuses shows its message under the
 * field. Saving or clearing replaces the row with what the API answered.
 */
export default function RuntimeSettingsAdmin({
  settings,
}: {
  settings: RuntimeSettings;
}) {
  const [rows, setRows] = useState(() =>
    Object.fromEntries(
      settings.groups.flatMap((group) =>
        group.settings.map((setting) => [setting.key, setting] as const),
      ),
    ),
  );
  const replace = (setting: RuntimeSetting) =>
    setRows((current) => ({ ...current, [setting.key]: setting }));

  return (
    <div className="space-y-10">
      {settings.groups.map((group) => (
        <section
          key={group.id}
          aria-labelledby={`settings-${group.id}`}
          className="scroll-mt-6"
        >
          <header className="log-section-header flex-wrap">
            <h2 id={`settings-${group.id}`}>{group.title}</h2>
            {group.id === "email" && <SmtpTest />}
            {group.id === "ai-provider" && <AiTest />}
          </header>
          <ul>
            {group.settings.map((setting) => (
              <SettingRow
                key={setting.key}
                setting={rows[setting.key] ?? setting}
                onChange={replace}
              />
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function SettingRow({
  setting,
  onChange,
}: {
  setting: RuntimeSetting;
  onChange: (setting: RuntimeSetting) => void;
}) {
  const id = useId();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const source = SOURCE[setting.source] ?? SOURCE.unset;

  const open = () => {
    setDraft(setting.isSecret ? "" : (setting.value ?? ""));
    setError(null);
    setEditing(true);
  };
  const close = () => {
    setEditing(false);
    setError(null);
  };

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      onChange(await setRuntimeSetting(setting.key, draft));
      appToast.success(`${setting.label} saved`);
      setEditing(false);
    } catch (caught) {
      setError(getErrorMessage(caught, `Could not save ${setting.label}`));
    } finally {
      setBusy(false);
    }
  };

  const clear = async () => {
    setBusy(true);
    try {
      onChange(await clearRuntimeSetting(setting.key));
      appToast.success(`${setting.label} uses configuration again`);
      close();
    } catch (caught) {
      appToast.error(caught, `Could not clear ${setting.label}`);
    } finally {
      setBusy(false);
    }
  };

  const errorId = `${id}-error`;
  const control = {
    id: `${id}-value`,
    value: draft,
    "aria-invalid": error ? true : undefined,
    "aria-describedby": error ? errorId : `${id}-help`,
    className: `input w-full ${error ? "input-error" : ""}`,
  };

  return (
    <li
      className="ledger-row flex-wrap items-start!"
      data-testid={`setting-${setting.key}`}
    >
      <div className="min-w-0 flex-1 basis-64">
        <p className="font-medium break-words">{setting.label}</p>
        <p id={`${id}-help`} className="mt-0.5 text-xs log-muted">
          {setting.description}
        </p>
        <p className="mt-0.5 text-xs log-muted">
          <code>{setting.key}</code>
        </p>
      </div>
      <div className="flex min-w-0 flex-wrap items-center gap-x-4 gap-y-2">
        <span
          className="max-w-full break-all text-sm"
          data-testid="setting-value"
        >
          {shown(setting)}
        </span>
        <Pill tone={source.tone}>{source.label}</Pill>
        {!editing && (
          <button
            type="button"
            className="btn-secondary btn-sm min-h-9"
            aria-label={`Edit ${setting.label}`}
            disabled={busy}
            onClick={open}
          >
            Edit
          </button>
        )}
        {setting.source === "database" && (
          <button
            type="button"
            className="btn-ghost btn-sm min-h-9"
            aria-label={`Clear ${setting.label}`}
            disabled={busy}
            onClick={clear}
          >
            Clear
          </button>
        )}
      </div>
      {editing && (
        <form
          onSubmit={save}
          onKeyDown={(event) => {
            if (event.key === "Escape") close();
          }}
          aria-label={`Edit ${setting.label}`}
          className="flex basis-full flex-wrap items-end gap-3 pb-2"
        >
          <div className="min-w-0 flex-1 basis-60">
            <label htmlFor={control.id} className="label">
              {setting.isSecret
                ? `New ${setting.label.toLowerCase()}`
                : setting.label}
            </label>
            {setting.type === "enum" || setting.type === "bool" ? (
              <select
                {...control}
                autoFocus
                onChange={(event) => setDraft(event.target.value)}
              >
                {(setting.type === "bool"
                  ? ["", "true", "false"]
                  : ["", ...setting.allowedValues]
                ).map((option) => (
                  <option key={option} value={option}>
                    {option === "" ? "(empty)" : option}
                  </option>
                ))}
              </select>
            ) : setting.format === "json" ? (
              <textarea
                {...control}
                autoFocus
                rows={5}
                spellCheck={false}
                placeholder="Paste the key file's JSON"
                onChange={(event) => setDraft(event.target.value)}
              />
            ) : (
              <input
                {...control}
                autoFocus
                type={setting.isSecret ? "password" : "text"}
                inputMode={setting.type === "int" ? "numeric" : undefined}
                autoComplete={setting.isSecret ? "new-password" : "off"}
                spellCheck={false}
                placeholder={setting.isSecret ? "Enter a new value" : undefined}
                onChange={(event) => setDraft(event.target.value)}
              />
            )}
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
              onClick={close}
            >
              Cancel
            </button>
          </div>
          {error && (
            <p
              id={errorId}
              role="alert"
              className="basis-full text-sm text-destructive"
            >
              {error}
            </p>
          )}
        </form>
      )}
    </li>
  );
}

function SmtpTest() {
  const [busy, setBusy] = useState(false);
  const [outcome, setOutcome] = useState<React.ReactNode>(null);

  const run = async () => {
    setBusy(true);
    setOutcome(null);
    try {
      const queued = await testSmtp();
      setOutcome(
        <>
          Queued for {queued.to}. Job <code>{queued.jobId}</code>;{" "}
          <Link href="/admin/jobs?type=email" className="underline">
            see Jobs
          </Link>{" "}
          for delivery.
        </>,
      );
    } catch (caught) {
      setOutcome(getErrorMessage(caught, "Could not queue the test email"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <TestButton label="Send test email" busy={busy} onClick={run}>
      {outcome}
    </TestButton>
  );
}

function AiTest() {
  const [busy, setBusy] = useState(false);
  const [outcome, setOutcome] = useState<React.ReactNode>(null);

  const run = async () => {
    setBusy(true);
    setOutcome(null);
    try {
      const result = await testAiProvider();
      setOutcome(
        result.reachable
          ? `Reachable: ${result.model} answered in ${result.latencyMs} ms.`
          : `Not reachable: ${result.errorClass ?? "unknown"}.`,
      );
    } catch (caught) {
      setOutcome(getErrorMessage(caught, "Could not test the provider"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <TestButton label="Test provider" busy={busy} onClick={run}>
      {outcome}
    </TestButton>
  );
}

function TestButton({
  label,
  busy,
  onClick,
  children,
}: {
  label: string;
  busy: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-w-0 flex-wrap items-center justify-end gap-3">
      <p role="status" className="min-w-0 text-sm log-muted break-words">
        {busy ? "Testing…" : children}
      </p>
      <button
        type="button"
        className="btn-secondary btn-sm min-h-9"
        disabled={busy}
        onClick={onClick}
      >
        {label}
      </button>
    </div>
  );
}
