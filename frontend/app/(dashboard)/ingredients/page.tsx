import Link from "next/link";
import { getAllIngredient } from "@/data/ingredient";
import { getUserOptionalServer } from "@/helper/session";
import { DataTable, readPage, readSort, type Column } from "@/components/ui/data-table";
import SearchBar from "@/components/IngredientTable/SearchInputField";
import IngredientFilters from "./IngredientFilters";
import { AppFeatureIllustration } from "@/components/illustrations/AppFeatureIllustration";

export const dynamic = "force-dynamic";

const PAGE_SIZE = 10;

type Ingredient = Awaited<ReturnType<typeof getAllIngredient>>["ingredients"][number];

const macro = (value: number | null | undefined) => (
	<span className="tabular-nums">{value ?? 0}g</span>
);

const columns: Column<Ingredient>[] = [
	{
		id: "name",
		header: "Name",
		sortKey: "name",
		cell: (i) => (
			<span className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
				{i.name}
			</span>
		),
	},
	{
		id: "calories",
		header: "kcal",
		sortKey: "calories",
		align: "center",
		cell: (i) => <span className="tabular-nums">{i.caloriesPer100g}</span>,
	},
	{
		id: "protein",
		header: "Protein",
		sortKey: "protein",
		align: "center",
		cell: (i) => macro(i.proteinPer100g),
	},
	{ id: "fat", header: "Fat", align: "center", secondary: true, cell: (i) => macro(i.fatPer100g) },
	{
		id: "carbs",
		header: "Carbs",
		align: "center",
		secondary: true,
		cell: (i) => macro(i.carbsPer100g),
	},
	{
		id: "fiber",
		header: "Fiber",
		align: "center",
		secondary: true,
		cell: (i) => macro(i.fiberPer100g),
	},
	{
		id: "pcal",
		header: "P/Cal",
		sortKey: "proteinCalorieRatio",
		align: "center",
		cell: (i) => (
			<span className="inline-flex items-center rounded-lg bg-violet-50 px-2 py-0.5 text-xs font-medium tabular-nums text-violet-700 dark:bg-violet-500/12 dark:text-violet-300">
				{i.proteinCalorieRatio.toFixed(0)}%
			</span>
		),
	},
];

export default async function IngredientsPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	const user = await getUserOptionalServer();
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);
	const minPcal = params.minPcal ? Number.parseInt(params.minPcal, 10) : 0;

	const { ingredients, totalPages, totalCount } = await getAllIngredient(
		params.searchIngredient || undefined,
		sort.sortBy ?? undefined,
		sort.sortOrder,
		page,
		PAGE_SIZE,
		minPcal > 0 ? minPcal : undefined,
	);

	return (
		<div className="space-y-6 lg:space-y-8" data-testid="ingredient-list">
			<header className="flex flex-col gap-6 sm:flex-row sm:items-end sm:justify-between">
				<div className="flex items-start gap-6">
					<div className="hidden w-28 shrink-0 sm:block">
						<AppFeatureIllustration variant="recipes" />
					</div>
					<div className="space-y-2">
						<p className="eyebrow">Catalogue</p>
						<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							Foods
						</h1>
					</div>
				</div>
				<div className="flex items-center gap-3">
					<SearchBar />
					<IngredientFilters currentMinPcal={minPcal || undefined} />
					{user?.role === "admin" && (
						<Link href="/ingredients/add" className="btn-primary !rounded-2xl">
							Add
						</Link>
					)}
				</div>
			</header>

			<DataTable
				columns={columns}
				rows={ingredients}
				rowKey={(i) => i.id}
				href={(i) => `/ingredients/${i.id}`}
				pathname="/ingredients"
				searchParams={params}
				sort={sort}
				page={{ page, pageSize: PAGE_SIZE, totalCount, totalPages }}
				empty={{
					title: "No foods match",
					description: "Try a different search, or widen the protein filter.",
				}}
			/>
		</div>
	);
}
