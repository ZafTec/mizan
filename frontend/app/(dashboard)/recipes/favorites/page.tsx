import Link from "next/link";
import Image from "next/image";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import { getAllRecipes } from "@/data/recipe";
import { getUserServer } from "@/helper/session";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";
import Pagination from "@/components/Pagination";

export const dynamic = 'force-dynamic';

export default async function FavoritesPage({
	searchParams,
}: {
	searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
	await getUserServer();
	const params = await searchParams;
	const { page } = parseListParams(params);
	const { recipes, totalPages, totalCount } = await getAllRecipes(undefined, page, 10, true);
	const baseUrl = buildListUrl('/recipes/favorites', {});

	return (
		<div className="space-y-8">
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div className="flex items-center gap-4">
					<Link href="/recipes" className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
						<i className="ri-arrow-left-line text-xl text-slate-600" />
					</Link>
					<div>
						<h1 className="text-2xl font-bold text-slate-900">Favorite Recipes</h1>
						<p className="text-slate-500 mt-1">{totalCount} saved recipes</p>
					</div>
				</div>
			</div>

			<div className="card p-6">
				{recipes.length > 0 ? (
					<div className="space-y-4">
						{recipes.map((recipe) => (
							<Link
								key={recipe.id}
								href={`/recipes/${recipe.id}`}
								className="group flex flex-col sm:flex-row bg-slate-50 hover:bg-slate-100 rounded-2xl overflow-hidden transition-all duration-300"
							>
								<div className="sm:w-48 h-48 sm:h-auto relative bg-slate-200">
									<Image
										src={recipe.imageUrl || placeHolderImage}
										alt={recipe.title || "Recipe"}
										fill
										className="object-cover group-hover:scale-105 transition-transform duration-300"
									/>
								</div>
								<div className="flex-1 p-5">
									<div className="flex justify-between items-start gap-4">
										<div className="flex-1">
											<div className="flex items-center gap-2 mb-2">
												<h3 className="text-lg font-semibold text-slate-900 group-hover:text-brand-600 transition-colors">
													{recipe.title}
												</h3>
												<i className="ri-heart-3-fill text-rose-500" />
											</div>
											<p className="text-slate-600 text-sm line-clamp-2 mb-4">
												{recipe.description || "A delicious recipe"}
											</p>
										</div>
									</div>
									<div className="flex flex-wrap gap-2 mt-4">
										<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-orange-50 text-orange-700 text-sm font-medium">
											<i className="ri-fire-line" />
											{recipe.nutrition?.caloriesPerServing || 0} kcal
										</span>
										<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-red-50 text-red-700 text-sm font-medium">
											<i className="ri-heart-pulse-line" />
											{recipe.nutrition?.proteinGrams?.toFixed(0) || 0}g protein
										</span>
										<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-amber-50 text-amber-700 text-sm font-medium">
											<i className="ri-bread-line" />
											{recipe.nutrition?.carbsGrams?.toFixed(0) || 0}g carbs
										</span>
										<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-yellow-50 text-yellow-700 text-sm font-medium">
											<i className="ri-drop-line" />
											{recipe.nutrition?.fatGrams?.toFixed(0) || 0}g fat
										</span>
									</div>
								</div>
							</Link>
						))}
					</div>
				) : (
					<div className="text-center py-16">
						<div className="w-16 h-16 rounded-2xl bg-rose-100 flex items-center justify-center mx-auto mb-4">
							<i className="ri-heart-3-line text-3xl text-rose-400" />
						</div>
						<h3 className="text-lg font-semibold text-slate-900 mb-2">No favorites yet</h3>
						<p className="text-slate-500 mb-6">Save recipes you love by clicking the heart icon</p>
						<Link href="/recipes" className="btn-primary">
							<i className="ri-restaurant-line" />
							Browse Recipes
						</Link>
					</div>
				)}

				{totalPages > 1 && (
					<Pagination
						currentPage={page}
						totalPages={totalPages}
						totalCount={totalCount}
						pageSize={10}
						baseUrl={baseUrl}
					/>
				)}
			</div>
		</div>
	);
}
