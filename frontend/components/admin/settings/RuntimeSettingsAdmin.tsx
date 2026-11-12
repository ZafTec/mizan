"use client";

import { useId, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  clearRuntimeSetting,
  rotateEndUserKey,
  setRuntimeSetting,
  testAiProvider,
  testSmtp,
  type RuntimeSetting,
  type RuntimeSettings,
} from "@/lib/api/admin-settings";
import { appToast, getErrorMessage } from "@/lib/toast";
import { ConfirmButton } from "@/components/ConfirmDialog";
import { Select } from "@/components/ui/select";
import { useSectionInView } from "@/lib/hooks/useSectionInView";
import { cn } from "@/lib/utils";

/** Where the value comes from, in one word at the right of the row. */
const SOURCE: Record<string, string> = {
  database: "Stored",
  default: "Default",
  unset: "Unset",
};

/** What the row shows: a secret is only ever "Set" or "Not set". */
function shown(setting: RuntimeSetting): string {
  if (setting.isSecret) return setting.hasValue ? "Set" : "Not set";
  if (setting.source === "unset") return "Not set";
  return setting.value === "" ? "Empty" : (setting.value ?? "Not set");
}

/**
 * /admin/settings: the runtime settings in their sections
 * (docs/ADMIN.md#runtime-settings), with an anchor list that marks the
 * section in view on a wide screen. The sections, rows, rules and messages
 * come from the API's catalog, so a new key appears without a change here; a
 * value the API refuses shows its message under the field. Saving or clearing
 * replaces the row with what the API answered, and refreshes the page so a
 * section's notice (such as "Email is not configured") follows what is now
 * set. The notice text is the API's.
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
  const router = useRouter();
  const replace = (setting: RuntimeSetting) => {
    setRows((current) => ({ ...current, [setting.key]: setting }));
    router.refresh();
  };
  const ids = useMemo(
    () => settings.groups.map((group) => group.id),
    [settings.groups],
  );
  const active = useSectionInView(ids);

  return (
    <div className="flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10">
      <nav
        aria-label="On this page"
        className="hidden lg:sticky lg:top-0 lg:flex lg:w-[200px] lg:shrink-0 lg:flex-col lg:gap-0.5"
      >
        <p className="px-2.5 pb-2 text-[12px] text-muted-foreground">
          On this page
        </p>
        {settings.groups.map((group) => (
          <a
            key={group.id}
            href={`#${group.id}`}
            aria-current={active === group.id ? "location" : undefined}
            className={cn(
              "flex min-h-9 items-center rounded-[3px] px-2.5 text-[14px] transition-colors hover:bg-hair",
              active === group.id
                ? "bg-hair font-medium text-foreground"
                : "text-muted-foreground",
            )}
          >
            {group.title}
          </a>
        ))}
      </nav>

      <div className="min-w-0 flex-1 space-y-12">
        {settings.groups.map((group) => (
          <section
            key={group.id}
            id={group.id}
            aria-labelledby={`settings-${group.id}`}
            className="setting-section"
          >
            <header>
              <h2 id={`settings-${group.id}`} className="text-xl font-semibold">
                {group.title}
              </h2>
              {group.id === "email" && <SmtpTest />}
              {group.id === "ai-provider" && <AiTest />}
              {group.id === "end-user-tokens" && <RotateKey />}
            </header>
            {group.notice && (
              <p
                data-testid={`settings-notice-${group.id}`}
                className="border-b border-border py-3 text-[14px] text-muted-foreground"
              >
                {group.notice}
              </p>
            )}
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
      appToast.success(`${setting.label} uses the default again`);
      close();
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
      className="setting-row items-start!"
      data-testid={`setting-${setting.key}`}
    >
      <div className="min-w-0">
        <p className="text-[14px] font-medium break-words">{setting.label}</p>
        <p
          id={`${id}-help`}
          className="mt-0.5 text-[12px] text-muted-foreground"
        >
          {setting.description}
        </p>
        <p className="mt-0.5 text-[12px] break-all text-muted-foreground">
          <code>{setting.key}</code>
        </p>
      </div>
      <div className="flex min-w-0 flex-col gap-3">
        <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-2">
          <span
            className="min-w-0 flex-1 basis-32 text-[14px] break-all"
            data-testid="setting-value"
          >
            {shown(setting)}
          </span>
          {!editing && (
            <button
              type="button"
              className="btn-secondary btn-sm min-h-9"
              aria-label={`Edit ${setting.label}`}
              disabled={busy}
              onClick={open}
            >
              {setting.isSecret
                ? setting.hasValue
                  ? "Replace"
                  : "Set"
                : "Edit"}
            </button>
          )}
          {setting.source === "database" && (
            <ConfirmButton
              className="btn-ghost-destructive btn-sm min-h-9"
              aria-label={`Clear ${setting.label}`}
              disabled={busy}
              title={`Clear ${setting.label}?`}
              description="The saved value is deleted and the setting uses its environment or built-in default again."
              confirmLabel="Clear setting"
              pendingLabel="Clearing…"
              errorFallback={`Could not clear ${setting.label}`}
              onConfirm={clear}
            >
              Clear
            </ConfirmButton>
          )}
          <span className="w-14 text-right text-[12px] text-muted-foreground">
            {source}
          </span>
        </div>
        {editing && (
          <form
            onSubmit={save}
            onKeyDown={(event) => {
              if (event.key === "Escape") close();
            }}
            aria-label={`Edit ${setting.label}`}
            className="flex flex-wrap items-end gap-3"
          >
            <div className="min-w-0 flex-1 basis-56">
              <label htmlFor={control.id} className="label">
                {setting.isSecret
                  ? `New ${setting.label.toLowerCase()}`
                  : setting.label}
              </label>
              {setting.type === "enum" || setting.type === "bool" ? (
                <Select
                  id={control.id}
                  value={control.value}
                  aria-invalid={control["aria-invalid"]}
                  aria-describedby={control["aria-describedby"]}
                  autoFocus
                  onChange={setDraft}
                  options={(setting.type === "bool"
                    ? ["", "true", "false"]
                    : ["", ...setting.allowedValues]
                  ).map((option) => ({
                    value: option,
                    label: option === "" ? "(empty)" : option,
                  }))}
                />
              ) : setting.format === "json" ||
                setting.format === "ecprivatekey" ||
                setting.format === "ecpublickey" ? (
                <textarea
                  {...control}
                  autoFocus
                  rows={5}
                  spellCheck={false}
                  autoComplete="off"
                  className={cn(control.className, "font-mono text-xs")}
                  placeholder={
                    setting.format === "json"
                      ? "Paste the key file's JSON"
                      : "-----BEGIN …-----"
                  }
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
                  placeholder={
                    setting.isSecret ? "Enter a new value" : undefined
                  }
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
      </div>
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

/**
 * Makes a new signing key. The key in use stays published as the previous
 * key, so tokens it signed in the last minutes still verify.
 */
function RotateKey() {
  const router = useRouter();
  const [outcome, setOutcome] = useState<string | null>(null);

  const rotate = async () => {
    const result = await rotateEndUserKey();
    setOutcome(
      result.previousKeyId
        ? `Signing with ${result.keyId}; ${result.previousKeyId} stays published until its tokens expire.`
        : `Signing with ${result.keyId}.`,
    );
    appToast.success("Signing key rotated");
    router.refresh();
  };

  return (
    <div className="flex min-w-0 flex-wrap items-center justify-end gap-3">
      <p
        role="status"
        className="min-w-0 text-sm break-words text-muted-foreground"
      >
        {outcome}
      </p>
      <ConfirmButton
        className="btn-secondary btn-sm min-h-9"
        title="Rotate the signing key?"
        description="Mizan makes a new key and signs with it from now on. The current key stays published as the previous key, and the one before it is dropped."
        confirmLabel="Rotate key"
        pendingLabel="Rotating…"
        errorFallback="Could not rotate the signing key"
        onConfirm={rotate}
      >
        Rotate key
      </ConfirmButton>
    </div>
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
      <p
        role="status"
        className="min-w-0 text-sm break-words text-muted-foreground"
      >
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
