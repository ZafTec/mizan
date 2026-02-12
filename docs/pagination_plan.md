# Pagination, Sorting & Filtering -- Full Implementation Plan

## Context

The MacroChef backend has 29 queries, but only 7 support pagination. Zero support sorting params (all ORDER BY is hardcoded). Filtering is ad-hoc per query. The MCP tool handler passes minimal params to the backend. The frontend has no shared abstractions -- each list page re-implements pagination/state from scratch. Sorting UI exists on some pages but is non-functional.

**Goal:** Standardize pagination/sorting/filtering across backend, MCP, and frontend with shared types and reusable components.

---

## Phase 1: Backend Shared Types & Standardize Existing Queries

### 1a. Create shared pagination types

**New:** `backend/Mizan.Application/Common/Pagination.cs`

```csharp
public interface IPagedQuery { int Page { get; init; } int PageSize { get; init; } }
public interface ISortableQuery { string? SortBy { get; init; } string? SortOrder { get; init; } }

public record PagedResult<T> {
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
```

**New:** `backend/Mizan.Application/Common/QueryableExtensions.cs`
- `ApplyPaging<T>(IPagedQuery)` -- Skip/Take
- `ApplySorting<T>(ISortableQuery, Dictionary<string, Expression>, defaultSort)` -- dynamic OrderBy via sort mappings

**New:** `backend/Mizan.Application/Common/PaginationValidator.cs`
- `ValidPage()` -- >= 1
- `ValidPageSize()` -- 1-100
- `ValidSortOrder()` -- null, "asc", or "desc"

### 1b. Retrofit 6 existing paginated queries to use `PagedResult<T>`

| Query | Current Result | Sort Options |
|-------|---------------|-------------|
| `SearchFoodsQuery` | `SearchFoodsResult { Foods, TotalCount }` (missing Page/PageSize) | name, calories, protein, verified |
| `GetRecipesQuery` | `GetRecipesResult { Recipes, TotalCount, Page, PageSize }` | title, createdAt |
| `GetMealPlansQuery` | `GetMealPlansResult` | startDate, name |
| `GetExercisesQuery` | `GetExercisesResult` (keep filter options alongside) | name, category |
| `GetShoppingListsQuery` | `GetShoppingListsResult` | name, updatedAt |
| `GetAuditLogsQuery` | `GetAuditLogsResult { Logs, TotalCount }` (missing Page/PageSize) | timestamp, action |

Each: implement `IPagedQuery, ISortableQuery`, use `ApplyPaging`/`ApplySorting` extensions, return `PagedResult<TDto>`.

**Files:** 6 query files + their handlers in `backend/Mizan.Application/Queries/`

---

## Phase 2: Backend -- Add Pagination to Unpaginated Queries

Add `Page`, `PageSize`, `SortBy`, `SortOrder` to these queries that currently return raw `List<T>`:

| Query | Default Sort | Priority |
|-------|-------------|----------|
| `GetBodyMeasurementsQuery` | date desc | High (table page) |
| `GetAchievementsQuery` | earnedAt desc | Medium |
| `GetAvailableTrainersQuery` + add `SearchTerm` | name asc | High (browsing) |
| `GetTrainerClientsQuery` | name asc | Medium |
| `GetTrainerPendingRequestsQuery` | createdAt desc | Low |
| `GetMyTrainerRequestsQuery` | createdAt desc | Low |
| `GetMcpTokensQuery` | createdAt desc | Low |

**NOT paginated (intentionally):** GetChatConversation (cursor-based later), GetGoalProgressHistory (bounded by Days), GetFoodDiary (bounded by date), single-entity queries.

**Files:** 7 query files + 4 controllers (`BodyMeasurementsController`, `AchievementsController`, `TrainersController`, `McpTokensController`)

---

## Phase 3: MCP Tool Handler Updates

**File:** `backend/Mizan.Mcp.Server/Services/McpToolHandler.cs`

Add params to tool schemas and wire to backend query strings:

```
list_ingredients: + page (int), sortBy (string), sortOrder (string)
list_recipes:     + page (int), limit (int), sortBy, sortOrder, tags (string), favoritesOnly (bool)
get_shopping_list: + page (int), limit (int)
```

Pass all params as query string to backend API calls.

---

## Phase 4: MCP Integration Tests

**File:** `backend/Mizan.Tests/Integration/McpIntegrationTests.cs`

