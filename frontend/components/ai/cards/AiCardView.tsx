import type { AiCard } from "@/lib/api/ai";
import { typedCard } from "@/lib/ai/cards";
import CostTableCard from "./CostTableCard";
import DayPlanCard from "./DayPlanCard";
import PlanPreviewCard from "./PlanPreviewCard";
import ShoppingListCard from "./ShoppingListCard";

/**
 * One fixed component per card type, rendered from the card's typed data.
 * Nothing a model writes becomes markup here; a type this build does not know
 * is left out and the reply's text stands on its own (docs/AI.md).
 */
export function AiCardView({ card }: { card: AiCard }) {
  const typed = typedCard(card);
  switch (typed?.type) {
    case "plan_preview":
      return <PlanPreviewCard data={typed.data} />;
    case "day_plan":
      return <DayPlanCard data={typed.data} />;
    case "cost_table":
      return <CostTableCard data={typed.data} />;
    case "shopping_list":
      return <ShoppingListCard data={typed.data} />;
    default:
      return null;
  }
}

export default function AiCardList({ cards }: { cards: AiCard[] }) {
  if (cards.length === 0) return null;
  return (
    <div className="flex w-full max-w-[92%] flex-col gap-2 sm:max-w-[80%]">
      {cards.map((card, index) => (
        <AiCardView key={`${card.type}-${index}`} card={card} />
      ))}
    </div>
  );
}
