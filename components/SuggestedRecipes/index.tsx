
import Link from 'next/link';
import Image from 'next/image';
import placeHolderImage from '@/public/placeholder-recipe.jpg';
import type Recipe from '@/types/recipe';
import type User from '@/types/user';

interface SuggestedRecipesProps {
  user?: User;
  suggestions?: Recipe[];
  serverError?: string;
}

export default function SuggestedRecipes({ user, suggestions, serverError }: SuggestedRecipesProps) {

	if (serverError) {
		return (
		<div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-6">
			<p className="text-red-600">{serverError}</p>
		</div>
		);
	}

	return (
		<div className="max-w-7xl mx-auto px-4 py-8">
			<div className="flex justify-between items-center mb-8">
				<h1 className="text-3xl font-bold text-gray-800">Today&apos;s Suggested Recipes For You</h1>
				
				{/* Regenerate button for admins */}
				{user?.isAdmin && (
					<Link
						href="/suggestions/regenerate"
						className="bg-emerald-600 hover:bg-emerald-700 text-white px-4 py-2 rounded-lg transition shadow-sm disabled:opacity-50 disabled:cursor-not-allowed flex items-center"
					>
						<span className="mr-2">
							<svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
								<path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
							</svg>
						</span>
						Regenerate Suggestions
					</Link>
				)}
			</div>
			
			{suggestions?.length === 0 ? (
					<div className="bg-gray-50 rounded-lg p-8 text-center">
						<h3 className="text-xl font-semibold mb-4">No Suggestions Available</h3>
						<p className="text-gray-600">
							Add more meals or update your preferences to get personalized recipe suggestions.
						</p>
					</div>
				) : (
					<div className="space-y-6">
						{suggestions?.map((recipe) => (
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
										<div>
											<h3 className="text-xl font-semibold mb-2">{recipe.name}</h3>
											<p className="text-gray-600 mb-4">
												{recipe.description || `A delicious recipe with ${recipe.ingredients.length} ingredients`}
											</p>
										</div>
										<div className="flex flex-wrap gap-6">
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
			)}
		</div>
	);
}
