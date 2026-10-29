"use client";

import { useId, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  addPlanEntry,
  addPrepSession,
  getMealPlan,
  removePlanEntry,
  removePrepSession,
  SLOTS,
  updateMealPlan,
  updatePlanEntry,
  updatePrepSession,
  updateTemplate,
  type MealPlanDetail,
  type PlanEntry,
  type PlanEntryRequest,
  type PrepSession,
} from "@/lib/api/mealPlans";
import {
  entryQuantity,
  entryTitle,
  formatPlanDate,
  parsePlanForm,
  parseRules,
  parseTemplateForm,
  rulesToForm,
  slotLabel,
  type PlanFormErrors,
  type RulesFormErrors,
  type RulesFormValues,
} from "@/lib/planning/rules";
import { appToast, getErrorMessage } from "@/lib/toast";
import FoodPicker from "@/components/logging/FoodPicker";
import type { PickerItem } from "@/components/logging/logging";
import RulesFields from "./RulesFields";
import { dayLabel } from "./PlanDetail";
import { Select } from "@/components/ui/select";

type EntryDraft = {
  dayIndex: number;
  slot: string;
  kind: "Recipe" | "Food" | "Text";
  item: { id: string; name: string } | null;
  amount: string;
  text: string;
  notes: string;
  prepSessionId: string;
};

function draftFrom(entry: PlanEntry): EntryDraft {
  return {
    dayIndex: entry.dayIndex,
    slot: entry.slot,
    kind: entry.kind as EntryDraft["kind"],
    item:
      entry.kind === "Recipe" && entry.recipeId
        ? { id: entry.recipeId, name: entry.recipeTitle ?? "Recipe" }
        : entry.kind === "Food" && entry.foodId
          ? { id: entry.foodId, name: entry.foodName ?? "Food" }
          : null,
    amount: String(entry.servings ?? entry.grams ?? ""),
    text: entry.text ?? "",
    notes: entry.notes ?? "",
    prepSessionId: entry.prepSessionId ?? "",
  };
}

function toRequest(
  draft: EntryDraft,
  sortOrder: number,
): PlanEntryRequest | string {
  const amount = Number(draft.amount);
  if (draft.kind === "Text") {
    if (!draft.text.trim()) return "Write what the meal is.";
  } else {
    if (!draft.item) return "Pick a food or a recipe.";
    if (!(amount > 0))
      return draft.kind === "Food"
        ? "Grams must be above zero."
        : "Servings must be above zero.";
  }
  return {
    dayIndex: draft.dayIndex,
    slot: draft.slot,
    kind: draft.kind,
    recipeId: draft.kind === "Recipe" ? draft.item!.id : null,
    servings: draft.kind === "Recipe" ? amount : null,
    foodId: draft.kind === "Food" ? draft.item!.id : null,
    grams: draft.kind === "Food" ? amount : null,
    text: draft.kind === "Text" ? draft.text.trim() : null,
    notes: draft.notes.trim() || null,
    sortOrder,
    prepSessionId: draft.prepSessionId || null,
  };
}

const dayOptions = (plan: MealPlanDetail) =>
  plan.days.map((day) => ({
    value: String(day.dayIndex),
    label: dayLabel(day),
  }));

