import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { listShoppingListsServer } from "@/lib/api/shoppingLists.server";
import type { ShoppingListSummary } from "@/lib/api/shoppingLists";
import NewListButton from "@/components/planning/NewListButton";
import { logger } from "@/lib/logger";

const log = logger.createModuleLogger("shopping-lists-page");

export const dynamic = "force-dynamic";
export const metadata = { title: "Shopping lists · Mizan" };

const when = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
  });

export default async function ShoppingListsPage() {
  let lists: ShoppingListSummary[] = [];
  let failed = false;
  try {
    lists = (await listShoppingListsServer(100)).items.sort((a, b) =>
      b.createdAt.localeCompare(a.createdAt),
    );
  } catch (error) {
    log.error("Failed to load shopping lists", {
      error: error instanceof Error ? error.message : String(error),
    });
    failed = true;
  }
  const open = lists.reduce(
    (sum, list) => sum + (list.totalItems - list.checkedItems),
    0,
  );
  return (
    <div className="log-page space-y-8" data-testid="shopping-lists-page">
      <header className="log-page-header block!">
        <h1>Shopping lists</h1>
        <p className="log-muted mt-2">What your plans need you to buy.</p>
      </header>
      {failed ? (
        <p className="log-empty">Your lists could not be loaded.</p>
      ) : (
        <>
          {lists.length > 0 && (
            <section aria-label="List summary" className="facts-strip">
              <div>
                <p className="text-sm log-muted">Lists</p>
                <p className="num nutrition-value">{lists.length}</p>
              </div>
              <div>
                <p className="text-sm log-muted">Left to buy</p>
                <p className="num nutrition-value">{open}</p>
              </div>
              <div>
                <p className="text-sm log-muted">To check</p>
                <p className="num nutrition-value">
                  {lists.reduce((sum, list) => sum + list.needsReviewCount, 0)}
                </p>
              </div>
            </section>
          )}
          {lists.length === 0 ? (
            <p className="log-empty">No lists yet.</p>
          ) : (
            <ul>
              {lists.map((list) => (
                <li key={list.id}>
                  <Link
                    href={`/shopping-lists/${list.id}`}
                    className="ledger-row transition-colors hover:bg-muted"
                  >
                    <span className="min-w-0 flex-1">
                      <span className="block font-medium">
                        {list.name || "Shopping list"}
                      </span>
                      <span className="mt-0.5 block text-sm log-muted">
                        {list.mealPlanId
                          ? `From a plan · ${when(list.generatedAt ?? list.createdAt)}`
                          : when(list.createdAt)}
                        {list.householdId ? " · household" : ""}
                      </span>
                    </span>
                    <span className="num whitespace-nowrap text-sm log-muted">
                      <span className="text-base font-semibold text-foreground">
                        {list.totalItems - list.checkedItems}
                      </span>{" "}
                      left of {list.totalItems}
                    </span>
                    <ChevronRight
                      aria-hidden="true"
                      size={17}
                      strokeWidth={1.7}
                      className="log-muted"
                    />
                  </Link>
                </li>
              ))}
            </ul>
          )}
          <NewListButton />
        </>
      )}
    </div>
  );
}
