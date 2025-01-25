
import Link from "next/link";
import Image from "next/image";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import RecipeMoreButton from "@/components/Recipes/RecipeMoreButton";
import { getAllRecipes } from "@/data/recipe";
import { getUserOptionalServer } from "@/helper/session";

export default async function Page() {

	const recipes = await getAllRecipes();
	const user = await getUserOptionalServer();

	return (
		<div className="max-w-7xl mx-auto px-4 py-8">
			{/* Your Recipes */}
			<div className="mb-12">
				<h2 className="text-2xl font-bold mb-6 text-gray-800">Your Recipes</h2>
				<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
					{/* Recipe cards */}
					<div className="bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow p-4">
						<div className="flex items-center justify-between mb-4">
							<h3 className="text-lg font-semibold">Favorite Recipes</h3>
							<span className="text-gray-500">12 recipes</span>
						</div>
						<div className="flex gap-2">
							<Image 
								src={placeHolderImage} 
								alt="Recipe thumbnail" 
								className="w-16 h-16 rounded-full object-cover"
							/>
							<Image 
								src={placeHolderImage} 
								alt="Recipe thumbnail" 
								className="w-16 h-16 rounded-full object-cover"
							/>
							<div className="w-16 h-16 rounded-full bg-gray-200 flex items-center justify-center">
								<span className="text-gray-600">+10</span>
							</div>
						</div>
					</div>
					
					<div className="bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow p-4">
						<div className="flex items-center justify-between mb-4">
							<h3 className="text-lg font-semibold">Added Recipes</h3>
							<span className="text-gray-500">12 recipes</span>
						</div>
						<div className="flex gap-2">
							<Image 
								src={placeHolderImage} 
								alt="Recipe thumbnail" 
								className="w-16 h-16 rounded-full object-cover"
							/>
							<Image 
								src={placeHolderImage} 
								alt="Recipe thumbnail" 
								className="w-16 h-16 rounded-full object-cover"
							/>
							<div className="w-16 h-16 rounded-full bg-gray-200 flex items-center justify-center">
								<span className="text-gray-600">+10</span>
							</div>
						</div>
					</div>
					<div className="bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow p-4">
						<div className="flex items-center justify-between mb-4">
							<h3 className="text-lg font-semibold">Recent Recipes</h3>
							<span className="text-gray-500">12 recipes</span>
						</div>
						<div className="flex gap-2">
							<Image 
								src={placeHolderImage} 
								alt="Recipe thumbnail" 
								className="w-16 h-16 rounded-full object-cover"
							/>
							<Image 
								src={placeHolderImage} 
								alt="Recipe thumbnail" 
								className="w-16 h-16 rounded-full object-cover"
							/>
							<div className="w-16 h-16 rounded-full bg-gray-200 flex items-center justify-center">
								<span className="text-gray-600">+10</span>
							</div>
						</div>
					</div>
				</div>
			</div>

			{/* Add Recipe */}
			<div className="mb-12">
				<div className="bg-gray-50 border-2 border-dashed border-gray-300 rounded-lg p-8 text-center">
					<h3 className="text-xl font-semibold mb-4">Create New Recipe</h3>
					<p className="text-gray-600 mb-6">Add your own recipe to your collection</p>
					<Link href="/recipes/add" className="bg-orange-600 text-white inline-block px-8 py-3 my-2 rounded-lg hover:bg-blue-600 transition-colors">
						Add Recipe
					</Link>
				</div>
			</div>

			{/* Recipes */}
			<div>
				{/* search bar */}
				<div className="mb-8">
				{/* Your search bar and sorting UI */}
				</div>

				{/* Recipe list */}
				<div className="space-y-4">
					{recipes.map((recipe) => (
						<Link
							key={recipe._id.toString()}
							href={`/recipes/${recipe._id}`}
							className="block bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow"
						>
							<div className="flex">
								<div className="w-48 h-48 bg-gray-200 rounded-l-lg relative">
									<Image
										src={recipe.images[0] || placeHolderImage}
										alt={recipe.name}
										fill
										className="object-cover rounded-l-lg"
									/>
								</div>
								<div className="flex-1 p-6">
									<div className="flex justify-between items-start">
										<div>
											<h3 className="text-xl font-semibold mb-2">
												{recipe.name}
											</h3>
											<p className="text-gray-600 mb-4">
												{recipe.description}
											</p>
										</div>
										{user && recipe.creator && recipe.creator._id.toString() === user._id.toString() && (
											<RecipeMoreButton recipeId={recipe._id} />
										)}
									</div>
									<div className="flex gap-6">
										<div>
											<span className="text-gray-500 text-sm">Calories</span>
											<p className="font-semibold">
												{recipe.totalMacros.calories.toFixed(0)} kcal
											</p>
										</div>
										<div>
											<span className="text-gray-500 text-sm">Protein</span>
											<p className="font-semibold">
												{recipe.totalMacros.protein.toFixed(1)}g
											</p>
										</div>
										<div>
											<span className="text-gray-500 text-sm">Carbs</span>
											<p className="font-semibold">
												{recipe.totalMacros.carbs.toFixed(1)}g
											</p>
										</div>
										<div>
											<span className="text-gray-500 text-sm">Fat</span>
											<p className="font-semibold">
												{recipe.totalMacros.fat.toFixed(1)}g
											</p>
										</div>
									</div>
								</div>
							</div>
						</Link>
					))}
				</div>
			</div>
		</div>
	)
}


