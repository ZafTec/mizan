"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import * as TabsPrimitive from "@radix-ui/react-tabs";
import {
  deleteMealPlan,
  generateShoppingList,
  setMealPlanStatus,
  type MealPlanDetail,
  type PlanDay,
  type PlanEntry,
  type PlanRules,
} from "@/lib/api/mealPlans";
import {
  dayIndexFor,
  entryQuantity,
  entryTitle,
  formatPlanDate,
  planDates,
  slotLabel,
  STATUS_LABELS,
} from "@/lib/planning/rules";
import { appToast } from "@/lib/toast";
import { DeleteConfirmModal } from "@/components/DeleteConfirmModal";
import DraftActions from "./DraftActions";
import LogPlanEntryButton from "./LogPlanEntryButton";

const SLOT_ORDER = ["BREAKFAST", "LUNCH", "DINNER", "SNACK"];

function sortedSlots(day: PlanDay) {
  return [...day.slots].sort((a, b) => {
    const indexA = SLOT_ORDER.indexOf(a.slot.toUpperCase());
    const indexB = SLOT_ORDER.indexOf(b.slot.toUpperCase());
    return (indexA < 0 ? 99 : indexA) - (indexB < 0 ? 99 : indexB);
  });
}

export function dayLabel(day: PlanDay): string {
  return day.date
    ? formatPlanDate(day.date, { weekday: "short", day: "numeric" })
    : `Day ${day.dayIndex + 1}`;
}

/** The day's totals against the targets the rules set. Carbs is a ceiling, the rest are targets. */
export function DayTotals({ day, rules }: { day: PlanDay; rules: PlanRules }) {
  const rows = [
    {
      name: "Calories",
      value: day.totals.calories,
      target: rules.targetCalories,
      unit: "kcal",
      ceiling: false,
    },
    {
      name: "Protein",
      value: day.totals.proteinGrams,
      target: rules.targetProtein,
      unit: "g",
      ceiling: false,
    },
    {
      name: "Carbs",
      value: day.totals.carbsGrams,
      target: rules.maxCarbs,
      unit: "g",
      ceiling: true,
    },
    {
      name: "Fat",
      value: day.totals.fatGrams,
      target: rules.targetFat,
      unit: "g",
      ceiling: false,
    },
  ];
  return (
    <section aria-label="Day totals" className="nutrition-strip">
      {rows.map((row) => (
        <div className="nutrition-readout" key={row.name}>
          <p className="text-sm log-muted">{row.name}</p>
          <p className="num nutrition-value">
            {Math.round(row.value).toLocaleString()}
            <span className="text-sm font-normal log-muted">
              {" "}
              {row.target != null
                ? `${row.ceiling ? "of max" : "/"} ${Math.round(row.target).toLocaleString()} `
                : " "}
              {row.unit}
            </span>
          </p>
        </div>
      ))}
    </section>
  );
}

function EntryRow({
  planId,
  entry,
  sessionTitle,
}: {
  planId: string;
  entry: PlanEntry;
  sessionTitle: string | null;
}) {
  const quantity = entryQuantity(entry);
  const details = [
    quantity,
    entry.nutrition
      ? `${Math.round(entry.nutrition.proteinGrams)} g protein`
      : entry.kind === "Text"
        ? "No nutrition"
        : null,
    sessionTitle ? `Cook: ${sessionTitle}` : null,
  ].filter(Boolean);
  return (
    <li className="ledger-row">
      <span className="min-w-0 flex-1">
        <span className="block font-medium break-words">
          {entryTitle(entry)}
        </span>
        {details.length > 0 && (
          <span className="mt-0.5 block text-xs log-muted">
            {details.join(" · ")}
          </span>
        )}
        {entry.notes && (
          <span className="mt-1 block text-sm break-words">{entry.notes}</span>
        )}
      </span>
      {entry.nutrition && (
        <span className="num whitespace-nowrap text-sm">
          {Math.round(entry.nutrition.calories).toLocaleString()}{" "}
          <span className="text-xs log-muted">kcal</span>
        </span>
      )}
      <LogPlanEntryButton planId={planId} entry={entry} />
    </li>
  );
}

