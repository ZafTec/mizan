import { getAllRecipes } from "@/data/recipe";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";

export const dynamic = "force-dynamic";

export default async function AdminRecipesPage({
	searchParams,
}: {
	searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
	const params = await searchParams;
	const { page, sortBy, sortOrder } = parseListParams(params);
	const searchTerm = params.search as string | undefined;

	const { recipes, totalPages, totalCount } = await getAllRecipes(
		searchTerm,
		page,
		20,
		false,
		sortBy ?? undefined,
		sortOrder,
	);

	const baseUrl = buildListUrl("/admin/recipes", { search: searchTerm });

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Moderation</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Manage recipes
					</h1>
					<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Browse and moderate community recipes.
					</p>
				</div>
			</header>

			<div className="flex flex-col md:flex-row gap-4 items-center justify-between">
				<form className="relative w-full md:w-96">
					<i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-charcoal-blue-400" />
					<input
						name="search"
						type="search"
						placeholder="Search recipes..."
						defaultValue={searchTerm}
						className="input pl-10 h-11"
					/>
				</form>
				<div className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					<span className="font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">{totalCount}</span> recipes
				</div>
			</div>

			<div className="card overflow-hidden">
				<div className="overflow-x-auto">
					<table className="w-full text-left border-collapse">
						<thead>
							<tr className="border-b border-charcoal-blue-100 bg-charcoal-blue-50/50 dark:border-white/10 dark:bg-charcoal-blue-900/60">
								<th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Title</th>
								<th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Servings</th>
								<th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Calories</th>
								<th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Protein</th>
								<th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Visibility</th>
								<th className="px-6 py-4 text-right text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Actions</th>
							</tr>
						</thead>
						<tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
							{recipes.length === 0 ? (
								<tr>
									<td colSpan={6} className="px-6 py-12 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
										No recipes found.
									</td>
								</tr>
							) : (
								recipes.map((recipe) => (
									<tr key={recipe.id} className="group transition-colors hover:bg-charcoal-blue-50/50 dark:hover:bg-charcoal-blue-900/60">
										<td className="px-6 py-4">
											<a href={`/recipes/${recipe.id}`} className="text-sm font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100 group-hover:text-brand-600 dark:group-hover:text-brand-300 transition-colors">
												{recipe.title}
											</a>
											{recipe.description && (
												<div className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400 line-clamp-1">{recipe.description}</div>
											)}
										</td>
										<td className="px-6 py-4 text-center text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
											{recipe.servings || "—"}
										</td>
										<td className="px-6 py-4 text-center">
											<span className="text-sm font-bold text-orange-600">
												{recipe.nutrition?.caloriesPerServing || 0}
											</span>
										</td>
										<td className="px-6 py-4 text-center">
											<span className="text-sm font-bold text-red-600">
												{recipe.nutrition?.proteinGrams?.toFixed(0) || 0}g
											</span>
										</td>
										<td className="px-6 py-4 text-center">
											{recipe.isPublic ? (
												<span className="inline-flex items-center gap-1 rounded-full border border-emerald-100 bg-emerald-50 px-2 py-0.5 text-xs font-bold text-emerald-600 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300">
													<i className="ri-global-line" />
													Public
												</span>
											) : (
												<span className="inline-flex items-center gap-1 rounded-full border border-charcoal-blue-200 bg-charcoal-blue-100 px-2 py-0.5 text-xs font-bold text-charcoal-blue-500 dark:border-white/10 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300">
													<i className="ri-lock-line" />
													Private
												</span>
											)}
										</td>
										<td className="px-6 py-4 text-right">
											<a href={`/recipes/${recipe.id}`} className="text-sm text-brand-600 hover:text-brand-700 dark:text-brand-400 dark:hover:text-brand-300">
												View
											</a>
										</td>
									</tr>
								))
							)}
						</tbody>
					</table>
				</div>
			</div>

			{totalPages > 1 && (
				<Pagination
					currentPage={page}
					totalPages={totalPages}
					totalCount={totalCount}
					pageSize={20}
					baseUrl={baseUrl}
				/>
			)}
		</div>
	);
}
