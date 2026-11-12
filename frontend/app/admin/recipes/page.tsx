import { getAllRecipes, type RecipeDto } from "@/data/recipe";
import {
  DataTable,
  Pill,
  TableToolbar,
  readPage,
  readSort,
  type Column,
} from "@/components/ui/data-table";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export const dynamic = "force-dynamic";

const PAGE_SIZE = 20;

const columns: Column<RecipeDto>[] = [
  {
    id: "title",
    header: "Title",
    sortKey: "title",
    cell: (r) => (
      <div className="min-w-0">
        <p className="truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
          {r.title}
        </p>
        {r.description && (
          <p className="line-clamp-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            {r.description}
          </p>
        )}
      </div>
    ),
  },
  {
    id: "servings",
    header: "Servings",
    align: "center",
    secondary: true,
    cell: (r) => <span className="tabular-nums">{r.servings || "—"}</span>,
  },
  {
    id: "calories",
    header: "kcal",
    align: "center",
    cell: (r) => (
      <span className="tabular-nums">
        {Math.round(r.nutrition?.caloriesPerServing ?? 0)}
      </span>
    ),
  },
  {
    id: "protein",
    header: "Protein",
    align: "center",
    secondary: true,
    cell: (r) => (
      <span className="tabular-nums">
        {Math.round(r.nutrition?.proteinGrams ?? 0)}g
      </span>
    ),
  },
  {
    id: "visibility",
    header: "Visibility",
    align: "center",
    cell: (r) =>
      r.isPublic ? <Pill tone="good">Public</Pill> : <Pill>Private</Pill>,
  },
];

export default async function AdminRecipesPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;
  const page = readPage(params);
  const sort = readSort(params);

  const { recipes, totalPages, totalCount } = await getAllRecipes(
    params.search || undefined,
    page,
    PAGE_SIZE,
    false,
    sort.sortBy ?? undefined,
    sort.sortOrder,
  );

  return (
    <div className="flex flex-col gap-6 [&>header]:mb-2">
      <AdminPageHeader
        title="Recipes"
        lead="Every recipe people have saved; a row opens the recipe."
      />

      <TableToolbar searchPlaceholder="Search recipes…" />

      <DataTable
        columns={columns}
        rows={recipes}
        rowKey={(r) => r.id}
        href={(r) => `/recipes/${r.id}`}
        pathname="/admin/recipes"
        searchParams={params}
        sort={sort}
        page={{ page, pageSize: PAGE_SIZE, totalCount, totalPages }}
        empty={{
          title: "No recipes match",
          description: "Try a different search.",
        }}
      />
    </div>
  );
}