function EntryForm({
  plan,
  initial,
  date,
  onSubmit,
  onCancel,
  title,
}: {
  plan: MealPlanDetail;
  initial: EntryDraft;
  date: string;
  onSubmit: (draft: EntryDraft) => Promise<void>;
  onCancel: () => void;
  title: string;
}) {
  const id = useId();
  const [draft, setDraft] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const set = <K extends keyof EntryDraft>(key: K, value: EntryDraft[K]) =>
    setDraft((current) => ({ ...current, [key]: value }));
  const pick = (item: PickerItem) =>
    setDraft((current) => ({
      ...current,
      kind: item.kind === "food" ? "Food" : "Recipe",
      item: { id: item.id, name: item.name },
      amount: current.amount || (item.kind === "food" ? "100" : "1"),
    }));

  return (
    <form
      aria-label={title}
      className="space-y-4 border-b border-border py-5"
      onSubmit={async (event) => {
        event.preventDefault();
        const request = toRequest(draft, 0);
        if (typeof request === "string") {
          setError(request);
          return;
        }
        setBusy(true);
        setError(null);
        try {
          await onSubmit(draft);
        } catch (caught) {
          setError(getErrorMessage(caught, "Could not save the entry"));
          setBusy(false);
        }
      }}
    >
      <p className="font-medium">{title}</p>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label htmlFor={`${id}-day`} className="label">
            Day
          </label>
          <Select
            id={`${id}-day`}
            value={String(draft.dayIndex)}
            onChange={(next) => set("dayIndex", Number(next))}
            options={dayOptions(plan)}
          />
        </div>
        <div>
          <label htmlFor={`${id}-slot`} className="label">
            Meal
          </label>
          <Select
            id={`${id}-slot`}
            value={draft.slot}
            onChange={(next) => set("slot", next)}
            options={SLOTS.map((slot) => ({
              value: slot,
              label: slotLabel(slot),
            }))}
          />
        </div>
      </div>
      <div
        role="radiogroup"
        aria-label="Entry kind"
        className="flex flex-wrap gap-2"
      >
        {(
          [
            ["Recipe", "Food or recipe"],
            ["Text", "Just text"],
          ] as const
        ).map(([value, label]) => {
          const selected =
            value === "Text" ? draft.kind === "Text" : draft.kind !== "Text";
          return (
            <label
              key={value}
              className={`flex min-h-11 cursor-pointer items-center gap-2 border px-4 text-sm ${selected ? "border-foreground" : "border-border"}`}
            >
              <input
                type="radio"
                name={`${id}-kind`}
                className="accent-brand-700"
                checked={selected}
                onChange={() =>
                  set("kind", value === "Text" ? "Text" : "Recipe")
                }
              />
              {label}
            </label>
          );
        })}
      </div>
      {draft.kind === "Text" ? (
        <div>
          <label htmlFor={`${id}-text`} className="label">
            What to eat
          </label>
          <input
            id={`${id}-text`}
            type="text"
            maxLength={255}
            className="input w-full"
            value={draft.text}
            onChange={(event) => set("text", event.target.value)}
            placeholder="Eat out"
          />
        </div>
      ) : (
        <>
          {draft.item ? (
            <div className="flex flex-wrap items-end gap-4">
              <p className="min-w-0 flex-1 text-sm">
                <span className="block font-medium">{draft.item.name}</span>
                <button
                  type="button"
                  className="text-link"
                  onClick={() => set("item", null)}
                >
                  Choose another
                </button>
              </p>
              <div>
                <label htmlFor={`${id}-amount`} className="label">
                  {draft.kind === "Food" ? "Grams" : "Servings"}
                </label>
                <input
                  id={`${id}-amount`}
                  type="number"
                  inputMode="decimal"
                  min={0}
                  step="any"
                  className="input tabular-nums w-28"
                  value={draft.amount}
                  onChange={(event) => set("amount", event.target.value)}
                />
              </div>
            </div>
          ) : (
            <FoodPicker date={date} onSelect={pick} />
          )}
        </>
      )}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor={`${id}-notes`} className="label">
            Notes
          </label>
          <input
            id={`${id}-notes`}
            type="text"
            className="input w-full"
            value={draft.notes}
            onChange={(event) => set("notes", event.target.value)}
          />
        </div>
        {plan.prepSessions.length > 0 && (
          <div>
            <label htmlFor={`${id}-session`} className="label">
              Cooking session
            </label>
            <Select
              id={`${id}-session`}
              value={draft.prepSessionId}
              onChange={(next) => set("prepSessionId", next)}
              options={[
                { value: "", label: "None" },
                ...plan.prepSessions.map((session) => ({
                  value: session.id,
                  label: session.title,
                })),
              ]}
            />
          </div>
        )}
      </div>
      {error && (
        <p role="alert" className="input-error text-sm">
          {error}
        </p>
      )}
      <div className="flex gap-2">
        <button
          type="submit"
          className="btn-primary btn-sm min-h-10"
          disabled={busy}
        >
          Save entry
        </button>
        <button
          type="button"
          className="btn-ghost btn-sm min-h-10"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

function SessionForm({
  plan,
  initial,
  onSubmit,
  onCancel,
  title,
}: {
  plan: MealPlanDetail;
  initial: { dayIndex: number; title: string; notes: string };
  onSubmit: (value: {
    dayIndex: number;
    title: string;
    notes: string;
  }) => Promise<void>;
  onCancel: () => void;
  title: string;
}) {
  const id = useId();
  const [value, setValue] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  return (
    <form
      aria-label={title}
      className="space-y-4 border-b border-border py-5"
      onSubmit={async (event) => {
        event.preventDefault();
        if (!value.title.trim()) {
          setError("Give the session a title.");
          return;
        }
        setBusy(true);
        setError(null);
        try {
          await onSubmit(value);
        } catch (caught) {
          setError(getErrorMessage(caught, "Could not save the session"));
          setBusy(false);
        }
      }}
    >
      <p className="font-medium">{title}</p>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="sm:col-span-2">
          <label htmlFor={`${id}-title`} className="label">
            Title
          </label>
          <input
            id={`${id}-title`}
            type="text"
            className="input w-full"
            value={value.title}
            onChange={(event) =>
              setValue({ ...value, title: event.target.value })
            }
            placeholder="Sunday batch"
          />
        </div>
        <div>
          <label htmlFor={`${id}-day`} className="label">
            Day
          </label>
          <Select
            id={`${id}-day`}
            value={String(value.dayIndex)}
            onChange={(next) => setValue({ ...value, dayIndex: Number(next) })}
            options={dayOptions(plan)}
          />
        </div>
      </div>
      <div>
        <label htmlFor={`${id}-notes`} className="label">
          Notes
        </label>
        <input
          id={`${id}-notes`}
          type="text"
          className="input w-full"
          value={value.notes}
          onChange={(event) =>
            setValue({ ...value, notes: event.target.value })
          }
        />
      </div>
      {error && (
        <p role="alert" className="input-error text-sm">
          {error}
        </p>
      )}
      <div className="flex gap-2">
        <button
          type="submit"
          className="btn-primary btn-sm min-h-10"
          disabled={busy}
        >
          Save session
        </button>
        <button
          type="button"
          className="btn-ghost btn-sm min-h-10"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

/**
 * Everything on a plan a person may change: its name and dates, its rules,
 * the entries of each day and the cooking sessions. Each write goes to the
 * API and the plan is read back, so totals and violations stay the API's.
 * A template is edited here too: it has no start date, and its name, length
 * and rules go through the templates route, while its entries and sessions
 * use the same routes as a plan's. `back` is where the title links to.
 */
export default function PlanEditor({
  plan: initialPlan,
  today,
  foodNames,
  back,
}: {
  plan: MealPlanDetail;
  today: string;
  foodNames: Record<string, string>;
  back?: { href: string; label: string };
}) {
  const router = useRouter();
  const id = useId();
  const [plan, setPlan] = useState(initialPlan);
  const [name, setName] = useState(plan.name ?? "");
  const [description, setDescription] = useState(plan.description ?? "");
  const [startDate, setStartDate] = useState(plan.startDate ?? today);
  const [durationDays, setDurationDays] = useState(String(plan.durationDays));
  const [rules, setRules] = useState<RulesFormValues>(() =>
    rulesToForm(plan.rules, foodNames),
  );
  const [planErrors, setPlanErrors] = useState<PlanFormErrors>({});
  const [rulesErrors, setRulesErrors] = useState<RulesFormErrors>({});
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [dayIndex, setDayIndex] = useState(0);
  const [editing, setEditing] = useState<
    { kind: "new" } | { kind: "entry"; entry: PlanEntry } | null
  >(null);
  const [sessionEditing, setSessionEditing] = useState<
    { kind: "new" } | { kind: "session"; session: PrepSession } | null
  >(null);
  const day = plan.days.find((d) => d.dayIndex === dayIndex) ?? plan.days[0];
  const isTemplate = plan.kind === "Template";
  const planName = plan.name || (isTemplate ? "Template" : "Meal plan");
  const backLink = back ?? { href: `/meal-plan/${plan.id}`, label: planName };

  const reload = async () => {
    setPlan(await getMealPlan(plan.id));
    router.refresh();
  };

  const saveTemplate = async () => {
    const parsed = parseTemplateForm({ name, durationDays });
    const parsedRules = parseRules(rules);
    setPlanErrors(parsed.errors ?? {});
    setRulesErrors(parsedRules.errors ?? {});
    if (parsed.errors || parsedRules.errors) return;
    setSaving(true);
    setSaveError(null);
    try {
      await updateTemplate(plan.id, {
        name: parsed.template.name,
        description: description.trim() || null,
        durationDays: parsed.template.durationDays,
        rules: parsedRules.rules,
      });
      appToast.success("Template saved");
      await reload();
    } catch (error) {
      setSaveError(getErrorMessage(error, "Could not save the template"));
    } finally {
      setSaving(false);
    }
  };

  const savePlan = async (event: React.FormEvent) => {
    event.preventDefault();
    if (isTemplate) {
      await saveTemplate();
      return;
    }
    const parsedPlan = parsePlanForm({ name, startDate, durationDays });
    const parsedRules = parseRules(rules);
    setPlanErrors(parsedPlan.errors ?? {});
    setRulesErrors(parsedRules.errors ?? {});
    if (parsedPlan.errors || parsedRules.errors) return;
    setSaving(true);
    setSaveError(null);
    try {
      await updateMealPlan(plan.id, {
        name: parsedPlan.plan.name || null,
        description: description.trim() || null,
        startDate: parsedPlan.plan.startDate,
        durationDays: parsedPlan.plan.durationDays,
        rules: parsedRules.rules,
      });
      appToast.success("Plan saved");
      await reload();
    } catch (error) {
      setSaveError(getErrorMessage(error, "Could not save the plan"));
    } finally {
      setSaving(false);
    }
  };

  const submitEntry = async (draft: EntryDraft) => {
    const sortOrder =
      editing?.kind === "entry"
        ? editing.entry.sortOrder
        : (day?.slots.find((slot) => slot.slot === draft.slot)?.entries
            .length ?? 0);
    const request = toRequest(draft, sortOrder);
    if (typeof request === "string") throw new Error(request);
    if (editing?.kind === "entry")
      await updatePlanEntry(plan.id, editing.entry.id, request);
    else await addPlanEntry(plan.id, request);
    appToast.success(editing?.kind === "entry" ? "Entry saved" : "Entry added");
    setEditing(null);
    await reload();
  };

  const removeEntry = async (entry: PlanEntry) => {
    try {
      await removePlanEntry(plan.id, entry.id);
      appToast.success(`${entryTitle(entry)} removed`);
      await reload();
    } catch (error) {
      appToast.error(error, "Could not remove the entry");
    }
  };

  const submitSession = async (value: {
    dayIndex: number;
    title: string;
    notes: string;
  }) => {
    const body = {
      dayIndex: value.dayIndex,
      title: value.title.trim(),
      notes: value.notes.trim() || null,
    };
    if (sessionEditing?.kind === "session")
      await updatePrepSession(plan.id, sessionEditing.session.id, body);
    else await addPrepSession(plan.id, body);
    appToast.success("Session saved");
    setSessionEditing(null);
    await reload();
  };

  const removeSession = async (session: PrepSession) => {
    try {
      await removePrepSession(plan.id, session.id);
      appToast.success(`${session.title} removed`);
      await reload();
    } catch (error) {
      appToast.error(error, "Could not remove the session");
    }
  };

  const dayDate = day?.date ?? today;

  return (
    <div className="log-page space-y-10" data-testid="meal-plan-edit-page">
      <header className="log-page-header block!">
        <p className="text-sm">
          <Link href={backLink.href} className="text-link">
            {backLink.label}
          </Link>
        </p>
        <h1 className="mt-1">
          {isTemplate ? `Edit ${planName}` : "Edit plan"}
        </h1>
      </header>

      <form onSubmit={savePlan} className="space-y-6" noValidate>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
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
              aria-invalid={planErrors.name ? true : undefined}
            />
            {planErrors.name && (
              <p className="input-error mt-1 text-sm">{planErrors.name}</p>
            )}
          </div>
          <div>
            <label htmlFor={`${id}-description`} className="label">
              Description
            </label>
            <input
              id={`${id}-description`}
              type="text"
              className="input w-full"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </div>
          {!isTemplate && (
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
                <p className="input-error mt-1 text-sm">
                  {planErrors.startDate}
                </p>
              )}
            </div>
          )}
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
              className="input tabular-nums w-full"
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
        </div>
        <RulesFields
          values={rules}
          errors={rulesErrors}
          durationDays={Number(durationDays) || plan.durationDays}
          onChange={setRules}
        />
        {saveError && (
          <p role="alert" className="input-error text-sm">
            {saveError}
          </p>
        )}
        <button
          type="submit"
          className="btn-primary min-h-11 w-full sm:w-auto"
          disabled={saving}
        >
          {isTemplate ? "Save template" : "Save plan"}
        </button>
      </form>

      <section aria-labelledby="entries-title">
        <header className="log-section-header">
          <h2 id="entries-title">Entries</h2>
          <button
            type="button"
            className="text-link text-sm"
            onClick={() => setEditing({ kind: "new" })}
          >
            Add entry
          </button>
        </header>
        <div
          role="tablist"
          aria-label="Days"
          className="-mx-1 flex gap-1 overflow-x-auto border-b border-border px-1"
        >
          {plan.days.map((candidate) => (
            <button
              key={candidate.dayIndex}
              type="button"
              role="tab"
              aria-selected={candidate.dayIndex === dayIndex}
              className={`min-h-11 shrink-0 whitespace-nowrap border-b-2 px-3 text-sm ${candidate.dayIndex === dayIndex ? "border-foreground font-semibold" : "border-transparent text-muted-foreground"}`}
              onClick={() => setDayIndex(candidate.dayIndex)}
            >
              {dayLabel(candidate)}
            </button>
          ))}
        </div>
        {editing?.kind === "new" && (
          <EntryForm
            plan={plan}
            date={dayDate}
            title="New entry"
            initial={{
              dayIndex,
              slot: "DINNER",
              kind: "Recipe",
              item: null,
              amount: "",
              text: "",
              notes: "",
              prepSessionId: "",
            }}
            onSubmit={submitEntry}
            onCancel={() => setEditing(null)}
          />
        )}
        {day && day.entryCount === 0 && editing?.kind !== "new" && (
          <p className="log-empty">Nothing planned for this day.</p>
        )}
        {day?.slots.map((slot) => (
          <div className="mt-6" key={slot.slot}>
            <div className="meal-group-header">
              <h3>{slotLabel(slot.slot)}</h3>
            </div>
            <ul>
              {slot.entries.map((entry) =>
                editing?.kind === "entry" && editing.entry.id === entry.id ? (
                  <li key={entry.id}>
                    <EntryForm
                      plan={plan}
                      date={dayDate}
                      title={`Edit ${entryTitle(entry)}`}
                      initial={draftFrom(entry)}
                      onSubmit={submitEntry}
                      onCancel={() => setEditing(null)}
                    />
                  </li>
                ) : (
                  <li key={entry.id} className="ledger-row">
                    <span className="min-w-0 flex-1">
                      <span className="block font-medium break-words">
                        {entryTitle(entry)}
                      </span>
                      <span className="mt-0.5 block text-xs text-muted-foreground">
                        {[
                          entryQuantity(entry),
                          entry.nutrition
                            ? `${Math.round(entry.nutrition.calories)} kcal`
                            : null,
                          entry.notes,
                        ]
                          .filter(Boolean)
                          .join(" · ")}
                      </span>
                    </span>
                    <button
                      type="button"
                      className="btn-ghost btn-sm min-h-9"
                      aria-label={`Edit ${entryTitle(entry)}`}
                      onClick={() => setEditing({ kind: "entry", entry })}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className="btn-ghost-destructive btn-sm min-h-9"
                      aria-label={`Remove ${entryTitle(entry)}`}
                      onClick={() => removeEntry(entry)}
                    >
                      Remove
                    </button>
                  </li>
                ),
              )}
            </ul>
          </div>
        ))}
      </section>

      <section aria-labelledby="sessions-title">
        <header className="log-section-header">
          <h2 id="sessions-title">Cooking sessions</h2>
          <button
            type="button"
            className="text-link text-sm"
            onClick={() => setSessionEditing({ kind: "new" })}
          >
            Add session
          </button>
        </header>
        {sessionEditing?.kind === "new" && (
          <SessionForm
            plan={plan}
            title="New cooking session"
            initial={{ dayIndex, title: "", notes: "" }}
            onSubmit={submitSession}
            onCancel={() => setSessionEditing(null)}
          />
        )}
        {plan.prepSessions.length === 0 && sessionEditing?.kind !== "new" && (
          <p className="log-empty">
            No cooking sessions. Add one to group entries you cook at once.
          </p>
        )}
        <ul>
          {plan.prepSessions.map((session) =>
            sessionEditing?.kind === "session" &&
            sessionEditing.session.id === session.id ? (
              <li key={session.id}>
                <SessionForm
                  plan={plan}
                  title={`Edit ${session.title}`}
                  initial={{
                    dayIndex: session.dayIndex,
                    title: session.title,
                    notes: session.notes ?? "",
                  }}
                  onSubmit={submitSession}
                  onCancel={() => setSessionEditing(null)}
                />
              </li>
            ) : (
              <li key={session.id} className="ledger-row">
                <span className="min-w-0 flex-1">
                  <span className="block font-medium">{session.title}</span>
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {session.date
                      ? formatPlanDate(session.date, {
                          weekday: "short",
                          day: "numeric",
                          month: "short",
                        })
                      : `Day ${session.dayIndex + 1}`}{" "}
                    · {session.entryCount}{" "}
                    {session.entryCount === 1 ? "entry" : "entries"}
                  </span>
                </span>
                <button
                  type="button"
                  className="btn-ghost btn-sm min-h-9"
                  aria-label={`Edit ${session.title}`}
                  onClick={() =>
                    setSessionEditing({ kind: "session", session })
                  }
                >
                  Edit
                </button>
                <button
                  type="button"
                  className="btn-ghost-destructive btn-sm min-h-9"
                  aria-label={`Remove ${session.title}`}
                  onClick={() => removeSession(session)}
                >
                  Remove
                </button>
              </li>
            ),
          )}
        </ul>
      </section>
    </div>
  );
}
