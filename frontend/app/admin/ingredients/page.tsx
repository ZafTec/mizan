import Link from "next/link";
import { getAllIngredient } from "@/data/ingredient";
import {
	DataTable,
	Pill,
	TableToolbar,
	readPage,
	readSort,
	type Column,
} from "@/components/ui/data-table";
import AdminIngredientActions from "./AdminIngredientActions";

export const dynamic = "force-dynamic";

const PAGE_SIZE = 20;

type Ingredient = Awaited<ReturnType<typeof getAllIngredient>>["ingredients"][number];

const macro = (value: number | null | undefined, suffix = "g") => (
	<span className="tabular-nums">
		{value ?? 0}
		{suffix}
	</span>
);

const columns: Column<Ingredient>[] = [
	{
		id: "name",
		header: "Ingredient",
		sortKey: "name",
		cell: (i) => (
			<div className="min-w-0">
				<p className="truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{i.name}
				</p>
				{i.brand && (
					<p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{i.brand}
					</p>
				)}
			</div>
		),
	},
	{
		id: "serving",
		header: "Serving",
		align: "center",
		secondary: true,
		cell: (i) => (
			<span className="whitespace-nowrap tabular-nums">
				{i.servingSize}
				{i.servingUnit || "g"}
			</span>
		),
	},
	{ id: "calories", header: "kcal", sortKey: "calories", align: "center", cell: (i) => macro(i.caloriesPer100g, "") },
	{ id: "protein", header: "Protein", sortKey: "protein", align: "center", cell: (i) => macro(i.proteinPer100g) },
	{ id: "carbs", header: "Carbs", align: "center", secondary: true, cell: (i) => macro(i.carbsPer100g) },
	{ id: "fat", header: "Fat", align: "center", secondary: true, cell: (i) => macro(i.fatPer100g) },
	{
		id: "verified",
		header: "Verified",
		align: "center",
		cell: (i) => (i.isVerified ? <Pill tone="good">Yes</Pill> : <Pill>No</Pill>),
	},
	{
		id: "actions",
		header: "",
		align: "right",
		width: "1%",
		cell: (i) => <AdminIngredientActions id={i.id} name={i.name} />,
	},
];

export default async function AdminIngredientsPage({
	searchParams,
}: {
	searchParams: Promise<Record<string, string | undefined>>;
}) {
	const params = await searchParams;
	const page = readPage(params);
	const sort = readSort(params);

	const { ingredients, totalPages, totalCount } = await getAllIngredient(
		params.search || undefined,
		sort.sortBy ?? undefined,
		sort.sortOrder,
		page,
		PAGE_SIZE,
	);

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Moderation</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Ingredients
					</h1>
				</div>
				<Link href="/admin/ingredients/add" className="btn-primary !rounded-2xl">
					Add ingredient
				</Link>
			</header>

			<TableToolbar searchPlaceholder="Search ingredients…" />

			<DataTable
				columns={columns}
				rows={ingredients}
				rowKey={(i) => i.id}
				pathname="/admin/ingredients"
				searchParams={params}
				sort={sort}
				page={{ page, pageSize: PAGE_SIZE, totalCount, totalPages }}
				empty={{
					title: "No ingredients match",
					description: "Try a different search, or add one.",
					action: (
						<Link href="/admin/ingredients/add" className="btn-primary !rounded-2xl">
							Add ingredient
						</Link>
					),
				}}
			/>
		</div>
	);
}
