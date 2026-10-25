import type { PlanDay } from "@/lib/api/mealPlans";
import { entryQuantity, entryTitle, slotLabel } from "@/lib/planning/rules";

const SLOT_ORDER = ["BREAKFAST", "LUNCH", "DINNER", "SNACK"];

function slotRank(slot: string) {
  const index = SLOT_ORDER.indexOf(slot.toUpperCase());
  return index < 0 ? SLOT_ORDER.length : index;
}

/**
 * A day's entries in slot order, read only: what a draft preview and a chat
 * card show of a day. The calories are the API's day totals.
 */
export default function PlanDayEntries({
  day,
  label,
  headingLevel = 3,
}: {
  day: PlanDay;
  label: string;
  headingLevel?: 3 | 4 | 5;
}) {
  const Heading = `h${headingLevel}` as const;
  const slots = [...day.slots].sort(
    (a, b) => slotRank(a.slot) - slotRank(b.slot),
  );
  return (
    <section aria-label={label}>
      <header className="log-section-header">
        <Heading>{label}</Heading>
        <span className="num text-sm log-muted">
          {Math.round(day.totals.calories).toLocaleString()} kcal
        </span>
      </header>
      {day.entryCount === 0 ? (
        <p className="py-3 text-sm log-muted">Nothing planned.</p>
      ) : (
        <ul>
          {slots.flatMap((slot) =>
            slot.entries.map((entry) => {
              const quantity = entryQuantity(entry);
              return (
                <li key={entry.id} className="ledger-row min-h-0! text-sm">
                  <span className="w-20 shrink-0 text-xs log-muted">
                    {slotLabel(slot.slot)}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block break-words">
                      {entryTitle(entry)}
                    </span>
                    {entry.notes && (
                      <span className="mt-0.5 block text-xs break-words log-muted">
                        {entry.notes}
                      </span>
                    )}
                  </span>
                  {quantity && (
                    <span className="num whitespace-nowrap text-xs log-muted">
                      {quantity}
                    </span>
                  )}
                </li>
              );
            }),
          )}
        </ul>
      )}
    </section>
  );
}
