import AddMealFromRecipe from "@/components/AddMealFromRecipe";
import { getRecipeById } from "@/data/recipe";
import Link from "next/link";

export default async function Page({ params }: { params: Promise<{ recipeId: string }> }) {
	const { recipeId } = await params;
	const recipe = await getRecipeById(recipeId);

	if (!recipe) {
		return (
			<div className="min-h-[50vh] flex flex-col items-center justify-center">
				<div className="w-20 h-20 rounded-2xl bg-slate-100 flex items-center justify-center mb-4">
					<i className="ri-restaurant-line text-4xl text-slate-400" />
				</div>
				<h2 className="text-xl font-semibold text-slate-900 mb-2">Recipe not found</h2>
				<p className="text-slate-500 mb-6">The recipe you&apos;re trying to log doesn&apos;t exist.</p>
				<Link href="/recipes" className="btn-primary">
					<i className="ri-arrow-left-line" />
					Browse Recipes
				</Link>
			</div>
		);
	}

	const macros = {
		calories: recipe.nutrition?.caloriesPerServing || 0,
		protein: recipe.nutrition?.proteinGrams || 0,
		carbs: recipe.nutrition?.carbsGrams || 0,
		fat: recipe.nutrition?.fatGrams || 0,
		fiber: recipe.nutrition?.fiberGrams || 0,
	};

	return (
		<div className="max-w-3xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex items-center gap-4">
				<Link href={`/recipes/${recipeId}`} className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-slate-600" />
				</Link>
				<div>
					<h1 className="text-2xl font-bold text-slate-900">Log from Recipe</h1>
					<p className="text-slate-500">Quick-log nutrition from &quot;{recipe.title}&quot;</p>
				</div>
			</div>

			{/* Recipe Info Card */}
			<div className="card p-6 bg-linear-to-br from-brand-50 to-accent-50 border-none">
				<div className="flex items-center gap-4 mb-6">
					<div className="w-14 h-14 rounded-2xl bg-white shadow-sm flex items-center justify-center">
						<i className="ri-restaurant-2-line text-2xl text-brand-600" />
					</div>
					<div>
						<h2 className="text-lg font-bold text-slate-900">{recipe.title}</h2>
						<p className="text-sm text-slate-600">Calculated per serving ({recipe.servings} total)</p>
					</div>
				</div>
				<div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
					<div className="text-center p-3 bg-white/80 backdrop-blur-sm rounded-xl shadow-sm">
						<p className="text-xl font-bold text-orange-600">{macros.calories.toFixed(0)}</p>
						<p className="text-[10px] uppercase tracking-wider font-bold text-slate-400 mt-1">Calories</p>
					</div>
					<div className="text-center p-3 bg-white/80 backdrop-blur-sm rounded-xl shadow-sm">
						<p className="text-xl font-bold text-red-600">{macros.protein.toFixed(1)}g</p>
						<p className="text-[10px] uppercase tracking-wider font-bold text-slate-400 mt-1">Protein</p>
					</div>
					<div className="text-center p-3 bg-white/80 backdrop-blur-sm rounded-xl shadow-sm">
						<p className="text-xl font-bold text-amber-600">{macros.carbs.toFixed(1)}g</p>
						<p className="text-[10px] uppercase tracking-wider font-bold text-slate-400 mt-1">Carbs</p>
					</div>
					<div className="text-center p-3 bg-white/80 backdrop-blur-sm rounded-xl shadow-sm">
						<p className="text-xl font-bold text-yellow-600">{macros.fat.toFixed(1)}g</p>
						<p className="text-[10px] uppercase tracking-wider font-bold text-slate-400 mt-1">Fat</p>
					</div>
					<div className="text-center p-3 bg-white/80 backdrop-blur-sm rounded-xl shadow-sm">
						<p className="text-xl font-bold text-green-600">{macros.fiber.toFixed(1)}g</p>
						<p className="text-[10px] uppercase tracking-wider font-bold text-slate-400 mt-1">Fiber</p>
					</div>
				</div>
			</div>

			{/* Form Component */}
			<AddMealFromRecipe recipeId={recipeId} name={recipe.title || ""} macros={macros} />
		</div>
	);
}
