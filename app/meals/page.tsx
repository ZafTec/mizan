
import { getTodayMeal } from "@/data/meal";
import MealMacrosChart from "@/components/MealMacrosChart";
import Link from "next/link";

export default async function Page() {
	const meals = await getTodayMeal();

	// Calculate total calories and macros
	const totalCalories = meals.reduce((sum, meal) => sum + meal.totalMacros.calories, 0);
	const totalProtein = meals.reduce((sum, meal) => sum + meal.totalMacros.protein, 0);
	const totalCarbs = meals.reduce((sum, meal) => sum + meal.totalMacros.carbs, 0);
	const totalFat = meals.reduce((sum, meal) => sum + meal.totalMacros.fat, 0);
	const totalFiber = meals.reduce((sum, meal) => sum + meal.totalMacros.fiber, 0);

	return (
		<div className="flex flex-col py-4 md:px-10 lg:px-16 gap-4">
			<div className="flex flex-row justify-between items-center">
				<h1 className="text-4xl font-bold">Meals</h1>
				<div>
					<Link href="/meals/add" className="bg-emerald-700 text-white px-4 py-2 rounded-lg">Add Meal</Link>
				</div>
			</div>
			

			{/* Display total daily intake */}
			<div className="mt-4">
				<h2 className="text-2xl font-bold">Today's Total Intake</h2>
				<p>Calories: {totalCalories} kcal</p>
				<p>Protein: {totalProtein} g</p>
				<p>Carbs: {totalCarbs} g</p>
				<p>Fat: {totalFat} g</p>
				<p>Fiber: {totalFiber} g</p>
			</div>

			{/* List of meals */}
			<div className="mt-4">
				<h2 className="text-2xl font-bold">Meals List</h2>
				<ul className="list-disc pl-5">
					{meals.map((meal) => (
						<li key={meal.id.toString()} className="mt-2">
							<h3 className="text-xl font-semibold">{meal.name}</h3>
							<p>Type: {meal.mealType}</p>
							<p>Calories: {meal.totalMacros.calories} kcal</p>
							<p>Protein: {meal.totalMacros.protein} g</p>
							<p>Carbs: {meal.totalMacros.carbs} g</p>
							<p>Fat: {meal.totalMacros.fat} g</p>
							<p>Fiber: {meal.totalMacros.fiber} g</p>
						</li>
					))}
				</ul>
			</div>

			{/* Chart of macros over time */}
			<div className="mt-4">
				<h2 className="text-2xl font-bold">Macros Over Time</h2>
				<MealMacrosChart meals={meals} />
			</div>
		</div>
	);
}