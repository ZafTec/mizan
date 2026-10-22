import { notFound } from "next/navigation";
import { getShoppingListServer } from "@/lib/api/shoppingLists.server";
import ShoppingListView from "@/components/planning/ShoppingListView";

export const dynamic = "force-dynamic";

export default async function ShoppingListPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const list = await getShoppingListServer(id);
  if (!list) notFound();
  return <ShoppingListView list={list} />;
}
