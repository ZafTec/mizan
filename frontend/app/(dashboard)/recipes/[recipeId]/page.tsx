import { getRecipeById } from "@/data/recipe";
import { getUserOptionalServer } from "@/helper/session";
import Image from "next/image";
import Link from "next/link";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import RecipeActions from "./RecipeActions";

export const dynamic = 'force-dynamic';

export default async function Page({ params }: { params: Promise<{ recipeId: string }> }) {
	const { recipeId } = await params;

	await getUserOptionalServer();
	const recipe = await getRecipeById(recipeId);

	if (!recipe) {
		return (
			<div className="min-h-[50vh] flex flex-col items-center justify-center">
				<div className="w-20 h-20 rounded-2xl bg-slate-100 flex items-center justify-center mb-4">
					<i className="ri-restaurant-line text-4xl text-slate-400" />
				</div>
				<h2 className="text-xl font-semibold text-slate-900 mb-2">Recipe not found</h2>
				<p className="text-slate-500 mb-6">The recipe you&apos;re looking for doesn&apos;t exist.</p>
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

	return (
		<div className="max-w-4xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex items-center gap-4">
				<Link href="/recipes" className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-slate-600" />
				</Link>
				<div className="flex-1">
					<h1 className="text-2xl font-bold text-slate-900">{recipe.title}</h1>
					<p className="text-slate-500 text-sm">
						{recipe.prepTimeMinutes && `${recipe.prepTimeMinutes} min prep`}
						{recipe.prepTimeMinutes && recipe.cookTimeMinutes && ' • '}
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
						className="object-cover"
					/>
				</div>
			</div>

			{/* Description */}
			{recipe.description && (
				<div className="card p-6">
					<p className="text-slate-700 leading-relaxed">{recipe.description}</p>
				</div>
			)}

			{/* Macros Per Serving */}
			<div className="card p-6 bg-gradient-to-br from-brand-50 to-accent-50">
				<div className="flex items-center justify-between mb-4">
					<h2 className="font-semibold text-slate-900">Nutrition per Serving</h2>
					<span className="text-sm text-slate-500 bg-white px-3 py-1 rounded-full">{recipe.servings} servings</span>
				</div>
				<div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-fire-line text-orange-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{macrosPerServing.calories}</p>
						<p className="text-xs text-slate-500">Calories</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-red-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-heart-pulse-line text-red-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{macrosPerServing.protein}g</p>
						<p className="text-xs text-slate-500">Protein</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-bread-line text-amber-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{macrosPerServing.carbs}g</p>
						<p className="text-xs text-slate-500">Carbs</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-yellow-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-drop-line text-yellow-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{macrosPerServing.fat}g</p>
						<p className="text-xs text-slate-500">Fat</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-green-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-leaf-line text-green-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{macrosPerServing.fiber}g</p>
						<p className="text-xs text-slate-500">Fiber</p>
					</div>
				</div>
			</div>

			{/* Ingredients & Instructions Grid */}
			<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
				{/* Ingredients */}
				<div className="card p-6">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2 mb-4">
						<i className="ri-list-check-2 text-brand-500" />
						Ingredients
					</h2>
					{recipe.ingredients && recipe.ingredients.length > 0 ? (
						<ul className="space-y-3">
							{recipe.ingredients.map((item, index) => (
								<li key={index} className="flex items-center gap-3 p-3 bg-slate-50 rounded-xl">
									<div className="w-8 h-8 rounded-lg bg-brand-100 text-brand-600 flex items-center justify-center text-sm font-medium">
										{index + 1}
									</div>
									<div className="flex-1">
										<span className="font-medium text-slate-900">{item.foodName || item.ingredientText}</span>
									</div>
									{item.amount && (
										<span className="text-slate-500 text-sm">{item.amount} {item.unit}</span>
									)}
								</li>
							))}
						</ul>
					) : (
						<p className="text-slate-500 text-center py-4">No ingredients listed</p>
					)}
				</div>

				{/* Instructions */}
				<div className="card p-6">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2 mb-4">
						<i className="ri-file-list-3-line text-brand-500" />
						Instructions
					</h2>
					{recipe.instructions && recipe.instructions.length > 0 ? (
						<ol className="space-y-4">
							{recipe.instructions.map((instruction, index) => (
								<li key={index} className="flex gap-3">
									<div className="w-8 h-8 rounded-lg bg-accent-100 text-accent-600 flex items-center justify-center text-sm font-bold flex-shrink-0">
										{instruction.stepNumber || index + 1}
									</div>
									<p className="text-slate-700 pt-1">{instruction.instruction}</p>
								</li>
							))}
						</ol>
					) : (
						<p className="text-slate-500 text-center py-4">No instructions listed</p>
					)}
				</div>
			</div>

			{/* Tags */}
			{recipe.tags && recipe.tags.length > 0 && (
				<div className="card p-6">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2 mb-4">
						<i className="ri-price-tag-3-line text-brand-500" />
						Tags
					</h2>
					<div className="flex flex-wrap gap-2">
						{recipe.tags.map((tag, index) => (
							<span
								key={index}
								className="inline-flex items-center px-4 py-2 rounded-full bg-gradient-to-r from-brand-100 to-accent-100 text-brand-700 text-sm font-medium"
							>
								{tag}
							</span>
						))}
					</div>
				</div>
			)}

			{/* Actions */}
			<RecipeActions
				recipeId={recipeId}
				isOwner={recipe.isOwner || false}
				isFavorited={recipe.isFavorited || false}
			/>
		</div>
	);
}
