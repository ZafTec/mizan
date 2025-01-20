
import { getCurrentGoal } from "@/data/goal";
import OverviewPieChart from "./PieChart";
import { getNutritionOverview, getTodayMeal } from "@/data/meal";


export default async function DailyOverviewChart() {
	const goal = await getCurrentGoal();
	if (!goal) return null;
	const {
		calories: targetCalories,
		protein: targetProtein,
		carbs: targetCarbs,
		fat: targetFat
	} = goal.targetMacro;
	const macros = await getNutritionOverview();
	const { calories, protein, carbs, fat} = macros;
	
	return (
		<div className="grid grid-cols-1 md:grid-cols-2 gap-6">
			<div className="p-4 bg-gray-50 rounded-lg">
				<div className="flex items-center w-full h-72 bg-gray-200 rounded-lg mb-4">
					<OverviewPieChart macros={macros} />
				</div>
				<h4 className="text-lg font-semibold">Daily Progress</h4>
			</div>
			<div className="p-4">
				<h4 className="text-lg font-semibold mb-4">Nutrition Summary</h4>

				<div className="space-y-4">
					<div className="flex justify-between items-center">
						<span>Calories</span>
						<span className="font-medium">
							{calories} / {targetCalories}
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Protein</span>
						<span className="font-medium">
							{protein}g / {targetProtein}g
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Carbs</span>
						<span className="font-medium">
							{carbs}g / {targetCarbs}g
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Fat</span>
						<span className="font-medium">
							{fat}g / {targetFat}g
						</span>
					</div>
				</div>
			</div>
		</div>
	)
}
