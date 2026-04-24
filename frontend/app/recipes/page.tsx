import type { Metadata } from "next";
import Link from "next/link";
import Image from "next/image";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import { getAllRecipes } from "@/data/recipe";
import { getUserOptionalServer } from "@/helper/session";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";
import Pagination from "@/components/Pagination";
import RecipeFilters from "./RecipeFilters";
import { AppFeatureIllustration } from "@/components/illustrations/AppFeatureIllustration";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
	title: "Recipes | Mizan",
	description:
		"Browse healthy, nutrition-tracked recipes. Find meals by macros, calories, and dietary goals.",
};

export default async function RecipesPage({
	searchParams,
}: {
	searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
	const params = await searchParams;
	const { page, sortBy, sortOrder } = parseListParams(params);
	const searchTerm = typeof params.search === "string" ? params.search : undefined;
	const rawTags = params.tags;
	const tags: string[] = rawTags
		? Array.isArray(rawTags)
			? rawTags
			: [rawTags]
		: [];
	const { recipes, totalPages, totalCount } = await getAllRecipes(
		searchTerm,
		page,
		10,
		false,
		sortBy ?? undefined,
		sortOrder,
		tags.length > 0 ? tags : undefined,
	);
	const user = await getUserOptionalServer();

	const listUrlParams: Record<string, string> = {};
	if (searchTerm) listUrlParams.search = searchTerm;
	if (sortBy) listUrlParams.sortBy = sortBy;
	if (sortOrder) listUrlParams.sortOrder = sortOrder;
	const baseUrl = buildListUrl("/recipes", listUrlParams);

	let favoriteCount = 0;
	if (user) {
		const { totalCount: favTotal } = await getAllRecipes(undefined, 1, 0, true);
		favoriteCount = favTotal;
	}

	return (
		<div className="space-y-8" data-testid="recipe-list">
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">Recipes</h1>
				</div>
				{user && (
					<Link href="/recipes/add" className="btn-primary">
						<i className="ri-add-line" />
						Create Recipe
					</Link>
				)}
			</div>

			{/* Quick Stats */}
			<div className="grid grid-cols-1 md:grid-cols-3 gap-4">
				{user ? (
					<>
						<Link href="/recipes/favorites" className="card-hover p-5 group">
							<div className="flex items-center gap-4">
								<div className="w-12 h-12 rounded-2xl bg-rose-600 flex items-center justify-center shadow-lg text-white">
									<i className="ri-heart-3-line text-xl" />
								</div>
								<div className="flex-1">
									<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Favorites</h3>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{favoriteCount} saved</p>
								</div>
								<i className="ri-arrow-right-s-line text-xl text-charcoal-blue-400 dark:text-charcoal-blue-500 group-hover:text-brand-500 transition-colors" />
							</div>
						</Link>
						<div className="card p-5">
							<div className="flex items-center gap-4">
								<div className="w-12 h-12 rounded-2xl bg-brand-600 flex items-center justify-center shadow-lg text-white">
									<i className="ri-restaurant-line text-xl" />
								</div>
								<div className="flex-1">
									<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Community</h3>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{totalCount} recipes</p>
								</div>
							</div>
						</div>
						<div className="card p-5">
							<div className="flex items-center gap-4">
								<div className="w-12 h-12 rounded-2xl bg-accent-600 flex items-center justify-center shadow-lg text-white">
									<i className="ri-line-chart-line text-xl" />
								</div>
								<div className="flex-1">
									<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">This Page</h3>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{recipes.length} shown</p>
								</div>
							</div>
						</div>
					</>
				) : (
					<>
						<div className="card p-5">
							<div className="flex items-center gap-4">
								<div className="w-12 h-12 rounded-2xl bg-brand-600 flex items-center justify-center shadow-lg text-white">
									<i className="ri-restaurant-line text-xl" />
								</div>
								<div className="flex-1">
									<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Recipes</h3>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{totalCount} available</p>
								</div>
							</div>
						</div>
						<div className="card p-5">
							<div className="flex items-center gap-4">
								<div className="w-12 h-12 rounded-2xl bg-accent-600 flex items-center justify-center shadow-lg text-white">
									<i className="ri-fire-line text-xl" />
								</div>
								<div className="flex-1">
									<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Nutrition Tracked</h3>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">Calories &amp; macros</p>
								</div>
							</div>
						</div>
						<Link href="/register" className="card-hover p-5 group">
							<div className="flex items-center gap-4">
								<div className="w-12 h-12 rounded-2xl bg-rose-600 flex items-center justify-center shadow-lg text-white">
									<i className="ri-heart-3-line text-xl" />
								</div>
								<div className="flex-1">
									<h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Sign Up</h3>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">Save favorites &amp; create</p>
								</div>
								<i className="ri-arrow-right-s-line text-xl text-charcoal-blue-400 dark:text-charcoal-blue-500 group-hover:text-brand-500 transition-colors" />
							</div>
						</Link>
					</>
				)}
			</div>

			{/* All Recipes */}
			<div className="card p-6">
				<div className="flex items-center justify-between mb-6">
					<h2 className="section-title">All Recipes</h2>
				</div>

				<div className="mb-6">
					<RecipeFilters
						currentSearch={searchTerm}
						currentTags={tags}
						currentSortBy={sortBy ?? undefined}
						currentSortOrder={sortOrder}
					/>
				</div>

				{recipes.length > 0 ? (
					<div className="space-y-4">
						{recipes.map((recipe) => (
							<Link
								key={recipe.id}
								href={`/recipes/${recipe.id}`}
								className="group flex flex-col sm:flex-row bg-charcoal-blue-50 hover:bg-charcoal-blue-100 dark:bg-charcoal-blue-900 dark:hover:bg-charcoal-blue-800 rounded-2xl overflow-hidden transition-all duration-300"
							>
								<div className="sm:w-48 h-48 sm:h-auto relative bg-charcoal-blue-200 dark:bg-charcoal-blue-900">
									<Image
										src={recipe.imageUrl || placeHolderImage}
										alt={recipe.title || "Recipe"}
										fill
										sizes="(max-width: 640px) 100vw, 192px"
										className="object-cover group-hover:scale-105 transition-transform duration-300"
									/>
								</div>
								<div className="flex-1 p-5">
									<div className="flex justify-between items-start gap-4">
										<div className="flex-1">
											<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 group-hover:text-brand-600 transition-colors mb-2">
												{recipe.title}
											</h3>
											<p className="text-charcoal-blue-600 dark:text-charcoal-blue-300 text-sm line-clamp-2 mb-4">
												{recipe.description || "A delicious recipe"}
											</p>
										</div>
									</div>
									<div className="flex flex-wrap gap-2 mt-4">
										<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-orange-50 text-orange-700 text-sm font-medium dark:bg-orange-500/15 dark:text-orange-300">
											<i className="ri-fire-line" />
											{recipe.nutrition?.caloriesPerServing || 0} kcal
										</span>
										<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-red-50 text-red-700 text-sm font-medium dark:bg-red-500/15 dark:text-red-300">
											<i className="ri-heart-pulse-line" />
											{recipe.nutrition?.proteinGrams?.toFixed(0) || 0}g protein
										</span>
										<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-amber-50 text-amber-700 text-sm font-medium dark:bg-amber-500/15 dark:text-amber-300">
											<i className="ri-bread-line" />
											{recipe.nutrition?.carbsGrams?.toFixed(0) || 0}g carbs
										</span>
										<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-yellow-50 text-yellow-700 text-sm font-medium dark:bg-yellow-500/15 dark:text-yellow-300">
											<i className="ri-drop-line" />
											{recipe.nutrition?.fatGrams?.toFixed(0) || 0}g fat
										</span>
										{recipe.nutrition?.proteinCalorieRatio != null && recipe.nutrition.proteinCalorieRatio > 0 && (
											<span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-indigo-50 text-indigo-700 text-xs font-semibold dark:bg-indigo-500/15 dark:text-indigo-300">
												{recipe.nutrition.proteinCalorieRatio.toFixed(0)}% P/Cal
											</span>
										)}
									</div>
								</div>
							</Link>
						))}
					</div>
				) : (
					<div className="text-center py-16 flex flex-col items-center">
						<div className="mb-6 w-full max-w-[18rem] opacity-95 drop-shadow-md">
							<AppFeatureIllustration variant="recipes" className="h-auto w-full" />
						</div>
						<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-2">
							No recipes yet
						</h3>
						<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mb-6">Be the first to add a delicious recipe!</p>
						{user && (
							<Link href="/recipes/add" className="btn-primary">
								<i className="ri-add-line" />
								Create Recipe
							</Link>
						)}
					</div>
				)}

				<Pagination
					currentPage={page}
					totalPages={totalPages}
					totalCount={totalCount}
					pageSize={10}
					baseUrl={baseUrl}
				/>
			</div>

			{/* Create Recipe CTA */}
			{user && (
				<div className="card overflow-hidden">
					<div className="relative p-8 bg-charcoal-blue-50/90 dark:bg-charcoal-blue-900/60">
						<div className="absolute top-0 right-0 w-64 h-64 bg-charcoal-blue-200/40 dark:bg-charcoal-blue-800/40 rounded-full blur-3xl -translate-y-1/2 translate-x-1/2" />
						<div className="relative flex flex-col sm:flex-row items-center gap-6 text-center sm:text-left">
							<div className="w-16 h-16 rounded-2xl bg-accent-600 flex items-center justify-center shadow-lg shadow-accent-500/30">
								<i className="ri-restaurant-2-line text-3xl text-white" />
							</div>
							<div className="flex-1">
								<h3 className="text-xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-1">
									Create Your Own Recipe
								</h3>
								<p className="text-charcoal-blue-600 dark:text-charcoal-blue-300">
									Share your favorite healthy meals with the community
								</p>
							</div>
							<Link href="/recipes/add" className="btn-secondary whitespace-nowrap">
								<i className="ri-add-line" />
								Add Recipe
							</Link>
						</div>
					</div>
				</div>
			)}
		</div>
	);
}
