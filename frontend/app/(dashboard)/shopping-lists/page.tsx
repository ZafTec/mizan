import { redirect } from "next/navigation";

/** The lists index lives under More → Food; this keeps the shorter address working. */
export default function ShoppingListsIndex() {
  redirect("/meal-plan/shopping-list");
}
