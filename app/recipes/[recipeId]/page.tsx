
import RecipeOptions from "@/components/RecipeOptions";
import { getRecipeById } from "@/data/recipe";
import { getUserOptionalServer } from "@/helper/session";
import Image from "next/image";

export default async function Page({ params }: { params: Promise<{ recipeId: string }> }) {
	const { recipeId } = await params;
	
	const user = await getUserOptionalServer();
	const recipe = await getRecipeById(recipeId);

	if (!recipe) {
		return <div>Recipe not found</div>;
	}

	const isCreator = user !== null && recipe.creator && recipe.creator._id.toString() === user._id.toString();
	
	return (
		<div className="container mx-auto px-4 py-8">
			<div className="max-w-3xl mx-auto">
				{/* Recipe Header */}
				<div className="flex justify-between items-start mb-6">
					<h1 className="text-4xl font-bold">{recipe.name}</h1>
					<RecipeOptions recipeId={recipe._id.toString()} isCreator={isCreator} />
				</div>

				{recipe.creator ? (
					<p className="text-gray-600 mb-4">Created by {recipe.creator.email}</p>
				) : (
					<p className="text-gray-600 mb-4">Created by an anonymous user</p>
				)}

				{/* Images */}
				{recipe.images && recipe.images.length > 0 && (
					<div className="recipe-images mb-8">
						{recipe.images.length === 1 ? (
							<div className="w-full h-64 rounded-md overflow-hidden mb-4 relative">
								<Image
									src={recipe.images[0]}
									alt={`${recipe.name} image`}
									className="w-full object-cover rounded-md"
									fill
								/>
							</div>
						) : (
							<div className="grid grid-cols-2 gap-4">
								{recipe.images.map((image, index) => (
									<div key={index} className="w-full h-48 rounded-md overflow-hidden relative">
										<Image
											src={image}
											fill
											alt={`${recipe.name} image ${index + 1}`}
											className="w-full object-cover rounded-md"
										/>
									</div>
								))}
							</div>
						)}
					</div>
				)}

				{/* Description */}
				{recipe.description && (
					<p className="text-lg text-gray-800 mb-8">{recipe.description}</p>
				)}

				{/* Ingredients and Macros */}
				<div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-8">
					{/* Ingredients */}
					<div>
						<h2 className="text-2xl font-semibold mb-4">Ingredients</h2>
						<ul className="list-disc list-inside space-y-2">
							{recipe.ingredients.map((item, index) => (
								<li key={index} className="text-gray-700">
									{item.amount} {item.unit} {item.ingredient.name}
								</li>
							))}
						</ul>
					</div>

					{/* Macros */}
					<div>
						<h2 className="text-2xl font-semibold mb-4">Macros per Serving</h2>
						<div className="grid grid-cols-2 gap-4">
							<div className="bg-gray-100 p-4 rounded-md">
								<p className="text-gray-600">Calories</p>
								<p className="text-xl font-bold">
									{Math.round((recipe.totalMacros.calories / recipe.servings) * 100) / 100}
								</p>
							</div>
							<div className="bg-gray-100 p-4 rounded-md">
								<p className="text-gray-600">Protein</p>
								<p className="text-xl font-bold">
									{Math.round((recipe.totalMacros.protein / recipe.servings) * 100) / 100}g
								</p>
							</div>
							<div className="bg-gray-100 p-4 rounded-md">
								<p className="text-gray-600">Carbs</p>
								<p className="text-xl font-bold">
									{Math.round((recipe.totalMacros.carbs / recipe.servings) * 100) / 100}g
								</p>
							</div>
							<div className="bg-gray-100 p-4 rounded-md">
								<p className="text-gray-600">Fat</p>
								<p className="text-xl font-bold">
									{Math.round((recipe.totalMacros.fat / recipe.servings) * 100) / 100}g
								</p>
							</div>
							<div className="bg-gray-100 p-4 rounded-md">
								<p className="text-gray-600">Fiber</p>
								<p className="text-xl font-bold">
									{Math.round((recipe.totalMacros.fiber / recipe.servings) * 100) / 100}g
								</p>
							</div>
						</div>
					</div>
				</div>

				{/* Instructions */}
				<div className="mb-8">
					<h2 className="text-2xl font-semibold mb-4">Instructions</h2>
					<ol className="list-decimal list-inside space-y-4">
						{recipe.instructions.map((instruction, index) => (
							<li key={index} className="text-gray-700">
								{instruction}
							</li>
						))}
					</ol>
				</div>

				<div className="">
					<span className="text-gray-600">Serves {recipe.servings}</span>
				</div>
				{/* Tags */}
				{recipe.tags && recipe.tags.length > 0 && (
					<div className="mb-8">
						<h2 className="text-2xl font-semibold mb-4">Tags</h2>
						<div className="flex flex-wrap gap-2">
							{recipe.tags.map((tag, index) => (
								<span
									key={index}
									className="inline-block bg-orange-200 text-orange-800 rounded-full px-3 py-1 text-sm font-medium"
								>
									{tag}
								</span>
							))}
						</div>
					</div>
				)}
			</div>
		</div>
	)
}
