import SortableHeader from "@/components/SortableHeader";
import { getAllIngredient } from "@/data/ingredient";
import Link from "next/link";
import SearchBar from "@/components/IngredientTable/SearchInputField";
import { getUserOptionalServer } from "@/helper/session";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";

export const dynamic = 'force-dynamic';

export default async function Page(
	{ searchParams }: { searchParams: Promise<{ [key: string]: string | string[] | undefined }> }
) {
	const user = await getUserOptionalServer();
	const params = await searchParams;
	const { page, sortBy, sortOrder } = parseListParams(params);
	const searchIngredient = params.searchIngredient as string | undefined;
	const { ingredients, totalPages, totalCount } = await getAllIngredient(searchIngredient, sortBy ?? undefined, sortOrder, page, 10);
	const baseUrl = buildListUrl('/ingredients', { searchIngredient, sortBy, sortOrder });

	return (
		<div className="space-y-6" data-testid="ingredient-list">
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Ingredients</h1>
					<p className="text-slate-500 dark:text-slate-400 mt-1">Browse and manage nutritional ingredients</p>
				</div>
				<div className="flex items-center gap-3">
					<SearchBar />
					{user && (
						<Link href="/ingredients/add" className="btn-primary">
							<i className="ri-add-line" />
							Add
						</Link>
					)}
				</div>
			</div>

			<div className="card overflow-hidden">
				<div className="overflow-x-auto">
					<table className="w-full">
						<thead>
							<tr className="bg-slate-50 border-b border-slate-200">
								<SortableHeader sortKey="name" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="w-52 text-left px-6 py-4 text-sm font-semibold text-slate-600">Name</SortableHeader>
								<SortableHeader sortKey="calories" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="text-center px-4 py-4 text-sm font-semibold text-slate-600">Calories</SortableHeader>
								<SortableHeader sortKey="protein" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="text-center px-4 py-4 text-sm font-semibold text-slate-600">Protein</SortableHeader>
								<th className="text-center px-4 py-4 text-sm font-semibold text-slate-600">Fat</th>
								<th className="text-center px-4 py-4 text-sm font-semibold text-slate-600">Carbs</th>
								<th className="text-center px-4 py-4 text-sm font-semibold text-slate-600">Fiber</th>
							</tr>
						</thead>
						<tbody className="divide-y divide-slate-100">
							{!ingredients || ingredients.length === 0 ? (
								<tr>
									<td colSpan={6} className="px-6 py-16 text-center">
										<div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto mb-4">
											<i className="ri-leaf-line text-3xl text-slate-400" />
										</div>
										<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No ingredients found</h3>
										<p className="text-slate-500 mb-4">Try adjusting your search or add a new ingredient</p>
									</td>
								</tr>
							) : (
								ingredients.map((ingredient) => (
									<tr
										key={ingredient.id}
										className="hover:bg-slate-50 transition-colors group"
									>
										<td className="px-6 py-4">
											<Link
												href={`/ingredients/${ingredient.id}`}
												className="font-medium text-slate-900 capitalize group-hover:text-brand-600 transition-colors"
											>
												{ingredient.name}
											</Link>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="inline-flex items-center px-2.5 py-1 rounded-lg bg-orange-50 text-orange-700 text-sm font-medium">
												{ingredient.caloriesPer100g}
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="inline-flex items-center px-2.5 py-1 rounded-lg bg-red-50 text-red-700 text-sm font-medium">
												{ingredient.proteinPer100g}g
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="inline-flex items-center px-2.5 py-1 rounded-lg bg-yellow-50 text-yellow-700 text-sm font-medium">
												{ingredient.fatPer100g}g
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="inline-flex items-center px-2.5 py-1 rounded-lg bg-amber-50 text-amber-700 text-sm font-medium">
												{ingredient.carbsPer100g}g
											</span>
										</td>
										<td className="px-4 py-4 text-center">
											<span className="inline-flex items-center px-2.5 py-1 rounded-lg bg-green-50 text-green-700 text-sm font-medium">
												{ingredient.fiberPer100g ?? 0}g
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
