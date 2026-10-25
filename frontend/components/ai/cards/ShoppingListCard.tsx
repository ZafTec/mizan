import type { ShoppingListCardData } from "@/lib/api/ai";
import { itemCount } from "@/lib/prices/format";
import CardFrame from "./CardFrame";

/** `shopping_list`: a list made from a plan. */
export default function ShoppingListCard({
  data,
}: {
  data: ShoppingListCardData;
}) {
  return (
    <CardFrame
      caption="Shopping list"
      label="Shopping list"
      href={`/shopping-lists/${data.listId}`}
      linkText="Open the list"
    >
      <p className="num">
        {itemCount(data.itemCount)}
        {data.needsReviewCount > 0 && (
          <span className="log-muted"> · {data.needsReviewCount} to check</span>
        )}
      </p>
    </CardFrame>
  );
}
