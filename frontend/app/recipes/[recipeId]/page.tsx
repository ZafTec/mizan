import type { Metadata } from "next";
import { getRecipeById } from "@/data/recipe";
import { getUserOptionalServer } from "@/helper/session";
import Image from "next/image";
import Link from "next/link";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import RecipeActions from "./RecipeActions";

export const dynamic = "force-dynamic";

export async function generateMetadata({
	params,
}: {
	params: Promise<{ recipeId: string }>;
}): Promise<Metadata> {
	const { recipeId } = await params;
	const recipe = await getRecipeById(recipeId);

	if (!recipe) {
		return { title: "Recipe not found - Mizan" };
	}

	return {
		title: `${recipe.title} - Mizan`,
		description:
			recipe.description ||
			`${recipe.nutrition?.caloriesPerServing || 0} kcal · ${recipe.nutrition?.proteinGrams?.toFixed(0) || 0}g protein`,
		openGraph: {
			title: recipe.title || "Recipe",
			description: recipe.description || undefined,
			images: recipe.imageUrl ? [{ url: recipe.imageUrl }] : [],
		},
	};
}

export default async function Page({
	params,
}: {
	params: Promise<{ recipeId: string }>;
}) {
	const { recipeId } = await params;

	const user = await getUserOptionalServer();
	const recipe = await getRecipeById(recipeId);

	if (!recipe) {
		return (
			<div className="min-h-[50vh] flex flex-col items-center justify-center">
				<div className="w-20 h-20 rounded-2xl bg-charcoal-blue-100 dark:bg-charcoal-blue-900 flex items-center justify-center mb-4">
					<i className="ri-restaurant-line text-4xl text-charcoal-blue-400" />
				</div>
				<h2 className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-2">
					Recipe not found
				</h2>
				<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mb-6">
					The recipe you&apos;re looking for doesn&apos;t exist.
				</p>
				<Link href="/recipes" className="btn-primary">
					<i className="ri-arrow-left-line" />
					Back to Recipes
				</Link>
			</div>
		);
	}

	const macrosPerServing = {
		calories: recipe.nutrition?.caloriesPerServing || 0,
		protein: recipe.nutrition?.proteinGrams || 0,
		carbs: recipe.nutrition?.carbsGrams || 0,
		fat: recipe.nutrition?.fatGrams || 0,
		fiber: recipe.nutrition?.fiberGrams || 0,
	};
	const proteinCalRatio = macrosPerServing.calories > 0
		? (macrosPerServing.protein * 4 / macrosPerServing.calories) * 100
		: 0;

	return (
		<div className="max-w-4xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex items-center gap-4">
				<Link
					href="/recipes"
					className="w-10 h-10 rounded-xl bg-charcoal-blue-100 hover:bg-charcoal-blue-200 dark:bg-charcoal-blue-900 dark:hover:bg-charcoal-blue-800 flex items-center justify-center transition-colors"
				>
					<i className="ri-arrow-left-line text-xl text-charcoal-blue-600 dark:text-charcoal-blue-300" />
				</Link>
				<div className="flex-1">
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">{recipe.title}</h1>
					<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 text-sm">
						{recipe.prepTimeMinutes && `${recipe.prepTimeMinutes} min prep`}
						{recipe.prepTimeMinutes && recipe.cookTimeMinutes && " • "}
						{recipe.cookTimeMinutes && `${recipe.cookTimeMinutes} min cook`}
					</p>
				</div>
			</div>

			{/* Image */}
			<div className="card overflow-hidden">
				<div className="relative h-72 sm:h-96">
					<Image
						src={recipe.imageUrl || placeHolderImage}
						alt={recipe.title || "Recipe"}
						fill
						sizes="(max-width: 768px) 100vw, 896px"
						className="object-cover"
					/>
				</div>
			</div>

			{/* Description */}
			{recipe.description && (
				<div className="card p-6">
					<p className="text-charcoal-blue-700 dark:text-charcoal-blue-300 leading-relaxed">{recipe.description}</p>
				</div>
			)}

			{/* Macros Per Serving */}
			<div className="card p-6 bg-charcoal-blue-50/90 dark:bg-charcoal-blue-900/60">
				<div className="flex items-center justify-between mb-4">
					<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Nutrition per Serving</h2>
					<span className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400 bg-white px-3 py-1 rounded-full dark:bg-charcoal-blue-950">
						{recipe.servings} servings
					</span>
				</div>
				<div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
					<div className="text-center p-4 bg-white rounded-xl dark:bg-charcoal-blue-950">
						<div className="w-10 h-10 rounded-xl bg-burnt-peach-100 dark:bg-orange-500/15 flex items-center justify-center mx-auto mb-2">
							<i className="ri-fire-line text-orange-600" />
						</div>
						<p className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							{macrosPerServing.calories}
						</p>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Calories</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl dark:bg-charcoal-blue-950">
						<div className="w-10 h-10 rounded-2xl bg-red-100 dark:bg-red-500/15 flex items-center justify-center mx-auto mb-2">
							<i className="ri-heart-pulse-line text-red-600" />
						</div>
						<p className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							{macrosPerServing.protein}g
						</p>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Protein</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl dark:bg-charcoal-blue-950">
						<div className="w-10 h-10 rounded-xl bg-tuscan-sun-100 dark:bg-amber-500/15 flex items-center justify-center mx-auto mb-2">
							<i className="ri-bread-line text-amber-600" />
						</div>
						<p className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							{macrosPerServing.carbs}g
						</p>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Carbs</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl dark:bg-charcoal-blue-950">
						<div className="w-10 h-10 rounded-xl bg-sandy-brown-100 dark:bg-yellow-500/15 flex items-center justify-center mx-auto mb-2">
							<i className="ri-drop-line text-yellow-600" />
						</div>
						<p className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							{macrosPerServing.fat}g
						</p>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Fat</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl dark:bg-charcoal-blue-950">
						<div className="w-10 h-10 rounded-2xl bg-green-100 dark:bg-green-500/15 flex items-center justify-center mx-auto mb-2">
							<i className="ri-leaf-line text-green-600" />
						</div>
						<p className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							{macrosPerServing.fiber}g
						</p>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Fiber</p>
					</div>
					{proteinCalRatio > 0 && (
						<div className="text-center p-4 bg-white rounded-xl dark:bg-charcoal-blue-950">
							<div className="w-10 h-10 rounded-xl bg-violet-100 dark:bg-violet-500/15 flex items-center justify-center mx-auto mb-2">
								<i className="ri-percent-line text-violet-600" />
							</div>
							<p className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
								{proteinCalRatio.toFixed(0)}%
							</p>
							<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">P/Cal Ratio</p>
						</div>
					)}
				</div>
			</div>

			{/* Ingredients & Instructions Grid */}
			<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
				{/* Ingredients */}
				<div className="card p-6">
					<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2 mb-4">
						<i className="ri-list-check-2 text-brand-500" />
						Ingredients
					</h2>
					{recipe.ingredients && recipe.ingredients.length > 0 ? (
						<ul className="space-y-3">
							{recipe.ingredients.map((item, index) => (
								<li
									key={index}
									className="flex items-center gap-3 p-3 bg-charcoal-blue-50 dark:bg-charcoal-blue-900 rounded-xl"
								>
									<div className="w-8 h-8 rounded-lg bg-brand-100 text-brand-600 flex items-center justify-center text-sm font-medium">
										{index + 1}
									</div>
									<div className="flex-1">
										<span className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">
											{item.foodName || item.ingredientText}
										</span>
									</div>
									{item.amount && (
										<span className="text-charcoal-blue-500 dark:text-charcoal-blue-400 text-sm">
											{item.amount} {item.unit}
										</span>
									)}
								</li>
							))}
						</ul>
					) : (
						<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 text-center py-4">
							No ingredients listed
						</p>
					)}
				</div>

				{/* Instructions */}
				<div className="card p-6">
					<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2 mb-4">
						<i className="ri-file-list-3-line text-brand-500" />
						Instructions
					</h2>
					{recipe.instructions && recipe.instructions.length > 0 ? (
						<ol className="space-y-4">
							{recipe.instructions.map((instruction, index) => (
								<li key={index} className="flex gap-3">
									<div className="w-8 h-8 rounded-lg bg-accent-100 text-accent-600 flex items-center justify-center text-sm font-bold shrink-0">
										{instruction.stepNumber || index + 1}
									</div>
									<p className="text-charcoal-blue-700 dark:text-charcoal-blue-300 pt-1">{instruction.instruction}</p>
								</li>
							))}
						</ol>
					) : (
						<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 text-center py-4">
							No instructions listed
						</p>
					)}
				</div>
			</div>

			{/* Tags */}
			{recipe.tags && recipe.tags.length > 0 && (
				<div className="card p-6">
					<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2 mb-4">
						<i className="ri-price-tag-3-line text-brand-500" />
						Tags
					</h2>
					<div className="flex flex-wrap gap-2">
						{recipe.tags.map((tag, index) => (
							<span
								key={index}
							className="inline-flex items-center px-4 py-2 rounded-full bg-brand-100 text-brand-700 text-sm font-medium dark:bg-brand-900/30 dark:text-brand-300"
							>
								{tag}
							</span>
						))}
					</div>
				</div>
			)}

			{/* Actions: only shown to authenticated users */}
			{user && (
				<RecipeActions
					recipeId={recipeId}
					isOwner={recipe.isOwner || false}
					isFavorited={recipe.isFavorited || false}
				/>
			)}
		</div>
	);
}
