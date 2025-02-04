'use client';

import OverviewPieChart from "./PieChart";
import { useEffect, useState } from "react";
import { GoalVersion } from "@/types/goal";
import Macros from "@/types/macro";
import Loading from "../Loading";


export default function DailyOverviewChart() {
	const [goal, setGoal] = useState<GoalVersion | null>(null);
	const [macros, setMacros] = useState<Macros>({
		calories: 0,
		protein: 0,
		carbs: 0,
		fat: 0,
		fiber: 0
	});
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		fetch("/api/goal")
			.then((res) => res.json())
			.then((data) => setGoal(data.goal))
			.catch((error) => console.error(error))
		
		fetch("/api/macros")
			.then((res) => res.json())
			.then((data) => setMacros(data.macros))
			.catch((error) => console.error(error))
			.finally(() => {
				setLoading(false);
			})
	}, []);

	if (!goal) return null;

	const {
		calories: targetCalories,
		protein: targetProtein,
		carbs: targetCarbs,
		fat: targetFat
	} = goal.targetMacro;
	
	return (
		<div className="grid grid-cols-1 md:grid-cols-2 gap-6">
			<div className="p-4 bg-gray-50 rounded-lg">
				<div className="flex items-center w-full h-72 bg-gray-200 rounded-lg mb-4">
					{loading ? <Loading /> : <OverviewPieChart macros={macros} />}
				</div>
				<h4 className="text-lg font-semibold">Daily Progress</h4>
			</div>
			<div className="p-4">
				<h4 className="text-lg font-semibold mb-4">Nutrition Summary</h4>

				<div className="space-y-4">
					<div className="flex justify-between items-center">
						<span>Calories</span>
						<span className="font-medium">
							{macros.calories} / {targetCalories}
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Protein</span>
						<span className="font-medium">
							{macros.protein}g / {targetProtein}g
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Carbs</span>
						<span className="font-medium">
							{macros.carbs}g / {targetCarbs}g
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Fat</span>
						<span className="font-medium">
							{macros.fat}g / {targetFat}g
						</span>
					</div>
				</div>
			</div>
		</div>
	)
}
