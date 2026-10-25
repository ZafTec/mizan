"use client";

import { useEffect, useId, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  addShoppingListItem,
  clearCheckedItems,
  deleteShoppingList,
  deleteShoppingListItem,
  formatAmount,
  getShoppingList,
  renameShoppingList,
  toggleShoppingListItem,
  updateShoppingListItem,
  type ShoppingListDetail,
  type ShoppingListItem,
} from "@/lib/api/shoppingLists";
import { confidenceLabel, formatMoney, itemCount } from "@/lib/prices/format";
import { appToast, getErrorMessage } from "@/lib/toast";
import { DeleteConfirmModal } from "@/components/DeleteConfirmModal";

type Group = { key: string; title: string; items: ShoppingListItem[] };

/** Items by cooking session when the list came from a batch plan, else by category. */
export function groupItems(items: ShoppingListItem[]): Group[] {
  const bySession = items.some((item) => item.prepSessionId);
  const groups = new Map<string, Group>();
  for (const item of [...items].sort((a, b) => a.sortOrder - b.sortOrder)) {
    const key = bySession ? (item.prepSessionId ?? "") : (item.category ?? "");
    const title = bySession
      ? (item.prepSessionTitle ?? "Any time")
      : (item.category ?? "Other");
    const group = groups.get(key) ?? { key, title, items: [] };
    group.items.push(item);
    groups.set(key, group);
  }
  return [...groups.values()];
}

function ItemForm({
  initial,
  title,
  onSubmit,
  onCancel,
}: {
  initial: { name: string; amount: string; unit: string };
  title: string;
  onSubmit: (value: {
    name: string;
    amount: string;
    unit: string;
  }) => Promise<void>;
  onCancel: () => void;
}) {
  const id = useId();
  const [value, setValue] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  return (
    <form
      aria-label={title}
      className="flex flex-wrap items-end gap-3 border-b border-border py-4"
      onSubmit={async (event) => {
        event.preventDefault();
        if (!value.name.trim()) {
          setError("Name the item.");
          return;
        }
        if (value.amount && !(Number(value.amount) > 0)) {
          setError("Amount must be above zero.");
          return;
        }
        setBusy(true);
        setError(null);
        try {
          await onSubmit(value);
        } catch (caught) {
          setError(getErrorMessage(caught, "Could not save the item"));
          setBusy(false);
        }
      }}
    >
      <div className="min-w-0 flex-1 basis-40">
        <label htmlFor={`${id}-name`} className="label">
          Item
        </label>
        <input
          id={`${id}-name`}
          type="text"
          className="input w-full"
          value={value.name}
          onChange={(event) => setValue({ ...value, name: event.target.value })}
          placeholder="Tomatoes"
        />
      </div>
      <div className="w-24">
        <label htmlFor={`${id}-amount`} className="label">
          Amount
        </label>
        <input
          id={`${id}-amount`}
          type="number"
          inputMode="decimal"
          min={0}
          step="any"
          className="input num w-full"
          value={value.amount}
          onChange={(event) =>
            setValue({ ...value, amount: event.target.value })
          }
        />
      </div>
      <div className="w-24">
        <label htmlFor={`${id}-unit`} className="label">
          Unit
        </label>
        <input
          id={`${id}-unit`}
          type="text"
          className="input w-full"
          value={value.unit}
          onChange={(event) => setValue({ ...value, unit: event.target.value })}
          placeholder="g"
        />
      </div>
      <div className="flex gap-2">
        <button
          type="submit"
          className="btn-primary btn-sm min-h-11"
          disabled={busy}
        >
          Save item
        </button>
        <button
          type="button"
          className="btn-ghost btn-sm min-h-11"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
      {error && (
        <p role="alert" className="input-error basis-full text-sm">
          {error}
        </p>
      )}
    </form>
  );
}

/**
 * One shopping list. Ticks are optimistic and revert only when the API
 * refuses. Every other write reads the list back so counts stay the API's.
 */
