"use client";

import { useEffect, useId, useState } from "react";
import { ArrowLeft, Heart, History, Plus, Search } from "lucide-react";
import { clientApi } from "@/lib/api.client";
import type { RecipeResult } from "./logging";

type Section = { key: string; title: string; icon: typeof Heart | null; recipes: RecipeResult[] };

/**
 * Logging from a recipe has its own view, so a long list does not push the meal off the screen. Favorites come
 * first, then the recipes this person logs most, then everything else. Choosing one adds it to the meal and goes
 * straight back.
 */
export default function RecipePicker({
  onPick,
  onBack,
}: {
  onPick: (recipe: RecipeResult) => void;
  onBack: () => void;
}) {
  const id = useId();
  const [query, setQuery] = useState("");
  const [favorites, setFavorites] = useState<RecipeResult[]>([]);
  const [frequent, setFrequent] = useState<RecipeResult[]>([]);
  const [results, setResults] = useState<RecipeResult[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    const term = query.trim();
    const timer = window.setTimeout(async () => {
      const params = new URLSearchParams({ SearchTerm: term, PageSize: "30", IncludePublic: "true" });
      const [pinned, often, all] = await Promise.allSettled([
        clientApi<{ items: RecipeResult[] }>("/api/Recipes?FavoritesOnly=true&PageSize=30"),
        clientApi<{ items: RecipeResult[] }>("/api/Recipes?SortBy=frequent&PageSize=10"),
        clientApi<{ items: RecipeResult[] }>(`/api/Recipes?${params}`),
      ]);
      if (!active) return;
      setFavorites(pinned.status === "fulfilled" ? pinned.value.items ?? [] : []);
      setFrequent(often.status === "fulfilled" ? often.value.items ?? [] : []);
      setResults(all.status === "fulfilled" ? all.value.items ?? [] : []);
      setFailed([pinned, often, all].every((r) => r.status === "rejected"));
    }, term ? 200 : 0);
    return () => {
      active = false;
      window.clearTimeout(timer);
    };
  }, [query, attempt]);

  const term = query.trim().toLowerCase();
  const matches = (recipe: RecipeResult) => !term || recipe.title.toLowerCase().includes(term);

  const sections: Section[] = [];
  const shown = new Set<string>();
  const take = (recipes: RecipeResult[]) =>
    recipes.filter(matches).filter((recipe) => {
      if (shown.has(recipe.id)) return false;
      shown.add(recipe.id);
      return true;
    });

  const favoriteRows = take(favorites.map((recipe) => ({ ...recipe, isFavorited: true })));
  const frequentRows = take(frequent);
  const otherRows = take(results ?? []);
  if (favoriteRows.length) sections.push({ key: "favorites", title: "Favorites", icon: Heart, recipes: favoriteRows });
  if (frequentRows.length) sections.push({ key: "frequent", title: "Often logged", icon: History, recipes: frequentRows });
  if (otherRows.length) sections.push({ key: "all", title: term ? "Results" : "All recipes", icon: null, recipes: otherRows });

  const loading = results === null;

  return (
    <section aria-label="Log from a recipe" className="flex min-h-0 flex-1 flex-col">
      <div className="mb-4 flex items-center justify-between gap-3">
        <button
          type="button"
          onClick={onBack}
          className="flex min-h-11 items-center gap-2 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300"
        >
          <ArrowLeft aria-hidden="true" size={16} strokeWidth={1.7} />
          Back to your meal
        </button>
      </div>
      <div className="relative mb-3">
        <label htmlFor={id} className="sr-only">
          Search recipes
        </label>
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
          autoComplete="off"
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Search your recipes"
          className="input min-h-11 w-full pl-10"
        />
      </div>

      <div className="min-h-0 flex-1 space-y-5 overflow-y-auto overscroll-contain pb-5" aria-busy={loading}>
        {failed && (
          <div role="alert" className="flex items-start justify-between gap-3 text-sm text-burnt-peach-700 dark:text-burnt-peach-300">
            <p>Recipes could not load.</p>
            <button
              type="button"
              className="min-h-11 shrink-0 underline underline-offset-4"
              onClick={() => {
                setResults(null);
                setAttempt((value) => value + 1);
              }}
            >
              Try again
            </button>
          </div>
        )}
        {loading && !failed && (
          <ul aria-hidden="true" className="divide-y divide-border border-y border-border">
            {[1, 2, 3].map((row) => (
              <li key={row} className="flex h-[68px] items-center px-1">
                <span className="h-3 w-1/2 rounded-sm bg-muted" />
              </li>
            ))}
          </ul>
        )}
        {!loading && !failed && sections.length === 0 && (
          <p className="py-6 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
            {term ? "No recipes match. Try another name." : "You have no recipes yet. Save a meal as a recipe to find it here."}
          </p>
        )}
        {sections.map((section) => (
          <div key={section.key} role="group" aria-label={section.title}>
            <h3 className="mb-1 flex items-center gap-2 text-sm font-semibold">
              {section.icon && <section.icon aria-hidden="true" size={14} strokeWidth={1.7} className="text-brand-700 dark:text-brand-300" />}
              {section.title}
            </h3>
            <ul className="divide-y divide-border border-y border-border">
              {section.recipes.map((recipe) => (
                <li key={recipe.id}>
                  <button
                    type="button"
                    onClick={() => onPick(recipe)}
                    aria-label={`Add ${recipe.title}`}
                    className="flex min-h-[68px] w-full items-center gap-3 px-1 py-3 text-left transition-colors hover:bg-muted"
                  >
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-medium">{recipe.title}</span>
                      <span className="mt-0.5 block text-xs text-charcoal-blue-700 dark:text-charcoal-blue-300">
                        {recipe.nutrition?.caloriesPerServing == null
                          ? "Nutrition unavailable"
                          : `${Math.round(recipe.nutrition.caloriesPerServing)} kcal / serving`}
                        {recipe.nutrition?.proteinGrams != null && ` · ${Math.round(recipe.nutrition.proteinGrams)} g protein`}
                        {(recipe.timesLogged ?? 0) > 0 && ` · Logged ${recipe.timesLogged} ${recipe.timesLogged === 1 ? "time" : "times"}`}
                      </span>
                    </span>
                    <Plus aria-hidden="true" size={18} strokeWidth={1.7} className="shrink-0" />
                  </button>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </section>
  );
}
