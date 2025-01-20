
import { getMeal, getTodayMeal } from "@/data/meal";
import MealMacrosChart from "@/components/MealMacrosChart";
import Link from "next/link";
import MealCard from "@/components/MealCard";

export default async function Page() {
    const todayMeals = await getTodayMeal();
    const weeksMeals = await getMeal();

	// Calculate total calories and macros
	const totalCalories = todayMeals.reduce((sum, meal) => sum + meal.totalMacros.calories, 0);
	const totalProtein = todayMeals.reduce((sum, meal) => sum + meal.totalMacros.protein, 0);
	const totalCarbs = todayMeals.reduce((sum, meal) => sum + meal.totalMacros.carbs, 0);
	const totalFat = todayMeals.reduce((sum, meal) => sum + meal.totalMacros.fat, 0);
	const totalFiber = todayMeals.reduce((sum, meal) => sum + meal.totalMacros.fiber, 0);

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
				<h2 className="text-2xl font-bold">Today&apos;s Total Intake</h2>
				<p>Calories: {totalCalories} kcal</p>
				<p>Protein: {totalProtein} g</p>
				<p>Carbs: {totalCarbs} g</p>
				<p>Fat: {totalFat} g</p>
				<p>Fiber: {totalFiber} g</p>
			</div>

			{/* List of meals */}
			<div className="mt-4">
				<h2 className="text-2xl font-bold">Today Meals List</h2>
				<div className="flex gap-3 pl-5">
					{todayMeals.map((meal) => (
						<MealCard meal={meal} key={meal.id.toString()} />
					))}
				</div>
			</div>

			{/* Chart of macros over time */}
			<div className="mt-4">
				<h2 className="text-2xl font-bold">Macros Over Time</h2>
				<MealMacrosChart meals={weeksMeals} />
			</div>
		</div>
	);
}