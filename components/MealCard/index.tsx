"use client";

import Meal from "@/types/meal";
import MealTypePill from "./MealTypePill";

interface MealCardProps {
	meal: Meal;
}

export default function MealCard({ meal }: MealCardProps) {

	function deleteMeal(meal: Meal) {
		// delete meal
		console.log("Deleting meal with id: ", meal);
	}

	return (
		<div className="bg-white rounded-lg shadow-md p-4">
			<div className="flex flex-row justify-between items-center">
				<h2 className="text-lg font-semibold">{meal.name}</h2>
				<i className="ri-delete-bin-6-line cursor-pointer text-red-500 hover:text-lg" onClick={() => deleteMeal(meal)}></i>
			</div>
			<MealTypePill mealType={meal.mealType} />
			<div className="flex justify-between mt-4">
				<div>
					<p className="text-sm text-gray-500">Calories</p>
					<p className="text-lg font-semibold">{meal.totalMacros.calories}</p>
				</div>
				<div>
					<p className="text-sm text-gray-500">Protein</p>
					<p className="text-lg font-semibold">{meal.totalMacros.protein}</p>
				</div>
				<div>
					<p className="text-sm text-gray-500">Carbs</p>
					<p className="text-lg font-semibold">{meal.totalMacros.carbs}</p>
				</div>
				<div>
					<p className="text-sm text-gray-500">Fat</p>
					<p className="text-lg font-semibold">{meal.totalMacros.fat}</p>
				</div>
				<div>
					<p className="text-sm text-gray-500">Fiber</p>
					<p className="text-lg font-semibold">{meal.totalMacros.fiber}</p>
				</div>
			</div>
		</div>
	);
}
