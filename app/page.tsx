
import { getServerSession } from "next-auth";
import { options } from "@/app/api/auth/[...nextauth]/options";
import { redirect } from "next/navigation";

export default async function Home() {

	const session = await getServerSession(options);
	const user = session?.user;

	return (
		<div className="flex flex-col items-center justify-center min-h-screen py-2">
			<div className="w-full max-w-4xl p-6 mb-8 bg-white rounded-lg shadow-lg">
				<h2 className="text-2xl font-bold mb-4 text-gray-800">Popular Recipes</h2>
				<div className="grid grid-flow-col auto-cols-max overflow-x-auto gap-4 p-2">
					{/* Recipe cards */}
					<div className="bg-white rounded-lg shadow p-4 hover:shadow-xl transition-shadow">
						<img 
							src="/placeholder-recipe.jpg" 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</div>
					<div className="bg-white rounded-lg shadow p-4 hover:shadow-xl transition-shadow">
						<img 
							src="/placeholder-recipe.jpg" 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</div>
					<div className="bg-white rounded-lg shadow p-4 hover:shadow-xl transition-shadow">
						<img 
							src="/placeholder-recipe.jpg" 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</div>
					<div className="bg-white rounded-lg shadow p-4 hover:shadow-xl transition-shadow">
						<img 
							src="/placeholder-recipe.jpg" 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</div>
					<div className="bg-white rounded-lg shadow p-4 hover:shadow-xl transition-shadow">
						<img 
							src="/placeholder-recipe.jpg" 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</div>
				{/* Add more recipe cards as needed */}
				</div>
			</div>

			{/* Add your own and log meal */}
			<div className="w-full max-w-4xl p-6 mb-8 bg-white rounded-lg shadow-lg">
				<div className="grid grid-cols-1 md:grid-cols-2 gap-6">
					<div className="p-4 border-2 border-dashed border-gray-300 rounded-lg text-center">
						<h3 className="text-xl font-semibold mb-3">Add Your Recipe</h3>
						<button className="bg-green-500 text-white px-6 py-2 rounded-full hover:bg-green-600 transition-colors">
							Create Recipe
						</button>
					</div>
					<div className="p-4 border-2 border-dashed border-gray-300 rounded-lg text-center">
						<h3 className="text-xl font-semibold mb-3">Log Your Meal</h3>
						<button className="bg-blue-500 text-white px-6 py-2 rounded-full hover:bg-blue-600 transition-colors">
							Log Meal
						</button>
					</div>
				</div>
			</div>

			{/* Nutrition status with chart and more */}
			<div className="w-full max-w-4xl p-6 bg-white rounded-lg shadow-lg">
				<h2 className="text-2xl font-bold mb-4 text-gray-800">Nutrition Overview</h2>
				<div className="grid grid-cols-1 md:grid-cols-2 gap-6">
					<div className="p-4 bg-gray-50 rounded-lg">
						{/* Placeholder for chart */}
						<div className="w-full h-64 bg-gray-200 rounded-lg mb-4"></div>
						<h4 className="text-lg font-semibold">Daily Progress</h4>
					</div>
					<div className="p-4">
						<h4 className="text-lg font-semibold mb-4">Nutrition Summary</h4>
						<div className="space-y-4">
							<div className="flex justify-between items-center">
								<span>Calories</span>
								<span className="font-medium">1,200 / 2,000</span>
							</div>
							<div className="flex justify-between items-center">
								<span>Protein</span>
								<span className="font-medium">45g / 60g</span>
							</div>
							<div className="flex justify-between items-center">
								<span>Carbs</span>
								<span className="font-medium">130g / 200g</span>
							</div>
							<div className="flex justify-between items-center">
								<span>Fat</span>
								<span className="font-medium">35g / 50g</span>
							</div>
						</div>
					</div>
				</div>
			</div>
		</div>
	)
}