export default function ShoppingListView({
  list: initialList,
}: {
  list: ShoppingListDetail;
}) {
  const router = useRouter();
  const id = useId();
  const [list, setList] = useState(initialList);
  const [renaming, setRenaming] = useState(false);
  const [name, setName] = useState(initialList.name ?? "");
  const [adding, setAdding] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [busy, setBusy] = useState(false);
  // Ticks before hydration would be lost; the page says when it listens.
  const [hydrated, setHydrated] = useState(false);
  useEffect(() => setHydrated(true), []);
  const title = list.name || "Shopping list";
  const left = list.items.filter((item) => !item.isChecked).length;
  const checked = list.items.length - left;

  const reload = async () => {
    setList(await getShoppingList(list.id));
    router.refresh();
  };

  const toggle = async (item: ShoppingListItem) => {
    const next = !item.isChecked;
    const apply = (isChecked: boolean) =>
      setList((current) => ({
        ...current,
        items: current.items.map((candidate) =>
          candidate.id === item.id ? { ...candidate, isChecked } : candidate,
        ),
      }));
    apply(next);
    try {
      await toggleShoppingListItem(item.id, next);
    } catch (error) {
      apply(!next);
      appToast.error(error, "Could not update the item");
    }
  };

  const grams = (value: { amount: string; unit: string }) =>
    value.unit.trim().toLowerCase() === "g" && value.amount
      ? Number(value.amount)
      : null;

  const add = async (value: { name: string; amount: string; unit: string }) => {
    const weight = grams(value);
    await addShoppingListItem(list.id, {
      itemName: value.name.trim(),
      amount: weight === null && value.amount ? Number(value.amount) : null,
      unit: weight === null ? value.unit.trim() || null : null,
      grams: weight,
    });
    appToast.success(`${value.name.trim()} added`);
    setAdding(false);
    await reload();
  };

  const edit = async (
    item: ShoppingListItem,
    value: { name: string; amount: string; unit: string },
  ) => {
    const weight = grams(value);
    await updateShoppingListItem(item.id, {
      itemName: value.name.trim(),
      amount: weight === null && value.amount ? Number(value.amount) : null,
      unit: weight === null ? value.unit.trim() || null : null,
      grams: weight,
      needsReview: false,
    });
    appToast.success("Item saved");
    setEditingId(null);
    await reload();
  };

  const remove = async (item: ShoppingListItem) => {
    try {
      await deleteShoppingListItem(item.id);
      await reload();
    } catch (error) {
      appToast.error(error, "Could not remove the item");
    }
  };

  const clearChecked = async () => {
    setBusy(true);
    try {
      const result = await clearCheckedItems(list.id);
      appToast.success(
        `${result.removed} ${result.removed === 1 ? "item" : "items"} cleared`,
      );
      await reload();
    } catch (error) {
      appToast.error(error, "Could not clear the list");
    } finally {
      setBusy(false);
    }
  };

  const rename = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!name.trim()) return;
    setBusy(true);
    try {
      await renameShoppingList(list.id, name.trim());
      setRenaming(false);
      await reload();
    } catch (error) {
      appToast.error(error, "Could not rename the list");
    } finally {
      setBusy(false);
    }
  };

  const removeList = async () => {
    try {
      await deleteShoppingList(list.id);
      appToast.success(`${title} deleted`);
      router.push("/meal-plan/shopping-list");
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not delete the list");
    }
  };

  const itemForm = (item: ShoppingListItem) => ({
    name: item.itemName,
    amount:
      item.grams != null
        ? String(item.grams)
        : item.amount != null
          ? String(item.amount)
          : "",
    unit: item.grams != null ? "g" : (item.unit ?? ""),
  });

  return (
    <div
      className="log-page space-y-8"
      data-testid="shopping-list-page"
      data-hydrated={hydrated || undefined}
    >
      <header className="log-page-header block!">
        <p className="text-sm print:hidden">
          <Link href="/meal-plan/shopping-list" className="text-link">
            Shopping lists
          </Link>
        </p>
        {renaming ? (
          <form onSubmit={rename} className="mt-1 flex flex-wrap gap-2">
            <label htmlFor={`${id}-rename`} className="sr-only">
              List name
            </label>
            <input
              id={`${id}-rename`}
              type="text"
              className="input min-w-0 flex-1"
              value={name}
              onChange={(event) => setName(event.target.value)}
              autoFocus
            />
            <button
              type="submit"
              className="btn-primary btn-sm"
              disabled={busy}
            >
              Save name
            </button>
            <button
              type="button"
              className="btn-ghost btn-sm"
              onClick={() => {
                setRenaming(false);
                setName(list.name ?? "");
              }}
            >
              Cancel
            </button>
          </form>
        ) : (
          <h1 className="mt-1">{title}</h1>
        )}
        <p className="log-muted mt-2">
          {list.mealPlanName && list.mealPlanId ? (
            <>
              From{" "}
              <Link
                href={`/meal-plan/${list.mealPlanId}`}
                className="text-link"
              >
                {list.mealPlanName}
              </Link>
              {" · "}
            </>
          ) : null}
          <span className="num" data-testid="items-left">
            {left} left of {list.items.length}
          </span>
          {list.needsReviewCount > 0 && (
            <> · {list.needsReviewCount} to check</>
          )}
        </p>
        {list.totalCost != null && (
          <p className="mt-1" data-testid="list-cost">
            <span className="log-muted">Total </span>
            <span className="num font-medium">
              {formatMoney(list.totalCost, list.currency)}
            </span>
            {list.costConfidence && (
              <span className="log-muted">
                {" · "}
                {confidenceLabel(list.costConfidence)}
              </span>
            )}
            {list.unpricedCount > 0 && (
              <span className="log-muted">
                {" · "}
                {itemCount(list.unpricedCount)} without a price
              </span>
            )}
          </p>
        )}
        {list.canEdit && (
          <div className="mt-4 flex flex-wrap items-center gap-2 print:hidden">
            <button
              type="button"
              className="btn-secondary btn-sm min-h-9"
              onClick={() => setRenaming(true)}
            >
              Rename
            </button>
            <button
              type="button"
              className="btn-ghost btn-sm min-h-9"
              disabled={busy || checked === 0}
              onClick={clearChecked}
            >
              Clear checked
            </button>
            <button
              type="button"
              className="btn-ghost btn-sm min-h-9"
              onClick={() => window.print()}
            >
              Print
            </button>
            <button
              type="button"
              className="btn-ghost btn-sm min-h-9 text-destructive"
              onClick={() => setConfirmDelete(true)}
            >
              Delete list
            </button>
          </div>
        )}
      </header>

      {list.items.length === 0 && !adding ? (
        <p className="log-empty">Nothing on this list yet.</p>
      ) : (
        groupItems(list.items).map((group) => (
          <section key={group.key} aria-label={group.title}>
            <header className="log-section-header">
              <h2>{group.title}</h2>
              <span className="num text-sm log-muted">
                {group.items.filter((item) => !item.isChecked).length} left
              </span>
            </header>
            <ul>
              {group.items.map((item) =>
                editingId === item.id ? (
                  <li key={item.id}>
                    <ItemForm
                      title={`Edit ${item.itemName}`}
                      initial={itemForm(item)}
                      onSubmit={(value) => edit(item, value)}
                      onCancel={() => setEditingId(null)}
                    />
                  </li>
                ) : (
                  <li key={item.id} className="ledger-row">
                    <input
                      type="checkbox"
                      id={`${id}-${item.id}`}
                      checked={item.isChecked}
                      disabled={!list.canEdit}
                      onChange={() => void toggle(item)}
                      className="h-5 w-5 shrink-0 accent-brand-700"
                    />
                    <label
                      htmlFor={`${id}-${item.id}`}
                      className={`min-w-0 flex-1 cursor-pointer ${item.isChecked ? "log-muted line-through" : ""}`}
                    >
                      {item.itemName}
                      {item.needsReview && (
                        <span className="ml-2 text-xs font-medium text-destructive">
                          Check
                        </span>
                      )}
                    </label>
                    <span
                      className={`num whitespace-nowrap text-right text-sm ${item.isChecked ? "log-muted" : ""}`}
                    >
                      {formatAmount(item)}
                      {item.cost != null && (
                        <span
                          className="block text-xs log-muted"
                          data-testid="item-cost"
                        >
                          {formatMoney(item.cost, list.currency)}
                        </span>
                      )}
                    </span>
                    {list.canEdit && (
                      <span className="flex shrink-0 gap-1 print:hidden">
                        <button
                          type="button"
                          className="btn-ghost btn-sm min-h-9"
                          aria-label={`Edit ${item.itemName}`}
                          onClick={() => setEditingId(item.id)}
                        >
                          Edit
                        </button>
                        <button
                          type="button"
                          className="btn-ghost btn-sm min-h-9 text-destructive"
                          aria-label={`Delete ${item.itemName}`}
                          onClick={() => remove(item)}
                        >
                          Delete
                        </button>
                      </span>
                    )}
                  </li>
                ),
              )}
            </ul>
          </section>
        ))
      )}

      {list.canEdit &&
        (adding ? (
          <ItemForm
            title="New item"
            initial={{ name: "", amount: "", unit: "" }}
            onSubmit={add}
            onCancel={() => setAdding(false)}
          />
        ) : (
          <button
            type="button"
            className="btn-primary min-h-11 w-full sm:w-auto print:hidden"
            onClick={() => setAdding(true)}
          >
            Add item
          </button>
        ))}

      <DeleteConfirmModal
        isOpen={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        onConfirm={removeList}
        itemName={title}
      />
    </div>
  );
}
