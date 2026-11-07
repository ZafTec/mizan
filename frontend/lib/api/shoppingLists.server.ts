import { ApiError } from "@/lib/api";
import { serverApi } from "@/lib/api.server";
import type { ShoppingListDetail, ShoppingListPage } from "./shoppingLists";

/** Server-component reads for the shopping list pages, typed from the OpenAPI document. */
export const listShoppingListsServer = (pageSize = 50) =>
  serverApi<ShoppingListPage>(`/api/ShoppingLists?PageSize=${pageSize}`);

/** Null for a list the viewer cannot open; the page turns that into 404. */
export async function getShoppingListServer(
  id: string,
): Promise<ShoppingListDetail | null> {
  try {
    return await serverApi<ShoppingListDetail>(`/api/ShoppingLists/${id}`, {
      expectedStatuses: [404],
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}
