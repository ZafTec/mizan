"use client";

import Meal from "@/types/meal";
import MealTypePill from "./MealTypePill";
import { useRouter } from "next/navigation";

interface MealCardProps {
	meal: Meal;
}

export default function MealCard({ meal }: MealCardProps) {
	const router = useRouter();

	function deleteMeal(meal: Meal) {
		// delete meal
		console.log("Deleting meal: ", meal);
		
		fetch(`/api/meals/${meal._id}`, {
			method: "DELETE",
		})
		.then((res) => res.json())
		.then((data) => {
			console.log(data);
			router.refresh();
		})
		.catch((error) => {
			console.error("Error deleting meal: ", error);
		});
		
	}

	return (
		<div className="bg-white rounded-lg shadow-md p-4">
			<div className="flex flex-row justify-between items-center">
				<h2 className="text-lg font-semibold">{meal.name}</h2>
				<i className="ri-delete-bin-6-line cursor-pointer text-red-500 hover:text-lg" onClick={() => deleteMeal(meal)}></i>
			</div>

			<MealTypePill mealType={meal.mealType} />

			<div className="grid grid-cols-2 gap-4 mt-4 text-sm">
				<div>
					<p className="text-gray-500">Calories</p>
					<p className="font-semibold">{meal.totalMacros.calories}</p>
				</div>
				<div>
					<p className="text-gray-500">Protein</p>
					<p className="font-semibold">{meal.totalMacros.protein}</p>
				</div>
				<div>
					<p className="text-gray-500">Carbs</p>
					<p className="font-semibold">{meal.totalMacros.carbs}</p>
				</div>
				<div>
					<p className="text-gray-500">Fat</p>
					<p className="font-semibold">{meal.totalMacros.fat}</p>
				</div>
			</div>
		</div>
	);
}
