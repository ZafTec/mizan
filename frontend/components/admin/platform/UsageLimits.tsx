"use client";

import { useId, useState } from "react";
import { usageLimitFormSchema } from "@/lib/admin/usage-limit-form";
import {
  USAGE_LINES,
  clearUsageLimit,
  getUsageLimitSubject,
  searchHouseholds,
  searchUsers,
  setUsageLimit,
  type EffectiveUsageLimit,
  type SourcedLimit,
  type UsageLimitOverride,
  type UsageLimitSubject,
  type UsageScope,
} from "@/lib/api/admin-platform";
import { appToast, getErrorMessage } from "@/lib/toast";
import { ConfirmButton } from "@/components/ConfirmDialog";
import { Select } from "@/components/ui/select";

const LINE_LABEL: Record<string, string> = {
  Personal: "Personal",
  Planning: "Meal planning",
  Onboarding: "Onboarding",
  TrainerClient: "Coaching",
  Eval: "Evaluation",
};

const FIELDS = [
  { key: "dailyRequests", label: "Requests a day" },
  { key: "dailyTokens", label: "Tokens a day" },
  { key: "dailyCostMicros", label: "Cost a day (micros)" },
  { key: "monthlyCostMicros", label: "Cost a month (micros)" },
] as const;

type FieldKey = (typeof FIELDS)[number]["key"];

const lineLabel = (line: string | null | undefined) =>
  line ? (LINE_LABEL[line] ?? line) : "All lines";

const show = (value: number | null | undefined) =>
  value === null || value === undefined ? "No cap" : value.toLocaleString();

/**
 * The subjects of one scope with the limits in force on every line and where
 * each value comes from (docs/ADMIN.md#usage-limits). The numbers are the
 * API's; the form only sends what the administrator typed.
 */
