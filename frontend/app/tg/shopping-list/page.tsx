import MiniAppGate from "@/components/tg/MiniAppGate";
import MiniAppShoppingList from "@/components/tg/MiniAppShoppingList";

type Search = Promise<Record<string, string | string[] | undefined>>;

const one = (value: string | string[] | undefined) =>
  Array.isArray(value) ? value[0] : value || undefined;

/** The /shop card's Open button: /tg/shopping-list?listId=.... */
export default async function MiniAppShoppingListPage({
  searchParams,
}: {
  searchParams: Search;
}) {
  const params = await searchParams;
  return (
    <MiniAppGate>
      <MiniAppShoppingList listId={one(params.listId)} />
    </MiniAppGate>
  );
}
