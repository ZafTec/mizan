import Link from "next/link";
import Image from "next/image";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import { getAllRecipes } from "@/data/recipe";
import { getUserOptionalServer } from "@/helper/session";

import Pagination from "@/components/Pagination";

export const dynamic = 'force-dynamic';

export default async function Page({
	searchParams,
}: {
	searchParams: Promise<{ page?: string }>;
}) {
	const params = await searchParams;
	const page = Number(params.page) || 1;
	// Fetch recipes for the current page
	const { recipes, totalPages, totalCount } = await getAllRecipes(undefined, page, 10);
	const user = await getUserOptionalServer();

	let favoriteCount = 0;
	if (user) {
		const { totalCount: favTotal } = await getAllRecipes(undefined, 1, 0, true);
		favoriteCount = favTotal;
	}

	return (
		<div className="space-y-8">
			{/* Page Header */}
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<h1 className="text-2xl font-bold text-slate-900">Recipes</h1>
					<p className="text-slate-500 mt-1">Discover and create delicious healthy meals</p>
				</div>
				{user && (
					<Link href="/recipes/add" className="btn-primary">
						<i className="ri-add-line" />
						Create Recipe
					</Link>
				)}
			</div>

			{/* Quick Collections */}
			<div className="grid grid-cols-1 md:grid-cols-3 gap-4">
				{[
					{ name: "Favorites", icon: "ri-heart-3-line", count: favoriteCount, color: "from-rose-400 to-rose-600" },
					{ name: "My Recipes", icon: "ri-restaurant-line", count: totalCount, color: "from-brand-400 to-brand-600" },
					{ name: "Recent", icon: "ri-history-line", count: Math.min(recipes.length, 5), color: "from-violet-400 to-violet-600" },
				].map((collection) => (
					<div key={collection.name} className="card-hover p-5 group cursor-pointer">
						<div className="flex items-center gap-4">
							<div className={`w-12 h-12 rounded-2xl bg-gradient-to-br ${collection.color} flex items-center justify-center shadow-lg`}>
								<i className={`${collection.icon} text-xl text-white`} />
							</div>
							<div className="flex-1">
								<h3 className="font-semibold text-slate-900">{collection.name}</h3>
								<p className="text-sm text-slate-500">{collection.count} recipes</p>
							</div>
							<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
						</div>
					</div>
				))}
			</div>

			{/* Create Recipe CTA */}
			{user && (
				<div className="card overflow-hidden">
					<div className="relative p-8 bg-gradient-to-br from-accent-50 to-brand-50">
						<div className="absolute top-0 right-0 w-64 h-64 bg-brand-200/30 rounded-full blur-3xl -translate-y-1/2 translate-x-1/2" />
						<div className="relative flex flex-col sm:flex-row items-center gap-6 text-center sm:text-left">
							<div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-accent-400 to-accent-600 flex items-center justify-center shadow-lg shadow-accent-500/30">
								<i className="ri-restaurant-2-line text-3xl text-white" />
							</div>
							<div className="flex-1">
								<h3 className="text-xl font-bold text-slate-900 mb-1">Create Your Own Recipe</h3>
								<p className="text-slate-600">Share your favorite healthy meals with the community</p>
							</div>
							<Link href="/recipes/add" className="btn-secondary whitespace-nowrap">
								<i className="ri-add-line" />
								Add Recipe
							</Link>
						</div>
					</div>
				</div>
			)}

			{/* All Recipes */}
			<div className="card p-6">
				<div className="flex items-center justify-between mb-6">
					<h2 className="section-title">All Recipes</h2>
					<div className="flex items-center gap-2">
						<button className="btn-secondary text-sm px-3 py-1.5">
							<i className="ri-filter-3-line" />
							Filter
						</button>
						<button className="btn-secondary text-sm px-3 py-1.5">
							<i className="ri-sort-desc" />
							Sort
						</button>
					</div>
				</div>

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
											<h3 className="text-lg font-semibold text-slate-900 group-hover:text-brand-600 transition-colors mb-2">
												{recipe.title}
											</h3>
											<p className="text-slate-600 text-sm line-clamp-2 mb-4">
												{recipe.description || "A delicious recipe"}
											</p>
										</div>
									</div>

									{/* Macro Pills */}
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
						<div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto mb-4">
							<i className="ri-restaurant-line text-3xl text-slate-400" />
						</div>
						<h3 className="text-lg font-semibold text-slate-900 mb-2">No recipes yet</h3>
						<p className="text-slate-500 mb-6">Be the first to add a delicious recipe!</p>
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
					baseUrl="/recipes"
				/>
			</div>
		</div>
	);
}