export default function PlanDetail({
  plan,
  today,
  viewerId,
}: {
  plan: MealPlanDetail;
  today: string;
  viewerId: string;
}) {
  const router = useRouter();
  const [dayIndex, setDayIndex] = useState(() =>
    String(dayIndexFor(plan, today)),
  );
  const [busy, setBusy] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const name = plan.name || "Meal plan";
  const sessions = new Map(
    plan.prepSessions.map((session) => [session.id, session.title]),
  );
  const fromCoach =
    plan.authoredByUserId != null && plan.authoredByUserId !== viewerId;
  const canAnswerDraft = plan.status === "Draft" && fromCoach;
  const facts = [
    STATUS_LABELS[plan.status] ?? plan.status,
    planDates(plan),
    fromCoach ? `From ${plan.authoredByName ?? "your coach"}` : null,
    plan.householdId ? "Household plan" : null,
  ].filter(Boolean);
  const planViolations = plan.violations.filter((v) => v.dayIndex == null);

  const changeStatus = async (status: "Active" | "Archived") => {
    setBusy("status");
    try {
      await setMealPlanStatus(plan.id, status);
      appToast.success(
        status === "Active" ? `${name} is active` : `${name} archived`,
      );
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not change the plan");
    } finally {
      setBusy(null);
    }
  };

  const makeList = async () => {
    setBusy("list");
    try {
      const result = await generateShoppingList(plan.id, {
        name: `${name} shopping`,
      });
      appToast.success(
        `${result.itemCount} items${result.needsReviewCount ? `, ${result.needsReviewCount} to check` : ""}`,
      );
      router.push(`/shopping-lists/${result.listId}`);
    } catch (error) {
      appToast.error(error, "Could not make the shopping list");
      setBusy(null);
    }
  };

  const remove = async () => {
    try {
      await deleteMealPlan(plan.id);
      appToast.success(`${name} deleted`);
      router.push("/meal-plan");
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not delete the plan");
    }
  };

  return (
    <div className="log-page space-y-8" data-testid="meal-plan-detail-page">
      <header className="log-page-header block!">
        <p className="text-sm">
          <Link href="/meal-plan" className="text-link">
            Meal plans
          </Link>
        </p>
        <h1 className="mt-1">{name}</h1>
        <p className="log-muted mt-2" data-testid="plan-facts">
          {facts.join(" · ")}
        </p>
        {plan.description && <p className="mt-2 text-sm">{plan.description}</p>}
        <div className="mt-4 flex flex-wrap items-center gap-2">
          {canAnswerDraft && <DraftActions planId={plan.id} planName={name} />}
          {plan.canEdit && (
            <>
              <Link
                href={`/meal-plan/${plan.id}/edit`}
                className="btn-secondary btn-sm min-h-9"
              >
                Edit
              </Link>
              {plan.status !== "Draft" && (
                <button
                  type="button"
                  className="btn-ghost btn-sm min-h-9"
                  disabled={busy !== null}
                  onClick={() =>
                    changeStatus(
                      plan.status === "Active" ? "Archived" : "Active",
                    )
                  }
                >
                  {plan.status === "Active" ? "Archive" : "Activate"}
                </button>
              )}
              <button
                type="button"
                className="btn-ghost btn-sm min-h-9 text-destructive"
                disabled={busy !== null}
                onClick={() => setConfirmDelete(true)}
              >
                Delete
              </button>
            </>
          )}
        </div>
      </header>

      <TabsPrimitive.Root value={dayIndex} onValueChange={setDayIndex}>
        <TabsPrimitive.List
          aria-label="Days"
          className="period-tabs -mx-1 overflow-x-auto px-1"
        >
          {plan.days.map((day) => (
            <TabsPrimitive.Trigger
              key={day.dayIndex}
              value={String(day.dayIndex)}
              className="flex min-h-11 shrink-0 items-center gap-1 whitespace-nowrap border-b-2 border-transparent px-3 pb-3 text-sm log-muted data-[state=active]:border-foreground data-[state=active]:font-semibold data-[state=active]:text-foreground"
              aria-label={`${dayLabel(day)}${day.date === today ? ", today" : ""}`}
            >
              {dayLabel(day)}
              {day.date === today && (
                <span aria-hidden="true" className="text-xs">
                  · today
                </span>
              )}
            </TabsPrimitive.Trigger>
          ))}
        </TabsPrimitive.List>
        {plan.days.map((day) => (
          <TabsPrimitive.Content
            key={day.dayIndex}
            value={String(day.dayIndex)}
            className="space-y-8 focus-visible:outline-none"
          >
            <DayTotals day={day} rules={plan.rules} />
            {day.entryCount === 0 ? (
              <p className="log-empty">Nothing planned for this day.</p>
            ) : (
              sortedSlots(day).map((slot) => (
                <div className="meal-group" key={slot.slot}>
                  <div className="meal-group-header">
                    <h3>{slotLabel(slot.slot)}</h3>
                    <span className="num text-xs log-muted">
                      {Math.round(
                        slot.entries.reduce(
                          (sum, entry) =>
                            sum + (entry.nutrition?.calories ?? 0),
                          0,
                        ),
                      ).toLocaleString()}{" "}
                      kcal
                    </span>
                  </div>
                  <ul>
                    {slot.entries.map((entry) => (
                      <EntryRow
                        key={entry.id}
                        planId={plan.id}
                        entry={entry}
                        sessionTitle={
                          entry.prepSessionId
                            ? (sessions.get(entry.prepSessionId) ?? null)
                            : null
                        }
                      />
                    ))}
                  </ul>
                </div>
              ))
            )}
            {(day.violations.length > 0 || planViolations.length > 0) && (
              <section aria-labelledby="violations-title">
                <header className="log-section-header">
                  <h2 id="violations-title">Rule breaks</h2>
                </header>
                <ul>
                  {[...planViolations, ...day.violations].map(
                    (violation, index) => (
                      <li
                        key={`${violation.rule}-${index}`}
                        className="ledger-row text-sm"
                      >
                        <span className="min-w-0 flex-1">
                          {violation.slot && (
                            <span className="log-muted">
                              {slotLabel(violation.slot)} ·{" "}
                            </span>
                          )}
                          {violation.message}
                        </span>
                        <span className="text-xs log-muted">
                          {violation.rule}
                        </span>
                      </li>
                    ),
                  )}
                </ul>
              </section>
            )}
          </TabsPrimitive.Content>
        ))}
      </TabsPrimitive.Root>

      {plan.prepSessions.length > 0 && (
        <section aria-labelledby="prep-title">
          <header className="log-section-header">
            <h2 id="prep-title">Cooking sessions</h2>
          </header>
          <ul>
            {plan.prepSessions.map((session) => (
              <li key={session.id} className="ledger-row">
                <span className="min-w-0 flex-1">
                  <span className="block font-medium">{session.title}</span>
                  <span className="mt-0.5 block text-xs log-muted">
                    {session.date
                      ? formatPlanDate(session.date, {
                          weekday: "long",
                          day: "numeric",
                          month: "short",
                        })
                      : `Day ${session.dayIndex + 1}`}
                    {session.notes ? ` · ${session.notes}` : ""}
                  </span>
                </span>
                <span className="num whitespace-nowrap text-sm log-muted">
                  {session.entryCount}{" "}
                  {session.entryCount === 1 ? "entry" : "entries"}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <button
        type="button"
        className="btn-primary min-h-11 w-full sm:w-auto"
        disabled={busy !== null || plan.entryCount === 0}
        onClick={makeList}
      >
        Make shopping list
      </button>

      <DeleteConfirmModal
        isOpen={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        onConfirm={remove}
        itemName={name}
      />
    </div>
  );
}
