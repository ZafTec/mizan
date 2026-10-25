"use client";

import { useId, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  applyTemplate,
  applyTemplateForClient,
  createMealPlan,
  createPlanForClient,
  type Template,
} from "@/lib/api/mealPlans";
import {
  emptyRulesForm,
  parsePlanForm,
  parseRules,
  type PlanFormErrors,
  type RulesFormErrors,
  type RulesFormValues,
} from "@/lib/planning/rules";
import { appToast, getErrorMessage } from "@/lib/toast";
import RulesFields from "./RulesFields";

/**
 * A new plan: from a template with a start date, or empty with its own rules.
 * The same form drafts a plan for a client when `clientId` is set, from the
 * coach's templates (their own and the global ones) or empty; households do
 * not apply there, since the draft belongs to the client.
 */
export default function NewPlanForm({
  templates,
  household,
  today,
  clientId,
  clientName,
}: {
  templates: Template[];
  household: { id: string; name: string } | null;
  today: string;
  clientId?: string;
  clientName?: string;
}) {
  const router = useRouter();
  const id = useId();
  const forClient = Boolean(clientId);
  const [mode, setMode] = useState<"template" | "empty">(
    templates.length > 0 ? "template" : "empty",
  );
  const [templateId, setTemplateId] = useState(templates[0]?.id ?? "");
  const [name, setName] = useState("");
  const [startDate, setStartDate] = useState(today);
  const [durationDays, setDurationDays] = useState("7");
  const [shared, setShared] = useState(false);
  const [rules, setRules] = useState<RulesFormValues>(emptyRulesForm);
  const [planErrors, setPlanErrors] = useState<PlanFormErrors>({});
  const [rulesErrors, setRulesErrors] = useState<RulesFormErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const template = templates.find((item) => item.id === templateId);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    if (mode === "template") {
      if (!template) {
        setError("Choose a template.");
        return;
      }
      const parsed = parsePlanForm({ name, startDate, durationDays: 1 });
      if (parsed.errors) {
        setPlanErrors(parsed.errors);
        return;
      }
      setBusy(true);
      try {
        if (clientId) {
          await applyTemplateForClient(clientId, {
            templateId: template.id,
            startDate: parsed.plan.startDate,
            name: parsed.plan.name || null,
          });
          appToast.success(`Draft sent to ${clientName ?? "your client"}`);
          setSent(true);
          return;
        }
        const created = await applyTemplate(template.id, {
          startDate: parsed.plan.startDate,
          householdId: shared && household ? household.id : null,
          name: parsed.plan.name || null,
        });
        appToast.success(`${created.name ?? "Plan"} created`);
        router.push(`/meal-plan/${created.id}`);
      } catch (caught) {
        setError(getErrorMessage(caught, "Could not create the plan"));
        setBusy(false);
      }
      return;
    }
    const parsedPlan = parsePlanForm({ name, startDate, durationDays });
    const parsedRules = parseRules(rules);
    setPlanErrors(parsedPlan.errors ?? {});
    setRulesErrors(parsedRules.errors ?? {});
    if (parsedPlan.errors || parsedRules.errors) return;
    setBusy(true);
    try {
      const body = {
        name: parsedPlan.plan.name || null,
        startDate: parsedPlan.plan.startDate,
        durationDays: parsedPlan.plan.durationDays,
        householdId: shared && household ? household.id : null,
        rules: parsedRules.rules,
        entries: [],
        prepSessions: [],
      };
      const created = clientId
        ? await createPlanForClient(clientId, body)
        : await createMealPlan(body);
      if (clientId) {
        appToast.success(`Draft sent to ${clientName ?? "your client"}`);
        setSent(true);
        return;
      }
      appToast.success(`${created.name ?? "Plan"} created`);
      router.push(`/meal-plan/${created.id}`);
    } catch (caught) {
      setError(getErrorMessage(caught, "Could not create the plan"));
      setBusy(false);
    }
  };

  if (sent && clientId)
    return (
      <p role="status" className="log-empty">
        Draft sent to {clientName ?? "your client"}.{" "}
        <Link href={`/trainer/clients/${clientId}`} className="text-link">
          Back to {clientName ?? "the client"}
        </Link>
      </p>
    );

  return (
    <form onSubmit={submit} className="space-y-8" noValidate>
      {templates.length > 0 && (
        <div
          role="radiogroup"
          aria-label="Start from"
          className="flex flex-wrap gap-2"
        >
          {(
            [
              ["template", "From a template"],
              ["empty", "Empty plan"],
            ] as const
          ).map(([value, label]) => (
            <label
              key={value}
              className={`flex min-h-11 cursor-pointer items-center gap-2 border px-4 text-sm font-medium ${mode === value ? "border-foreground" : "border-border"}`}
            >
              <input
                type="radio"
                name={`${id}-mode`}
                value={value}
                checked={mode === value}
                onChange={() => setMode(value)}
                className="accent-brand-700"
              />
              {label}
            </label>
          ))}
        </div>
      )}

      {mode === "template" && (
        <section aria-labelledby={`${id}-templates`}>
          <h2 id={`${id}-templates`} className="mb-3 text-base font-semibold">
            Template
          </h2>
          <ul role="radiogroup" aria-label="Templates">
            {templates.map((item) => (
              <li key={item.id}>
                <label className="ledger-row cursor-pointer">
                  <input
                    type="radio"
                    name={`${id}-template`}
                    value={item.id}
                    checked={templateId === item.id}
                    onChange={() => setTemplateId(item.id)}
                    className="h-4 w-4 accent-brand-700"
                  />
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium">{item.name}</span>
                    {item.description && (
                      <span className="mt-0.5 block text-sm log-muted">
                        {item.description}
                      </span>
                    )}
                  </span>
                  <span className="num whitespace-nowrap text-sm log-muted">
                    {item.durationDays} days · {item.entryCount} entries
                  </span>
                </label>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="space-y-5">
        <div>
          <label htmlFor={`${id}-name`} className="label">
            Name
          </label>
          <input
            id={`${id}-name`}
            type="text"
            className="input w-full"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder={
              template?.name ??
              (forClient ? `Plan for ${clientName ?? "client"}` : "This week")
            }
            aria-invalid={planErrors.name ? true : undefined}
          />
          {planErrors.name && (
            <p className="input-error mt-1 text-sm">{planErrors.name}</p>
          )}
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor={`${id}-start`} className="label">
              Start date
            </label>
            <input
              id={`${id}-start`}
              type="date"
              className="input w-full"
              value={startDate}
              onChange={(event) => setStartDate(event.target.value)}
              aria-invalid={planErrors.startDate ? true : undefined}
            />
            {planErrors.startDate && (
              <p className="input-error mt-1 text-sm">{planErrors.startDate}</p>
            )}
          </div>
          {mode === "empty" ? (
            <div>
              <label htmlFor={`${id}-days`} className="label">
                Length (days)
              </label>
              <input
                id={`${id}-days`}
                type="number"
                inputMode="numeric"
                min={1}
                max={31}
                className="input num w-full"
                value={durationDays}
                onChange={(event) => setDurationDays(event.target.value)}
                aria-invalid={planErrors.durationDays ? true : undefined}
              />
              {planErrors.durationDays && (
                <p className="input-error mt-1 text-sm">
                  {planErrors.durationDays}
                </p>
              )}
            </div>
          ) : (
            <div>
              <p className="label">Length</p>
              <p className="num py-3 text-sm">
                {template
                  ? `${template.durationDays} days`
                  : "Choose a template"}
              </p>
            </div>
          )}
        </div>
        {household && !forClient && (
          <label className="flex min-h-11 items-center gap-3 text-sm">
            <input
              type="checkbox"
              className="h-5 w-5 accent-brand-700"
              checked={shared}
              onChange={(event) => setShared(event.target.checked)}
            />
            Share with {household.name}
          </label>
        )}
      </section>

      {mode === "empty" && (
        <RulesFields
          values={rules}
          errors={rulesErrors}
          durationDays={Number(durationDays) || 1}
          onChange={setRules}
        />
      )}

      {error && (
        <p role="alert" className="input-error text-sm">
          {error}
        </p>
      )}

      <button
        type="submit"
        className="btn-primary min-h-11 w-full sm:w-auto"
        disabled={busy}
      >
        {forClient ? "Send draft" : "Create plan"}
      </button>
    </form>
  );
}
