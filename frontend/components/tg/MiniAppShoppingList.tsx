"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ApiError } from "@/lib/api";
import {
  getShoppingList,
  listShoppingLists,
  type ShoppingListDetail,
} from "@/lib/api/shoppingLists";
import { getErrorMessage } from "@/lib/toast";
import ShoppingListView from "@/components/planning/ShoppingListView";
import { preload, preloaded, remember } from "@/lib/tg/preload";
import { useBackButton } from "@/lib/tg/useBackButton";

type Loaded =
  | { kind: "list"; list: ShoppingListDetail }
  | { kind: "message"; message: string; lists: boolean };

async function load(listId?: string): Promise<Loaded> {
  try {
    let id = listId;
    if (!id) {
      // The newest list, as /shop picks it.
      const page = await listShoppingLists(1);
      id = page.items[0]?.id;
      if (!id)
        return {
          kind: "message",
          message: "No shopping list yet.",
          lists: true,
        };
    }
    return { kind: "list", list: await getShoppingList(id) };
  } catch (error) {
    if (error instanceof ApiError && error.status === 404)
      return {
        kind: "message",
        message: "This list is not available any more.",
        lists: true,
      };
    return {
      kind: "message",
      message: getErrorMessage(error, "Could not load the list."),
      lists: false,
    };
  }
}

const key = (listId?: string) => `list:${listId ?? ""}`;

/** Starts loading the newest list, as the home page's "Shopping list" row opens it. */
export const preloadMiniAppList = () => preload(key(), () => load());

/** A shopping list in the Mini App: /tg/shopping-list?listId=, or the newest one. */
export default function MiniAppShoppingList({ listId }: { listId?: string }) {
  useBackButton();
  const [loaded, setLoaded] = useState<Loaded | null>(() =>
    preloaded<Loaded>(key(listId)),
  );

  useEffect(() => {
    let cancelled = false;
    void load(listId).then((next) => {
      remember(key(listId), next);
      if (!cancelled) setLoaded(next);
    });
    return () => {
      cancelled = true;
    };
  }, [listId]);

  if (!loaded)
    return (
      <p role="status" className="text-muted-foreground">
        Loading the list…
      </p>
    );
  if (loaded.kind === "message")
    return (
      <section className="log-page space-y-4">
        <p className="text-base">{loaded.message}</p>
        {loaded.lists && (
          <Link
            href="/meal-plan/shopping-list"
            className="btn-secondary min-h-11"
          >
            Shopping lists
          </Link>
        )}
      </section>
    );
  // The view keeps its own copy of the list; a newer copy than the preloaded one replaces it.
  return (
    <ShoppingListView
      key={`${loaded.list.id}:${loaded.list.updatedAt}:${loaded.list.checkedItems}:${loaded.list.totalItems}`}
      list={loaded.list}
      embedded
    />
  );
}
