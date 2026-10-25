import type {
  AiCard,
  CostTableCardData,
  DayPlanCardData,
  PlanPreviewCardData,
  ShoppingListCardData,
} from "@/lib/api/ai";

/**
 * A chat card with its data typed by its `type`. The API builds every card
 * from a command's result and checks it against its record before sending
 * it (docs/AI.md#planning-tools-and-typed-cards), so this only narrows the
 * generated `{ type, data }`; a type this build does not know is left out.
 */
export type TypedCard =
  | { type: "plan_preview"; data: PlanPreviewCardData }
  | { type: "day_plan"; data: DayPlanCardData }
  | { type: "cost_table"; data: CostTableCardData }
  | { type: "shopping_list"; data: ShoppingListCardData };

export const CARD_TYPES: readonly TypedCard["type"][] = [
  "plan_preview",
  "day_plan",
  "cost_table",
  "shopping_list",
];

export function typedCard(card: AiCard): TypedCard | null {
  if (typeof card.data !== "object" || card.data === null) return null;
  if (!(CARD_TYPES as readonly string[]).includes(card.type)) return null;
  return card as TypedCard;
}
