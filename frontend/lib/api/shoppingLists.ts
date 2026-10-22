import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";

/**
 * Browser calls for shopping lists and their items. Types come from the
 * generated OpenAPI document. The toggle is the one write a person repeats
 * in a shop aisle, so it carries an Idempotency-Key.
 */
type Schemas = components["schemas"];

export type ShoppingListSummary = Schemas["ShoppingListSummaryDto"];
export type ShoppingListDetail = Schemas["ShoppingListDetailDto"];
export type ShoppingListItem = Schemas["ShoppingListItemDto"];
export type ShoppingListPage = Schemas["ShoppingListSummaryDtoPagedResult"];
export type AddShoppingListItemRequest = Schemas["AddShoppingListItemRequest"];
export type UpdateShoppingListItemRequest =
  Schemas["UpdateShoppingListItemRequest"];
export type ClearCheckedResult = Schemas["ClearCheckedResult"];

export const listShoppingLists = (pageSize = 50) =>
  clientApi<ShoppingListPage>(`/api/ShoppingLists?PageSize=${pageSize}`);

export const getShoppingList = (id: string) =>
  clientApi<ShoppingListDetail>(`/api/ShoppingLists/${id}`);

/** Answers with the new list's id. */
export const createShoppingList = (
  body: Schemas["CreateShoppingListRequest"],
) => clientApi<string>("/api/ShoppingLists", { method: "POST", body });

export const renameShoppingList = (id: string, name: string) =>
  clientApi<void>(`/api/ShoppingLists/${id}`, {
    method: "PUT",
    body: { name } satisfies Schemas["UpdateShoppingListRequest"],
  });

export const deleteShoppingList = (id: string) =>
  clientApi<void>(`/api/ShoppingLists/${id}`, { method: "DELETE" });

/** Answers with the new item's id. */
export const addShoppingListItem = (
  listId: string,
  body: AddShoppingListItemRequest,
) =>
  clientApi<string>(`/api/ShoppingLists/${listId}/items`, {
    method: "POST",
    body,
  });

export const updateShoppingListItem = (
  itemId: string,
  body: UpdateShoppingListItemRequest,
) =>
  clientApi<void>(`/api/ShoppingLists/items/${itemId}`, {
    method: "PUT",
    body,
  });

export const deleteShoppingListItem = (itemId: string) =>
  clientApi<void>(`/api/ShoppingLists/items/${itemId}`, { method: "DELETE" });

export const toggleShoppingListItem = (itemId: string, isChecked: boolean) =>
  clientApi<void>(`/api/ShoppingLists/items/${itemId}/toggle`, {
    method: "PATCH",
    body: { isChecked } satisfies Schemas["ToggleShoppingListItemRequest"],
    headers: { "Idempotency-Key": crypto.randomUUID() },
  });

export const clearCheckedItems = (listId: string) =>
  clientApi<ClearCheckedResult>(`/api/ShoppingLists/${listId}/clear-checked`, {
    method: "POST",
  });

/** "500 g" when the weight is known, otherwise the amount and unit as written. */
export function formatAmount(item: ShoppingListItem): string {
  if (item.grams != null)
    return `${Math.round(item.grams * 10) / 10 === Math.round(item.grams) ? Math.round(item.grams) : Math.round(item.grams * 10) / 10} g`;
  if (item.amount != null)
    return `${item.amount.toLocaleString()}${item.unit ? ` ${item.unit}` : ""}`;
  return item.unit ?? "";
}