New tests (~8):
- `ListIngredients_ReturnsSecondPage` -- seed 5 items, request page 2 with pageSize 2
- `ListIngredients_SortsByName_Ascending` -- verify order
- `ListIngredients_SortsByCalories_Descending`
- `ListIngredients_ReturnsTotalCountAndPageInfo`
- `ListRecipes_ReturnsPagedResults`
- `ListRecipes_SortsByTitle`
- `ListRecipes_FiltersByTags`
- `GetShoppingList_ReturnsPagedResults`

---

## Phase 5: Frontend -- Shared Hook & Components

### 5a. `useListState` hook

**New:** `frontend/lib/hooks/useListState.ts`

URL-search-params-based state management. No zustand needed -- URL params ARE the store (SSR-friendly, shareable, back-button works).

```typescript
export function useListState<TFilters>(defaults) {
  // Reads page, pageSize, sortBy, sortOrder, filters from URL searchParams
  // Returns { page, sortBy, sortOrder, filters, setPage, setSort, setFilter, resetFilters }
  // setSort toggles asc/desc and resets to page 1
  // setFilter resets to page 1
}
```

### 5b. Enhanced Pagination component

**Modify:** `frontend/components/Pagination.tsx`

- Add `totalCount`, `pageSize` props for "Showing X-Y of Z" display
- Support both `baseUrl` (SSR Link mode) and `onPageChange` (client callback mode)
- Optional page size selector

### 5c. SortableHeader component

**New:** `frontend/components/SortableHeader.tsx`

Renders as `<Link>` (SSR) or `<button>` (client). Shows arrow icon for current sort state.

### 5d. URL builder utility

**New:** `frontend/lib/utils/list-params.ts`

`buildListUrl(basePath, params)` -- builds URL with query params, used by SSR pages.

---

## Phase 6: Frontend -- Apply to All List Pages

### SSR page pattern (most pages):

```tsx
// Server component reads searchParams from props
export default async function Page({ searchParams }) {
  const params = await searchParams;
  const { items, totalCount, totalPages } = await fetchData({
    page: params.page, sortBy: params.sortBy, ...
  });
  return (
    <ClientFilterBar />  {/* uses useListState for interactions */}
    <Table with SortableHeader href links />
    <Pagination baseUrl={buildListUrl(...)} />
  );
}
```

### Pages to update:

| Page | Changes |
|------|---------|
| `/ingredients` | Wire SortableHeader to URL params, remove client-side sort from `data/ingredient.ts` |
| `/recipes` | Wire existing non-functional sort/filter buttons, add search |
| `/exercises` | Add sort headers |
| `/body-measurements` | Add pagination + sort by date/weight |
| `/achievements` | Add pagination + category filter |
| `/trainers` | Convert from client-side to SSR with pagination + search |
| `/trainers/requests` | Add pagination |
| `/meal-plan` | Add sort by date/name |
| `/meal-plan/shopping-list` | Add sort |
| `/admin/ingredients` | Add sort headers |
| `/admin/users` | Add sort headers |
| `/admin/audit-logs` | Add sort headers |

**Data layer files:** Update `data/ingredient.ts`, `data/recipe.ts`, `data/exercise.ts`, `data/bodyMeasurement.ts`, `data/achievement.ts`, `data/mealPlan.ts`, `data/shoppingList.ts` to pass `sortBy`/`sortOrder` to API.

---

## Phase 7: Codegen & Verify

```bash
cd frontend && bun run codegen    # Regenerate types from updated OpenAPI
bun run build                      # Verify no TypeScript errors
bun run lint                       # Verify no lint errors
cd ../backend && dotnet build      # Verify backend compiles
dotnet test                        # Run all tests
```

---

## Execution Order

```
Phase 1 (shared types) ─────┐
                             ├── Phase 2 (paginate queries) ── Phase 3 (MCP) ── Phase 4 (MCP tests)
Phase 5 (frontend hook) ────┘                                       │
                                                                     ├── Phase 6 (apply to pages)
                                                              Phase 7 (codegen + verify)
```

Phases 1+5 can run in parallel. Everything else is sequential.

## Estimated Scope

| Phase | Files | Complexity |
|-------|-------|-----------|
| 1. Shared types | 3 new + 6 modified | Medium |
| 2. Paginate queries | 7 queries + 4 controllers | Medium |
| 3. MCP tools | 1 file | Low |
| 4. MCP tests | 1 file | Low |
| 5. Frontend abstractions | 4 new files | Medium |
| 6. Apply to pages | ~15 pages + ~7 data files | High |
| 7. Codegen/verify | Commands only | Low |

Total: ~7 new files, ~40 modified files.
