"use client";

import { useState } from "react";
import Link from "next/link";
import type { GeneratedPlan } from "@/lib/api/mealPlans";
import { DRAFTED_BY_ASSISTANT } from "@/lib/planning/draft";
import { planDates, slotLabel } from "@/lib/planning/rules";
import { dayLabel } from "./PlanDetail";
import PlanCostTable from "./PlanCostTable";
import PlanDayEntries from "./PlanDayEntries";
import { ConfirmDialog } from "@/components/ConfirmDialog";

/**
 * One draft the assistant made, as the generate call answered it: the days
 * and what is in them, what breaks the rules, the lines it could not turn
 * into a food, and the cost table. The draft is already saved; **Keep** opens
 * it, **Discard** declines it.
 */
export default function DraftPreview({
  draft,
  headingRef,
  onDiscard,
}: {
  draft: GeneratedPlan;
  headingRef?: React.Ref<HTMLHeadingElement>;
  onDiscard: (planId: string) => Promise<void>;
}) {
  const [confirming, setConfirming] = useState(false);
  const { plan } = draft;
  const name = plan.name || "Meal plan";
  const headingId = `draft-${plan.id}`;
  const dayOf = (index: number) => {
    const day = plan.days.find((candidate) => candidate.dayIndex === index);
    return day ? dayLabel(day) : `Day ${index + 1}`;
  };

  return (
    <article
      aria-labelledby={headingId}
      className="space-y-6"
      data-testid="draft-preview"
    >
      <header>
        <h2
          id={headingId}
          ref={headingRef}
          tabIndex={-1}
          className="text-xl font-semibold focus-visible:outline-none"
        >
          {name}
        </h2>
        <p className="text-muted-foreground mt-1 text-sm" data-testid="draft-facts">
          {["Draft", planDates(plan), DRAFTED_BY_ASSISTANT].join(" · ")}
        </p>
        {draft.note && <p className="mt-2 text-sm">{draft.note}</p>}
      </header>

      <div className="space-y-4">
        {plan.days.map((day) => (
          <PlanDayEntries key={day.dayIndex} day={day} label={dayLabel(day)} />
        ))}
      </div>

      {draft.violations.length > 0 && (
        <section aria-label="Rule breaks">
          <header className="log-section-header">
            <h3>Rule breaks</h3>
          </header>
          <ul>
            {draft.violations.map((violation, index) => (
              <li
                key={`${violation.rule}-${index}`}
                className="ledger-row min-h-0! text-sm"
              >
                <span className="min-w-0 flex-1">{violation.message}</span>
                <span className="text-xs text-muted-foreground">{violation.rule}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {draft.unresolved.length > 0 && (
        <section aria-label="To review">
          <header className="log-section-header">
            <h3>To review</h3>
          </header>
          <ul>
            {draft.unresolved.map((line, index) => (
              <li
                key={`${line.dayIndex}-${line.slot}-${index}`}
                className="ledger-row min-h-0! text-sm"
              >
                <span className="min-w-0 flex-1">
                  <span className="block break-words">{line.text}</span>
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {[
                      dayOf(line.dayIndex),
                      slotLabel(line.slot),
                      line.reason,
                    ].join(" · ")}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <PlanCostTable cost={draft.cost} headingLevel={3} />

      <div className="flex flex-wrap gap-3">
        <Link
          href={`/meal-plan/${plan.id}`}
          className="btn-primary min-h-11 flex-1 sm:flex-none"
          aria-label={`Keep ${name}`}
        >
          Keep
        </Link>
        <button
          type="button"
          className="btn-ghost-destructive min-h-11 flex-1 sm:flex-none"
          aria-label={`Discard ${name}`}
          onClick={() => setConfirming(true)}
        >
          Discard
        </button>
      </div>
      <ConfirmDialog
        open={confirming}
        onClose={() => setConfirming(false)}
        title={`Discard ${name}?`}
        description="The draft is declined and kept under past plans. Nothing is added to your week."
        confirmLabel="Discard"
        pendingLabel="Discarding…"
        cancelLabel="Keep draft"
        errorFallback="Could not discard the draft."
        onConfirm={() => onDiscard(plan.id)}
      />
    </article>
  );
}