export default function UsageLimits({
  scope,
  subjects,
}: {
  scope: UsageScope;
  subjects: UsageLimitSubject[];
}) {
  const [added, setAdded] = useState<UsageLimitSubject[]>([]);
  const all = [
    ...added.filter((a) => !subjects.some((s) => s.subjectId === a.subjectId)),
    ...subjects,
  ];

  return (
    <div className="space-y-6">
      {scope !== "Plan" && (
        <AddSubject
          scope={scope}
          onPick={(subject) =>
            setAdded((current) => [
              subject,
              ...current.filter((s) => s.subjectId !== subject.subjectId),
            ])
          }
        />
      )}
      {all.length === 0 ? (
        <p className="log-empty">
          {scope === "Plan"
            ? "There are no billing plans yet."
            : `No ${scope === "User" ? "user" : "household"} has an override.`}
        </p>
      ) : (
        <ul aria-label={`${scope} limits`}>
          {all.map((subject) => (
            <SubjectRow
              key={subject.subjectId}
              initial={subject}
              startOpen={added.some((a) => a.subjectId === subject.subjectId)}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

function AddSubject({
  scope,
  onPick,
}: {
  scope: UsageScope;
  onPick: (subject: UsageLimitSubject) => void;
}) {
  const id = useId();
  const [term, setTerm] = useState("");
  const [results, setResults] = useState<
    { id: string; name: string; detail: string }[] | null
  >(null);
  const [error, setError] = useState<string | null>(null);
  const noun = scope === "User" ? "user" : "household";

  const search = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!term.trim()) {
      setError(
        scope === "User"
          ? "Type part of a name or an email."
          : "Type part of a household's name.",
      );
      return;
    }
    setError(null);
    try {
      if (scope === "User") {
        const page = await searchUsers(term.trim());
        setResults(
          page.items.map((user) => ({
            id: user.id,
            name: user.name || user.email,
            detail: user.email,
          })),
        );
      } else {
        const page = await searchHouseholds(term.trim());
        setResults(
          page.items.map((household) => ({
            id: household.id,
            name: household.name,
            detail: `${household.memberCount} member${household.memberCount === 1 ? "" : "s"}`,
          })),
        );
      }
    } catch (caught) {
      setError(getErrorMessage(caught, `Could not search for a ${noun}.`));
    }
  };

  const pick = async (subjectId: string) => {
    try {
      onPick(await getUsageLimitSubject(scope, subjectId));
      setResults(null);
      setTerm("");
    } catch (caught) {
      setError(getErrorMessage(caught, `Could not open that ${noun}.`));
    }
  };

  return (
    <section aria-labelledby={`${id}-title`}>
      <h2 id={`${id}-title`} className="text-base font-semibold">
        Add a {noun}
      </h2>
      <form
        onSubmit={search}
        role="search"
        aria-label={`Find a ${noun}`}
        className="mt-2 flex flex-wrap items-end gap-2"
      >
        <div className="min-w-0 flex-1 basis-48">
          <label htmlFor={`${id}-term`} className="label">
            {scope === "User" ? "Name or email" : "Household name"}
          </label>
          <input
            id={`${id}-term`}
            className="input w-full"
            value={term}
            autoComplete="off"
            onChange={(event) => setTerm(event.target.value)}
          />
        </div>
        <button type="submit" className="btn-secondary btn-sm min-h-11">
          Search
        </button>
        {error && (
          <p role="alert" className="basis-full text-sm text-destructive">
            {error}
          </p>
        )}
      </form>
      {results && (
        <ul aria-label={`${scope === "User" ? "Users" : "Households"} found`}>
          {results.length === 0 ? (
            <li className="mt-2 text-sm text-muted-foreground">Nothing matches.</li>
          ) : (
            results.map((result) => (
              <li key={result.id} className="ledger-row flex-wrap">
                <span className="min-w-0 flex-1 basis-40">
                  <span className="block font-medium break-words">
                    {result.name}
                  </span>
                  <span className="mt-0.5 block text-xs text-muted-foreground break-all">
                    {result.detail}
                  </span>
                </span>
                <button
                  type="button"
                  className="btn-secondary btn-sm min-h-11"
                  onClick={() => pick(result.id)}
                >
                  Set limits for {result.name}
                </button>
              </li>
            ))
          )}
        </ul>
      )}
    </section>
  );
}

function SubjectRow({
  initial,
  startOpen,
}: {
  initial: UsageLimitSubject;
  startOpen: boolean;
}) {
  const id = useId();
  const [subject, setSubject] = useState(initial);
  const [open, setOpen] = useState(startOpen);
  const count = subject.overrides.length;

  return (
    <li
      className="border-b border-border py-4"
      data-testid={`limits-${subject.subjectId}`}
    >
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <div className="min-w-0 flex-1 basis-56">
          <p className="font-medium break-words">{subject.name}</p>
          <p className="mt-0.5 text-xs text-muted-foreground break-words">
            {[
              subject.detail,
              `${subject.tier === "pro" ? "Pro" : "Free"} defaults`,
            ]
              .filter(Boolean)
              .join(" · ")}
            {" · "}
            {count === 0
              ? "No override"
              : `${count} override${count === 1 ? "" : "s"}`}
          </p>
        </div>
        <button
          type="button"
          className="btn-ghost btn-sm min-h-11"
          aria-expanded={open}
          aria-controls={`${id}-panel`}
          aria-label={`${open ? "Hide" : "Show"} limits for ${subject.name}`}
          onClick={() => setOpen((value) => !value)}
        >
          {open ? "Hide limits" : "Limits"}
        </button>
      </div>
      {open && (
        <div id={`${id}-panel`} className="mt-4 space-y-6">
          <EffectiveTable subject={subject} />
          <OverrideEditor subject={subject} onChange={setSubject} />
        </div>
      )}
    </li>
  );
}

function EffectiveTable({ subject }: { subject: UsageLimitSubject }) {
  return (
    <div className="overflow-x-auto border border-border">
      <table className="w-full min-w-[34rem] border-collapse text-sm">
        <caption className="caption-bottom px-3 py-2 text-left text-xs text-muted-foreground">
          Limits in force for {subject.name}. Under each value, where it comes
          from: an override for the user, the household or the plan, or the tier
          default.
        </caption>
        <thead>
          <tr className="border-b border-charcoal-blue-900 dark:border-charcoal-blue-50">
            <th
              scope="col"
              className="px-3 py-2 text-left text-xs font-semibold"
            >
              Line
            </th>
            {FIELDS.map((field) => (
              <th
                key={field.key}
                scope="col"
                className="px-3 py-2 text-right text-xs font-semibold"
              >
                {field.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {subject.effective.map((row: EffectiveUsageLimit) => (
            <tr key={row.line}>
              <th scope="row" className="px-3 py-2 text-left font-medium">
                {lineLabel(row.line)}
              </th>
              {FIELDS.map((field) => {
                const limit = row[field.key] as SourcedLimit;
                return (
                  <td key={field.key} className="tabular-nums px-3 py-2 text-right">
                    <span className="block">{show(limit.value)}</span>
                    <span className="block text-[11px] text-muted-foreground">
                      {limit.source}
                    </span>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

type FormState = { line: string; note: string } & Record<FieldKey, string>;

const emptyForm: FormState = {
  line: "all",
  dailyRequests: "",
  dailyTokens: "",
  dailyCostMicros: "",
  monthlyCostMicros: "",
  note: "",
};

const toForm = (override: UsageLimitOverride): FormState => ({
  line: override.line ?? "all",
  dailyRequests: override.dailyRequests?.toString() ?? "",
  dailyTokens: override.dailyTokens?.toString() ?? "",
  dailyCostMicros: override.dailyCostMicros?.toString() ?? "",
  monthlyCostMicros: override.monthlyCostMicros?.toString() ?? "",
  note: override.note ?? "",
});

function OverrideEditor({
  subject,
  onChange,
}: {
  subject: UsageLimitSubject;
  onChange: (subject: UsageLimitSubject) => void;
}) {
  const id = useId();
  const scope = subject.scope as UsageScope;
  const [form, setForm] = useState<FormState>(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    const parsed = usageLimitFormSchema.safeParse(form);
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? "Check the caps.");
      return;
    }
    setError(null);
    setBusy(true);
    try {
      onChange(await setUsageLimit(scope, subject.subjectId, parsed.data));
      appToast.success(`Saved the ${lineLabel(parsed.data.line)} override`);
      setForm(emptyForm);
    } catch (caught) {
      setError(getErrorMessage(caught, "Could not save the override."));
    } finally {
      setBusy(false);
    }
  };

  const remove = async (override: UsageLimitOverride) => {
    setBusy(true);
    try {
      await clearUsageLimit(scope, subject.subjectId, override.line ?? null);
      onChange(await getUsageLimitSubject(scope, subject.subjectId));
      appToast.success(`Removed the ${lineLabel(override.line)} override`);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-4">
      {subject.overrides.length > 0 && (
        <ul aria-label={`Overrides for ${subject.name}`}>
          {subject.overrides.map((override) => (
            <li key={override.id} className="ledger-row flex-wrap">
              <span className="min-w-0 flex-1 basis-48">
                <span className="block font-medium">
                  {lineLabel(override.line)}
                </span>
                <span className="mt-0.5 block text-xs text-muted-foreground break-words">
                  {FIELDS.filter(
                    (field) =>
                      override[field.key] !== null &&
                      override[field.key] !== undefined,
                  )
                    .map(
                      (field) =>
                        `${field.label}: ${override[field.key]!.toLocaleString()}`,
                    )
                    .join(" · ")}
                  {override.note ? ` · ${override.note}` : ""}
                </span>
              </span>
              <button
                type="button"
                className="btn-ghost btn-sm min-h-11"
                aria-label={`Edit the ${lineLabel(override.line)} override`}
                disabled={busy}
                onClick={() => setForm(toForm(override))}
              >
                Edit
              </button>
              <ConfirmButton
                className="btn-ghost-destructive btn-sm min-h-11"
                aria-label={`Remove the ${lineLabel(override.line)} override`}
                disabled={busy}
                title={`Remove the ${lineLabel(override.line)} override for ${subject.name}?`}
                description="The usage limits fall back to the plan's defaults."
                confirmLabel="Remove override"
                pendingLabel="Removing…"
                errorFallback="Could not remove the override"
                onConfirm={() => remove(override)}
              >
                Remove
              </ConfirmButton>
            </li>
          ))}
        </ul>
      )}

      <form
        onSubmit={save}
        aria-label={`Override for ${subject.name}`}
        className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3"
        noValidate
      >
        <div>
          <label htmlFor={`${id}-line`} className="label">
            Line
          </label>
          <Select
            id={`${id}-line`}
            value={form.line}
            onChange={(line) => setForm({ ...form, line })}
            options={[
              { value: "all", label: "All lines" },
              ...USAGE_LINES.map((line) => ({
                value: line,
                label: lineLabel(line),
              })),
            ]}
          />
        </div>
        {FIELDS.map((field) => (
          <div key={field.key}>
            <label htmlFor={`${id}-${field.key}`} className="label">
              {field.label}
            </label>
            <input
              id={`${id}-${field.key}`}
              className="input w-full"
              inputMode="numeric"
              placeholder="Inherit"
              value={form[field.key]}
              onChange={(event) =>
                setForm({ ...form, [field.key]: event.target.value })
              }
            />
          </div>
        ))}
        <div className="sm:col-span-2 lg:col-span-1">
          <label htmlFor={`${id}-note`} className="label">
            Note (optional)
          </label>
          <input
            id={`${id}-note`}
            className="input w-full"
            value={form.note}
            onChange={(event) => setForm({ ...form, note: event.target.value })}
          />
        </div>
        <p className="text-xs text-muted-foreground sm:col-span-2 lg:col-span-3">
          An empty box inherits. Saving a line that already has an override
          replaces it.
        </p>
        {error && (
          <p
            role="alert"
            className="text-sm text-destructive sm:col-span-2 lg:col-span-3"
          >
            {error}
          </p>
        )}
        <div className="sm:col-span-2 lg:col-span-3">
          <button
            type="submit"
            className="btn-primary btn-sm min-h-11"
            disabled={busy}
          >
            Save override
          </button>
        </div>
      </form>
    </div>
  );
}
