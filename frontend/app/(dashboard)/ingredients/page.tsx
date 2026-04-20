import SortableHeader from "@/components/SortableHeader";
import { getAllIngredient } from "@/data/ingredient";
import Link from "next/link";
import SearchBar from "@/components/IngredientTable/SearchInputField";
import { getUserOptionalServer } from "@/helper/session";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";
import IngredientFilters from "./IngredientFilters";

export const dynamic = 'force-dynamic';

export default async function Page(
	{ searchParams }: { searchParams: Promise<{ [key: string]: string | string[] | undefined }> }
) {
	const user = await getUserOptionalServer();
	const params = await searchParams;
	const { page, sortBy, sortOrder } = parseListParams(params);
	const searchIngredient = params.searchIngredient as string | undefined;
	const minPcal = typeof params.minPcal === "string" ? parseInt(params.minPcal) : 0;
	const { ingredients, totalPages, totalCount } = await getAllIngredient(searchIngredient, sortBy ?? undefined, sortOrder, page, 10, minPcal > 0 ? minPcal : undefined);
	const baseUrl = buildListUrl('/ingredients', { searchIngredient, sortBy, sortOrder, ...(minPcal > 0 ? { minPcal: String(minPcal) } : {}) });

	return (
		<div className="space-y-6 lg:space-y-8" data-testid="ingredient-list">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Catalogue</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Foods
					</h1>
					<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Browse and manage nutritional ingredients.
					</p>
				</div>
				<div className="flex items-center gap-3">
					<SearchBar />
					<IngredientFilters currentMinPcal={minPcal || undefined} />
					{user?.role === "admin" && (
						<Link href="/ingredients/add" className="btn-primary">
							<i className="ri-add-line" />
							Add
						</Link>
					)}
				</div>
			</header>

			<div className="card overflow-hidden">
				<div className="overflow-x-auto">
					<table className="w-full">
						<thead>
							<tr className="border-b border-charcoal-blue-200 bg-charcoal-blue-50 dark:border-white/10 dark:bg-charcoal-blue-900/85">
								<SortableHeader sortKey="name" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="w-52 px-6 py-4 text-left text-sm font-semibold text-charcoal-blue-600 dark:text-charcoal-blue-300">Name</SortableHeader>
								<SortableHeader sortKey="calories" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-4 py-4 text-center text-sm font-semibold text-charcoal-blue-600 dark:text-charcoal-blue-300">Calories</SortableHeader>
								<SortableHeader sortKey="protein" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-4 py-4 text-center text-sm font-semibold text-charcoal-blue-600 dark:text-charcoal-blue-300">Protein</SortableHeader>
								<th className="px-4 py-4 text-center text-sm font-semibold text-charcoal-blue-600 dark:text-charcoal-blue-300">Fat</th>
								<th className="px-4 py-4 text-center text-sm font-semibold text-charcoal-blue-600 dark:text-charcoal-blue-300">Carbs</th>
								<th className="px-4 py-4 text-center text-sm font-semibold text-charcoal-blue-600 dark:text-charcoal-blue-300">Fiber</th>
								<SortableHeader sortKey="proteinCalorieRatio" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-4 py-4 text-center text-sm font-semibold text-violet-600 dark:text-violet-300">P/Cal</SortableHeader>
							</tr>
						</thead>
						<tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
							{!ingredients || ingredients.length === 0 ? (
								<tr>
									<td colSpan={7} className="px-6 py-16 text-center">
										<div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-charcoal-blue-100 dark:bg-charcoal-blue-900/60">
											<i className="ri-leaf-line text-3xl text-charcoal-blue-400 dark:text-charcoal-blue-500" />
										</div>
										<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-2">No ingredients found</h3>
										<p className="mb-4 text-charcoal-blue-500 dark:text-charcoal-blue-400">Try adjusting your search or add a new ingredient</p>
									</td>
								</tr>
							) : (
								ingredients.map((ingredient) => (
									<tr
										key={ingredient.id}
										className="group transition-colors hover:bg-charcoal-blue-50 dark:hover:bg-charcoal-blue-900/60"
									>
										<td className="px-6 py-4">
											<Link
												href={`/ingredients/${ingredient.id}`}
												className="font-medium capitalize text-charcoal-blue-900 transition-colors group-hover:text-brand-600 dark:text-charcoal-blue-100 dark:group-hover:text-brand-300"
											>
												{ingredient.name}
											</Link>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="macro-chip-calories inline-flex items-center rounded-lg px-2.5 py-1 text-sm font-medium">
												{ingredient.caloriesPer100g}
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="macro-chip-protein inline-flex items-center rounded-lg px-2.5 py-1 text-sm font-medium">
												{ingredient.proteinPer100g}g
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="macro-chip-fat inline-flex items-center rounded-lg px-2.5 py-1 text-sm font-medium">
												{ingredient.fatPer100g}g
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="macro-chip-carbs inline-flex items-center rounded-lg px-2.5 py-1 text-sm font-medium">
												{ingredient.carbsPer100g}g
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="inline-flex items-center rounded-lg bg-green-50 px-2.5 py-1 text-sm font-medium text-green-700 dark:bg-green-500/12 dark:text-green-300">
												{ingredient.fiberPer100g ?? 0}g
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="inline-flex items-center rounded-lg bg-violet-50 px-2.5 py-1 text-sm font-medium text-violet-700 dark:bg-violet-500/12 dark:text-violet-300">
												{ingredient.proteinCalorieRatio.toFixed(0)}%
											</span>
										</td>
									</tr>
								))
							)}
						</tbody>
					</table>
				</div>
			</div>

			<Pagination
				currentPage={page}
				totalPages={totalPages}
				totalCount={totalCount}
				pageSize={10}
				baseUrl={baseUrl}
			/>
		</div>
	);
}
