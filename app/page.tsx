
import { getServerSession } from "next-auth";
import { options } from "@/app/api/auth/[...nextauth]/options";
import Link from "next/link";
import Image from "next/image";
import placeHolderImage from "@/public/placeholder-recipe.jpg";

export default async function Home() {

	const session = await getServerSession(options);
	const user = session?.user;

	return (
		<div className="flex flex-col items-center justify-center py-2">
			<div className=" w-full max-w-4xl p-6 mb-8 bg-white rounded-lg shadow-lg">
				<h2 className="text-2xl font-bold mb-4 text-gray-800">Popular Recipes</h2>
				<div className="grid grid-flow-col auto-cols-max overflow-x-auto gap-4 p-2">
					{/* Recipe cards */}
					<Link href="/recipes/1" className="bg-white rounded-lg shadow p-4 hover:shadow-xl cursor-pointer transition-shadow">
						<Image 
							src={placeHolderImage} 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</Link>
					<Link href="/recipes/2" className="bg-white rounded-lg shadow p-4 hover:shadow-xl cursor-pointer transition-shadow">
						<Image 
							src={placeHolderImage} 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</Link>
					<Link href="/recipes/3" className="bg-white rounded-lg shadow p-4 hover:shadow-xl cursor-pointer transition-shadow">
						<Image 
							src={placeHolderImage} 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</Link>
					<Link href="/recipes/4" className="bg-white rounded-lg shadow p-4 hover:shadow-xl cursor-pointer transition-shadow">
						<Image 
							src={placeHolderImage} 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</Link>
					<Link href="/recipes/5" className="bg-white rounded-lg shadow p-4 hover:shadow-xl cursor-pointer transition-shadow">
						<Image 
							src={placeHolderImage} 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</Link>
					<Link href="/recipes/6" className="bg-white rounded-lg shadow p-4 hover:shadow-xl cursor-pointer transition-shadow">
						<Image 
							src={placeHolderImage} 
							alt="Recipe" 
							className="w-full h-48 object-cover rounded-lg mb-3"
						/>
						<h3 className="text-lg font-semibold mb-2">Healthy Breakfast Bowl</h3>
						<p className="text-gray-600">300 calories | 15g protein</p>
					</Link>
				{/* Add more recipe cards as needed */}
				</div>
			</div>

			{/* Add your own and log meal */}
			<div className="w-full max-w-4xl p-6 mb-8 bg-white rounded-lg shadow-lg">
				<div className="grid grid-cols-1 md:grid-cols-2 gap-6">
					<div className="p-4 border-2 border-dashed border-gray-300 rounded-lg text-center">
						<h3 className="text-xl font-semibold mb-3">Add Your Recipe</h3>
						<Link href="/recipes/add" className="bg-orange-600 inline-block text-white px-6 py-2 rounded-full hover:bg-green-600 transition-colors">
							Create Recipe
						</Link>
					</div>
					<div className="p-4 border-2 border-dashed border-gray-300 rounded-lg text-center">
						<h3 className="text-xl font-semibold mb-3">Log Your Meal</h3>
						<Link href='/meals/add' className="bg-emerald-500 inline-block text-white px-6 py-2 rounded-full hover:bg-blue-600 transition-colors">
							Log Meal
						</Link>
					</div>
				</div>
			</div>

			{/* Nutrition status with chart and more */}
			<div className="relative w-full max-w-4xl">
				<div className={`p-6 bg-white rounded-lg shadow-lg ${!user ? 'blur-sm' : ''}`}>
					<h2 className="text-2xl font-bold mb-4 text-gray-800">Nutrition Overview</h2>
					<div className="grid grid-cols-1 md:grid-cols-2 gap-6">
						<div className="p-4 bg-gray-50 rounded-lg">
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

				{/* Authentication overlay */}
				{!user && (
					<div className="absolute inset-0 flex flex-col items-center justify-center bg-black bg-opacity-15 rounded-lg">
						<div className="text-center text-white mb-6">
							<h3 className="text-2xl font-bold mb-2">Track Your Nutrition</h3>
							<p className="mb-6">Sign in to access detailed nutrition tracking</p>
							<div className="space-x-4">
								<Link 
									href="/api/auth/signin" 
									className="bg-blue-500 text-white px-6 py-2 rounded-full hover:bg-blue-600 transition-colors"
								>
									Sign In
								</Link>
								<Link 
									href="/api/auth/register" 
									className="bg-green-500 text-white px-6 py-2 rounded-full hover:bg-green-600 transition-colors"
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
