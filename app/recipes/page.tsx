import Link from "next/link";
import Image from "next/image";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import RecipeMoreButton from "@/components/Recipes/RecipeMoreButton";

export default function Page() {
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
					<div className="flex gap-4 items-center">
						<div className="flex-1">
							<div className="relative">
								<input
									type="text"
									placeholder="Search recipes..."
									className="w-full py-3 px-4 pr-12 rounded-lg border border-gray-300 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
								/>
								<svg
									className="absolute right-4 top-1/2 transform -translate-y-1/2 text-gray-400 w-5 h-5"
									fill="none"
									strokeLinecap="round"
									strokeLinejoin="round"
									strokeWidth="2"
									viewBox="0 0 24 24"
									stroke="currentColor"
								>
									<path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
								</svg>
							</div>
						</div>
						<select className="py-3 px-4 rounded-lg border border-gray-300 focus:ring-2 focus:ring-blue-500 focus:border-transparent">
							<option>Sort by</option>
							<option>Name</option>
							<option>Date</option>
							<option>Calories</option>
						</select>
					</div>
				</div>

				{/* Recipe list */}
				<div className="space-y-4">
					{/* Recipe card */}
					<Link href={`/recipes/${1}`} className="block bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow">
						<div className="flex">
							<Image
								src={placeHolderImage}
								alt="Recipe"
								className="w-48 h-48 object-cover rounded-l-lg"
							/>
							<div className="flex-1 p-6">
								<div className="flex justify-between items-start">
									<div>
										<h3 className="text-xl font-semibold mb-2">Healthy Chicken Salad</h3>
										<p className="text-gray-600 mb-4">Fresh and light chicken salad with mixed greens and honey mustard dressing</p>
									</div>
									<RecipeMoreButton recipeId="1" />
								</div>
								<div className="flex gap-6">
									<div>
										<span className="text-gray-500 text-sm">Calories</span>
										<p className="font-semibold">350 kcal</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Protein</span>
										<p className="font-semibold">25g</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Carbs</span>
										<p className="font-semibold">30g</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Fat</span>
										<p className="font-semibold">15g</p>
									</div>
								</div>
							</div>
						</div>
					</Link>
					<Link href={`/recipes/${2}`} className="block bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow">
						<div className="flex">
							<Image
								src={placeHolderImage}
								alt="Recipe"
								className="w-48 h-48 object-cover rounded-l-lg"
							/>
							<div className="flex-1 p-6">
								<div className="flex justify-between items-start">
									<div>
										<h3 className="text-xl font-semibold mb-2">Healthy Chicken Salad</h3>
										<p className="text-gray-600 mb-4">Fresh and light chicken salad with mixed greens and honey mustard dressing</p>
									</div>
									<RecipeMoreButton recipeId="2" />
								</div>
								<div className="flex gap-6">
									<div>
										<span className="text-gray-500 text-sm">Calories</span>
										<p className="font-semibold">350 kcal</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Protein</span>
										<p className="font-semibold">25g</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Carbs</span>
										<p className="font-semibold">30g</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Fat</span>
										<p className="font-semibold">15g</p>
									</div>
								</div>
							</div>
						</div>
					</Link>
					<Link href={`/recipes/${3}`} className="block bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow">
						<div className="flex">
							<Image
								src={placeHolderImage}
								alt="Recipe"
								className="w-48 h-48 object-cover rounded-l-lg"
							/>
							<div className="flex-1 p-6">
								<div className="flex justify-between items-start">
									<div>
										<h3 className="text-xl font-semibold mb-2">Healthy Chicken Salad</h3>
										<p className="text-gray-600 mb-4">Fresh and light chicken salad with mixed greens and honey mustard dressing</p>
									</div>
									<RecipeMoreButton recipeId="3" />
								</div>
								<div className="flex gap-6">
									<div>
										<span className="text-gray-500 text-sm">Calories</span>
										<p className="font-semibold">350 kcal</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Protein</span>
										<p className="font-semibold">25g</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Carbs</span>
										<p className="font-semibold">30g</p>
									</div>
									<div>
										<span className="text-gray-500 text-sm">Fat</span>
										<p className="font-semibold">15g</p>
									</div>
								</div>
							</div>
						</div>
					</Link>
					{/* Add more recipe cards as needed */}
				</div>
			</div>
		</div>
	)
}


