import { getIngredientById } from "@/data/ingredient";
import { getUserOptionalServer } from "@/helper/session";
import Link from "next/link";
import DeleteIngredientButton from "./DeleteIngredientButton";

export default async function Page({ params }: { params: Promise<{ ingredientId: string }> }) {
	const { ingredientId } = await params;
	const [ingredient, user] = await Promise.all([
		getIngredientById(ingredientId),
		getUserOptionalServer(),
	]);
	const isAdmin = user?.role === "admin";

	if (!ingredient) {
		return (
			<div className="min-h-[50vh] flex flex-col items-center justify-center">
				<div className="w-20 h-20 rounded-2xl bg-slate-100 flex items-center justify-center mb-4">
					<i className="ri-leaf-line text-4xl text-slate-400" />
				</div>
				<h2 className="text-xl font-semibold text-slate-900 mb-2">Ingredient not found</h2>
				<p className="text-slate-500 mb-6">The ingredient you&apos;re looking for doesn&apos;t exist.</p>
				<Link href="/ingredients" className="btn-primary">
					<i className="ri-arrow-left-line" />
					Back to Ingredients
				</Link>
			</div>
		);
	}

	const totalMacros = ingredient.proteinPer100g + ingredient.carbsPer100g + ingredient.fatPer100g;
	const proteinPercentage = totalMacros > 0 ? (ingredient.proteinPer100g / totalMacros) * 100 : 0;
	const carbsPercentage = totalMacros > 0 ? (ingredient.carbsPer100g / totalMacros) * 100 : 0;
	const fatPercentage = totalMacros > 0 ? (ingredient.fatPer100g / totalMacros) * 100 : 0;

	return (
		<div className="max-w-3xl mx-auto space-y-6">
			<div className="flex items-center gap-4">
				<Link href="/ingredients" className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-slate-600" />
				</Link>
				<div className="flex-1">
					<div className="flex items-center gap-3">
						<h1 className="text-2xl font-bold text-slate-900 capitalize">{ingredient.name}</h1>
						{ingredient.isVerified && (
							<span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-green-100 text-green-700 text-xs font-medium">
								<i className="ri-verified-badge-line" />
								Verified
							</span>
						)}
					</div>
					<p className="text-slate-500">Per {ingredient.servingSize} {ingredient.servingUnit}</p>
				</div>
			</div>

			<div className="card p-6 bg-linear-to-br from-brand-50 to-accent-50">
				<h2 className="font-semibold text-slate-900 mb-4">Nutritional Information</h2>
				<div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-fire-line text-orange-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{ingredient.caloriesPer100g}</p>
						<p className="text-xs text-slate-500">Calories</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-red-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-heart-pulse-line text-red-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{ingredient.proteinPer100g}g</p>
						<p className="text-xs text-slate-500">Protein</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-bread-line text-amber-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{ingredient.carbsPer100g}g</p>
						<p className="text-xs text-slate-500">Carbs</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-yellow-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-drop-line text-yellow-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{ingredient.fatPer100g}g</p>
						<p className="text-xs text-slate-500">Fat</p>
					</div>
					<div className="text-center p-4 bg-white rounded-xl">
						<div className="w-10 h-10 rounded-full bg-green-100 flex items-center justify-center mx-auto mb-2">
							<i className="ri-leaf-line text-green-600" />
						</div>
						<p className="text-2xl font-bold text-slate-900">{ingredient.fiberPer100g ?? 0}g</p>
						<p className="text-xs text-slate-500">Fiber</p>
					</div>
				</div>
			</div>

			<div className="card p-6">
				<h2 className="font-semibold text-slate-900 flex items-center gap-2 mb-4">
					<i className="ri-pie-chart-2-line text-brand-500" />
					Macronutrient Distribution
				</h2>
				<div className="h-4 flex rounded-full overflow-hidden mb-4 bg-slate-100">
					<div
						style={{ width: `${proteinPercentage}%` }}
						className="bg-red-500 transition-all"
						title={`Protein: ${proteinPercentage.toFixed(1)}%`}
					/>
					<div
						style={{ width: `${carbsPercentage}%` }}
						className="bg-amber-500 transition-all"
						title={`Carbs: ${carbsPercentage.toFixed(1)}%`}
					/>
					<div
						style={{ width: `${fatPercentage}%` }}
						className="bg-yellow-500 transition-all"
						title={`Fat: ${fatPercentage.toFixed(1)}%`}
					/>
				</div>
				<div className="flex flex-wrap gap-6">
					<div className="flex items-center gap-2">
						<div className="w-4 h-4 rounded-full bg-red-500" />
						<span className="text-sm text-slate-600">Protein</span>
						<span className="text-sm font-semibold text-slate-900">{proteinPercentage.toFixed(0)}%</span>
					</div>
					<div className="flex items-center gap-2">
						<div className="w-4 h-4 rounded-full bg-amber-500" />
						<span className="text-sm text-slate-600">Carbs</span>
						<span className="text-sm font-semibold text-slate-900">{carbsPercentage.toFixed(0)}%</span>
					</div>
					<div className="flex items-center gap-2">
						<div className="w-4 h-4 rounded-full bg-yellow-500" />
						<span className="text-sm text-slate-600">Fat</span>
						<span className="text-sm font-semibold text-slate-900">{fatPercentage.toFixed(0)}%</span>
					</div>
				</div>
			</div>

			{isAdmin && (
				<div className="card p-6">
					<div className="flex items-center justify-between">
						<div className="text-sm text-slate-500">
							{ingredient.brand && <span>Brand: {ingredient.brand}</span>}
						</div>
						<div className="flex gap-2">
							<Link href={`/ingredients/${ingredientId}/edit`} className="btn-secondary text-sm px-3 py-1.5">
								<i className="ri-edit-line" />
								Edit
							</Link>
							<DeleteIngredientButton id={ingredientId} />
						</div>
					</div>
				</div>
			)}
		</div>
	);
}
