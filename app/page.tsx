import Link from "next/link";
import Image from "next/image";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import DailyOverviewChart from "@/components/DailyOverviewChart";
import { getUserOptionalServer } from "@/helper/session";
import { getPopularRecipes } from "@/data/recipe";

export default async function Home() {
	const user = await getUserOptionalServer();
	const popularRecipes = await getPopularRecipes();

	return (
		<div className="flex flex-col items-center justify-center py-2 w-full">
			<div className="w-full max-w-4xl p-4 sm:p-6 mb-6 sm:mb-8 bg-white rounded-lg shadow-lg">
				<h2 className="text-xl sm:text-2xl font-bold mb-4 text-gray-800">Popular Recipes</h2>
				<div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4 overflow-x-auto p-2">
					{popularRecipes.map((recipe) => (
						<Link key={recipe._id.toString()} href={`/recipes/${recipe._id}`} className="bg-white rounded-lg shadow p-4 hover:shadow-xl cursor-pointer transition-shadow">
							<Image 
								src={placeHolderImage} 
								alt="Recipe" 
								className="w-full h-36 sm:h-48 object-cover rounded-lg mb-3"
								width={400}
								height={300}
							/>
							<h3 className="text-base sm:text-lg font-semibold mb-2 line-clamp-1">{recipe.name}</h3>
							<p className="text-sm sm:text-base text-gray-600">{recipe.totalMacros.calories.toFixed()} calories | {recipe.totalMacros.protein.toFixed()}g protein</p>
						</Link>
					))}
				{/* Add more recipe cards as needed */}
				</div>
			</div>

			{/* Add your own and log meal */}
			<div className="w-full max-w-4xl p-4 sm:p-6 mb-6 sm:mb-8 bg-white rounded-lg shadow-lg">
				<div className="grid grid-cols-1 sm:grid-cols-2 gap-4 sm:gap-6">
					<div className="p-4 border-2 border-dashed border-gray-300 rounded-lg text-center">
						<h3 className="text-lg sm:text-xl font-semibold mb-3">Add Your Recipe</h3>
						<Link href="/recipes/add" className="bg-orange-600 inline-block text-white px-4 sm:px-6 py-2 rounded-full hover:bg-green-600 transition-colors">
							Create Recipe
						</Link>
					</div>
					<div className="p-4 border-2 border-dashed border-gray-300 rounded-lg text-center">
						<h3 className="text-lg sm:text-xl font-semibold mb-3">Log Your Meal</h3>
						<Link href='/meals/add' className="bg-emerald-500 inline-block text-white px-4 sm:px-6 py-2 rounded-full hover:bg-blue-600 transition-colors">
							Log Meal
						</Link>
					</div>
				</div>
			</div>

			{/* Nutrition status with chart and more */}
			<div className="relative w-full max-w-4xl min-h-fit h-64">
				<div className={`p-4 sm:p-6 bg-white rounded-lg shadow-lg ${!user ? 'blur-sm' : ''}`}>
					<div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-2 sm:gap-0 mb-4">
						<h2 className="text-xl sm:text-2xl font-bold text-gray-800">Nutrition Overview</h2>
						<Link href={"/goal"} className="bg-emerald-700 text-white px-3 sm:px-4 py-1 sm:py-2 text-sm sm:text-base rounded-lg">Set Goal</Link>
					</div>
					{user && (
						<DailyOverviewChart />
					)}
				</div>

				{/* Authentication overlay */}
				{!user && (
					<div className="absolute inset-0 flex flex-col items-center justify-center bg-black bg-opacity-15 rounded-lg">
						<div className="text-center text-white mb-6 px-4">
							<h3 className="text-xl sm:text-2xl font-bold mb-2">Track Your Nutrition</h3>
							<p className="mb-6">Sign in to access detailed nutrition tracking</p>
							<div className="flex flex-col sm:flex-row justify-center gap-3 sm:gap-4">
								<Link 
									href="login" 
									className="bg-blue-500 text-white px-5 sm:px-6 py-2 rounded-full hover:bg-blue-600 transition-colors w-full sm:w-auto"
								>
									Sign In
								</Link>
								<Link 
									href="/register" 
									className="bg-green-500 text-white px-5 sm:px-6 py-2 rounded-full hover:bg-green-600 transition-colors w-full sm:w-auto"
								>
									Register
								</Link>
							</div>
						</div>
					</div>
				)}
			</div>
		</div>
	)
}
