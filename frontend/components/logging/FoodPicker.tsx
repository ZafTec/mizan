"use client";

import { useEffect, useId, useState } from "react";
import { Pin, Plus, Search } from "lucide-react";
import { clientApi } from "@/lib/api.client";
import {
  foodItem,
  recipeItem,
  type FoodResult,
  type PickerItem,
  type RecipeResult,
} from "./logging";

type SearchState = { query: string; items: PickerItem[]; error: string | null };

export default function FoodPicker({
  date,
  disabled,
  onSelect,
}: {
  date: string;
  disabled?: boolean;
  onSelect: (item: PickerItem) => void;
}) {
  const id = useId();
  const [query, setQuery] = useState("");
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<SearchState | null>(null);
  const [favorites, setFavorites] = useState<RecipeResult[]>([]);
  const [recentIds, setRecentIds] = useState<string[]>([]);
  const loading = result?.query !== query.trim();

  useEffect(() => {
    let active = true;
    Promise.allSettled([
      clientApi<{ items: RecipeResult[] }>(
        "/api/Recipes?FavoritesOnly=true&PageSize=30",
      ),
      clientApi<{
        entries: { foodId?: string; recipeId?: string; loggedAt: string }[];
      }>(`/api/Meals?date=${encodeURIComponent(date)}`),
    ]).then(([pinned, recent]) => {
      if (!active) return;
      if (pinned.status === "fulfilled") setFavorites(pinned.value.items ?? []);
      if (recent.status === "fulfilled") {
        setRecentIds(
          (recent.value.entries ?? [])
            .slice()
            .sort((a, b) => b.loggedAt.localeCompare(a.loggedAt))
            .map((entry) => entry.recipeId || entry.foodId || "")
            .filter(Boolean),
        );
      }
    });
    return () => {
      active = false;
    };
  }, [date]);

  useEffect(() => {
    let active = true;
    const term = query.trim();
    const timer = window.setTimeout(
      async () => {
        const params = new URLSearchParams({
          SearchTerm: term,
          PageSize: "20",
        });
        const [foods, recipes] = await Promise.allSettled([
          clientApi<{ items: FoodResult[] }>(`/api/Foods/search?${params}`),
          clientApi<{ items: RecipeResult[] }>(
            `/api/Recipes?${params}&IncludePublic=true`,
          ),
        ]);
        if (!active) return;
        const items = [
          ...(foods.status === "fulfilled"
            ? foods.value.items.map(foodItem)
            : []),
          ...(recipes.status === "fulfilled"
            ? recipes.value.items.map((recipe) => recipeItem(recipe))
            : []),
        ];
        const failed = [foods, recipes].filter(
          (response) => response.status === "rejected",
        ).length;
        setResult({
          query: term,
          items,
          error:
            failed === 2
              ? "Food search is unavailable. Try again or enter your food manually."
              : failed === 1
                ? "Some results could not load. You can use these results or try again."
                : null,
        });
      },
      term ? 200 : 0,
    );
    return () => {
      active = false;
      window.clearTimeout(timer);
    };
  }, [query, attempt]);

  const pinnedIds = new Set(favorites.map((recipe) => recipe.id));
  const unique = new Map<string, PickerItem>();
  if (!loading) {
    const matchingFavorites = favorites.filter((recipe) =>
      recipe.title.toLowerCase().includes(query.trim().toLowerCase()),
    );
    [
      ...(result?.items ?? []),
      ...matchingFavorites.map((recipe) => recipeItem(recipe, true)),
    ].forEach((item) => {
      unique.set(`${item.kind}:${item.id}`, {
        ...item,
        pinned:
          item.kind === "recipe" && (item.pinned || pinnedIds.has(item.id)),
      });
    });
  }
  const items = [...unique.values()].sort((a, b) => {
    if (Boolean(a.pinned) !== Boolean(b.pinned)) return a.pinned ? -1 : 1;
    const recentA = recentIds.indexOf(a.id);
    const recentB = recentIds.indexOf(b.id);
    if (recentA !== recentB)
      return (
        (recentA < 0 ? Infinity : recentA) - (recentB < 0 ? Infinity : recentB)
      );
    if (a.lastUsedAt !== b.lastUsedAt)
      return (b.lastUsedAt ?? "").localeCompare(a.lastUsedAt ?? "");
    return a.name.localeCompare(b.name);
  });

  return (
    <section aria-label="Food and recipe picker" className="space-y-3">
      <label htmlFor={id} className="block text-sm font-medium">
        Find food or a recipe
      </label>
      <div className="relative">
        <Search
          aria-hidden="true"
          size={18}
          strokeWidth={1.7}
          className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-charcoal-blue-700 dark:text-charcoal-blue-300"
        />
        <input
          id={id}
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") event.preventDefault();
          }}
          placeholder="Search your foods and recipes"
          autoComplete="off"
          disabled={disabled}
          className="input min-h-11 w-full pl-10"
          aria-describedby={`${id}-status`}
        />
      </div>
      <p
        id={`${id}-status`}
        role="status"
        className="text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300"
      >
        {loading
          ? "Searching…"
          : query.trim()
            ? `${items.length} matching ${items.length === 1 ? "item" : "items"}`
            : "Pinned recipes and previously logged items come first."}
      </p>
      {result?.error && !loading && (
        <div
          role="alert"
          className="flex items-start justify-between gap-3 text-sm text-burnt-peach-700 dark:text-burnt-peach-300"
        >
          <p>{result.error}</p>
          <button
            type="button"
            className="min-h-11 shrink-0 underline underline-offset-4"
            onClick={() => {
              setResult(null);
              setAttempt((value) => value + 1);
            }}
          >
            Try again
          </button>
        </div>
      )}
      <ul
        aria-label="Food and recipe results"
        aria-busy={loading}
        className="max-h-56 overflow-y-auto overscroll-contain divide-y divide-border border-y border-border"
      >
        {loading ? (
          [1, 2, 3].map((row) => (
            <li
              key={row}
              aria-hidden="true"
              className="flex h-[68px] items-center justify-between gap-4 px-1"
            >
              <span className="h-3 w-1/2 rounded-sm bg-muted" />
              <span className="h-3 w-12 rounded-sm bg-muted" />
            </li>
          ))
        ) : items.length ? (
          items.map((item) => (
            <li key={`${item.kind}:${item.id}`}>
              <button
                type="button"
                disabled={disabled}
                onClick={() => onSelect(item)}
                className="flex min-h-[68px] w-full items-center gap-3 px-1 py-3 text-left transition-colors hover:bg-muted disabled:opacity-50"
                aria-label={`Add ${item.name}`}
              >
                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-2 font-medium">
                    <span className="truncate">{item.name}</span>
                    {item.pinned && (
                      <Pin
                        aria-label="Pinned recipe"
                        size={13}
                        strokeWidth={1.7}
                        className="shrink-0 text-brand-700 dark:text-brand-300"
                      />
                    )}
                  </span>
                  <span className="mt-0.5 block text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                    {item.detail} ·{" "}
                    {item.calories === null
                      ? "Nutrition unavailable"
                      : `${Math.round(item.calories)} kcal / ${item.kind === "food" ? "100 g" : "serving"}`}
                  </span>
                </span>
                <Plus
                  aria-hidden="true"
                  size={18}
                  strokeWidth={1.7}
                  className="shrink-0"
                />
              </button>
            </li>
          ))
        ) : (
          <li className="py-6 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
            {result?.error
              ? "Your manual entry is still available below."
              : "No matches yet. Try another name or enter the food below."}
          </li>
        )}
      </ul>
    </section>
  );
}
