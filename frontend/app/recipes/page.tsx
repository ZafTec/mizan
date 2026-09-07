import Link from "next/link";
import { ArrowRight, Search, Plus } from "lucide-react";
import { getUserOptionalServer } from "@/helper/session";
import { serverApi } from "@/lib/api.server";
import type { RecipeDto } from "@/data/recipe";
import { LogEntryButton } from "@/components/logging/LogEntryButton";
import Pagination from "@/components/Pagination";

export const metadata = {
  title: "Recipes · Mizan",
  description: "Saved meals, ready to log again.",
};

export default async function RecipesPage({
  searchParams,
}: {
  searchParams: Promise<{ search?: string; page?: string; pinned?: string }>;
}) {
  const [params, user] = await Promise.all([
    searchParams,
    getUserOptionalServer(),
  ]);
  const page = Math.max(1, Math.floor(Number(params.page) || 1));
  const pinned = params.pinned === "true";
  const query = new URLSearchParams({
    SearchTerm: params.search || "",
    Page: String(page),
    PageSize: "12",
    IncludePublic: "true",
    FavoritesOnly: String(pinned),
  });
  const result = await serverApi<{
    items: RecipeDto[];
    totalPages: number;
    totalCount: number;
  }>(`/api/Recipes?${query}`, { requireAuth: pinned });
  const paging = new URLSearchParams();
  if (params.search) paging.set("search", params.search);
  if (pinned) paging.set("pinned", "true");
  return (
    <div className="log-page" data-testid="recipe-list">
      <header className="log-page-header">
        <div>
          <h1>Recipes</h1>
          <p className="log-muted mt-2">Meals worth making again.</p>
        </div>
        {user && (
          <LogEntryButton kind="meal" className="btn-primary">
            <Plus size={17} /> Log a meal
          </LogEntryButton>
        )}
      </header>
      <form className="directory-search" action="/recipes">
        <Search size={18} aria-hidden="true" />
        <label htmlFor="recipe-search" className="sr-only">
          Search recipes
        </label>
        <input
          id="recipe-search"
          name="search"
          defaultValue={params.search}
          placeholder="Search recipes…"
          type="search"
        />
        {pinned && <input type="hidden" name="pinned" value="true" />}
        <button type="submit" className="text-link">
          Search
        </button>
      </form>
      {user && (
        <nav aria-label="Recipe filter" className="period-tabs">
          <Link href="/recipes" aria-current={!pinned ? "page" : undefined}>
            All recipes
          </Link>
          <Link
            href="/recipes?pinned=true"
            aria-current={pinned ? "page" : undefined}
          >
            Pinned
          </Link>
        </nav>
      )}
      {result.items.length ? (
        <div className="recipe-library">
          {result.items.map((recipe) => (
            <article className="recipe-library-row" key={recipe.id}>
              <Link href={`/recipes/${recipe.id}`} className="min-w-0 flex-1">
                <h2 className="text-base!">{recipe.title}</h2>
                <p className="log-muted text-sm mt-2">
                  {recipe.nutrition?.caloriesPerServing != null
                    ? `${Math.round(recipe.nutrition.caloriesPerServing)} kcal · ${Math.round(recipe.nutrition.proteinGrams ?? 0)} g protein per serving`
                    : "View ingredients and nutrition"}
                </p>
              </Link>
              <Link
                href={`/recipes/${recipe.id}`}
                className="icon-button"
                aria-label={`View ${recipe.title}`}
              >
                <ArrowRight size={17} />
              </Link>
            </article>
          ))}
        </div>
      ) : (
        <div className="log-empty">
          <h2>
            {params.search
              ? "No recipes match your search"
              : pinned
                ? "Your pinned recipes will appear here"
                : "Your recipe collection starts with a meal"}
          </h2>
          <p className="log-muted mt-3 max-w-lg text-sm">
            {params.search
              ? "Try another name or clear the search."
              : "Log two or more foods together, then choose Save as recipe in your daily log."}
          </p>
          <Link
            href={params.search ? "/recipes" : user ? "/today" : "/register"}
            className="btn-secondary mt-5"
          >
            {params.search
              ? "Clear search"
              : user
                ? "Go to your log"
                : "Create an account"}
          </Link>
        </div>
      )}
      <Pagination
        currentPage={page}
        totalPages={result.totalPages}
        baseUrl={`/recipes${paging.size ? `?${paging}` : ""}`}
      />
      {result.items.length > 0 && user && (
        <p className="text-sm log-muted mt-6">
          To save a new recipe, log a meal with at least two items and choose{" "}
          <Link href="/today" className="text-link">
            Save as recipe
          </Link>
          .
        </p>
      )}
    </div>
  );
}
