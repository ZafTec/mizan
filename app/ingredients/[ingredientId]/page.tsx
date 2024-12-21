import { getIngredientById } from "@/data/ingredient";

export default async function Page({ params }: { params: Promise<{ ingredientId: string }> }) {
	const { ingredientId } = await params;

	const ingredient = await getIngredientById(ingredientId);

	if (!ingredient) {
		return <div>Ingredient not found</div>
	}

	// Calculate total macros (excluding calories) for percentage calculation
	const totalMacros = 
		ingredient.macros.protein + 
		ingredient.macros.carbs + 
		ingredient.macros.fat;

	// Calculate percentages
	const proteinPercentage = (ingredient.macros.protein / totalMacros) * 100;
	const carbsPercentage = (ingredient.macros.carbs / totalMacros) * 100;
	const fatPercentage = (ingredient.macros.fat / totalMacros) * 100;

	return (
		<div className="max-w-2xl mx-auto p-6">
			<h1 className="text-2xl font-bold mb-6 uppercase text-orange-800">{ingredient.name}</h1>
			
			{/* Basic Info */}
			<div className="mb-6">
				<p className="text-gray-600">
					Serving Size: {ingredient.servingSize} {ingredient.servingUnit}
				</p>
				{ingredient.verified && (
					<span className="inline-block bg-green-100 text-green-800 px-2 py-1 rounded-full text-sm">
						Verified
					</span>
				)}
			</div>

			{/* Macros Summary */}
			<div className="mb-6">
				<h2 className="text-xl font-semibold mb-3">Nutritional Information</h2>
				<div className="space-y-2">
					<p>Calories: {ingredient.macros.calories} kcal</p>
					<p>Protein: {ingredient.macros.protein}g</p>
					<p>Carbs: {ingredient.macros.carbs}g</p>
					<p>Fat: {ingredient.macros.fat}g</p>
					<p>Fiber: {ingredient.macros.fiber}g</p>
				</div>
			</div>

			{/* Macros Progress Bar */}
			<div className="mb-6">
				<h2 className="text-xl font-semibold mb-3">Macronutrient Distribution</h2>
				<div className="h-6 flex rounded-full overflow-hidden">
					<div 
						style={{ width: `${proteinPercentage}%` }}
						className="bg-blue-500 h-full"
						title={`Protein: ${proteinPercentage.toFixed(1)}%`}
					/>
					<div 
						style={{ width: `${carbsPercentage}%` }}
						className="bg-green-500 h-full"
						title={`Carbs: ${carbsPercentage.toFixed(1)}%`}
					/>
					<div 
						style={{ width: `${fatPercentage}%` }}
						className="bg-yellow-500 h-full"
						title={`Fat: ${fatPercentage.toFixed(1)}%`}
					/>
				</div>

				{/* Legend */}
				<div className="flex gap-4 mt-2">
					<div className="flex items-center">
						<div className="w-3 h-3 bg-blue-500 rounded-full mr-1"></div>
						<span className="text-sm">Protein ({proteinPercentage.toFixed(1)}%)</span>
					</div>
					<div className="flex items-center">
						<div className="w-3 h-3 bg-green-500 rounded-full mr-1"></div>
						<span className="text-sm">Carbs ({carbsPercentage.toFixed(1)}%)</span>
					</div>
					<div className="flex items-center">
						<div className="w-3 h-3 bg-yellow-500 rounded-full mr-1"></div>
						<span className="text-sm">Fat ({fatPercentage.toFixed(1)}%)</span>
					</div>
				</div>
			</div>

			{/* Timestamps */}
			<div className="text-sm text-gray-500">
				{/* <p>Created: {new Date(ingredient.createdAt).toLocaleDateString()}</p> */}
				<p>Last Updated: {new Date(ingredient.updatedAt).toLocaleDateString()}</p>
			</div>
		</div>
	)
}
