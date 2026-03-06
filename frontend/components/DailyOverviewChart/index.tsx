'use client';

import OverviewPieChart from "./PieChart";
import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import Loading from "../Loading";

interface Goal {
	targetCalories: number | null;
	targetProteinGrams: number | null;
	targetCarbsGrams: number | null;
	targetFatGrams: number | null;
}

interface Macros {
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
	fiber: number;
}

export default function DailyOverviewChart() {
	const [goal, setGoal] = useState<Goal | null>(null);
	const [macros, setMacros] = useState<Macros>({
		calories: 0,
		protein: 0,
		carbs: 0,
		fat: 0,
		fiber: 0
	});
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		async function fetchData() {
			try {
				const today = new Date().toISOString().split('T')[0];

				// Fetch goal and daily meals in parallel
				const [goalResponse, mealsResponse] = await Promise.all([
					clientApi<Goal>('/api/Goals').catch(() => null),
					clientApi<{ totals: Macros }>('/api/Meals?date=' + today).catch(() => ({ totals: { calories: 0, protein: 0, carbs: 0, fat: 0, fiber: 0 } }))
				]);

				setGoal(goalResponse);
				setMacros(mealsResponse.totals);
			} catch (error) {
				console.error('Failed to fetch chart data:', error);
			} finally {
				setLoading(false);
			}
		}

		fetchData();
	}, []);

	const hasGoal = goal && (goal.targetCalories || goal.targetProteinGrams || goal.targetCarbsGrams || goal.targetFatGrams);

	return (
		<div className="grid grid-cols-1 md:grid-cols-2 gap-6">
			<div className="p-4 bg-gray-50 dark:bg-slate-800 rounded-lg">
				<div className="flex items-center w-full h-72 bg-gray-200 dark:bg-slate-700 rounded-lg mb-4">
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
							{Math.round(macros.calories)}{hasGoal ? ` / ${goal.targetCalories || 0}` : ''} kcal
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Protein</span>
						<span className="font-medium">
							{Math.round(macros.protein)}g{hasGoal ? ` / ${Math.round(goal.targetProteinGrams || 0)}g` : ''}
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Carbs</span>
						<span className="font-medium">
							{Math.round(macros.carbs)}g{hasGoal ? ` / ${Math.round(goal.targetCarbsGrams || 0)}g` : ''}
						</span>
					</div>
					<div className="flex justify-between items-center">
						<span>Fat</span>
						<span className="font-medium">
							{Math.round(macros.fat)}g{hasGoal ? ` / ${Math.round(goal.targetFatGrams || 0)}g` : ''}
						</span>
					</div>
					{macros.fiber > 0 && (
						<div className="flex justify-between items-center">
							<span>Fiber</span>
							<span className="font-medium">{Math.round(macros.fiber)}g</span>
						</div>
					)}
				</div>

				{!hasGoal && (
					<a
						href="/goal"
						className="mt-4 inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-brand-50 text-brand-600 hover:bg-brand-100 text-sm font-medium transition-colors"
					>
						<i className="ri-target-line" />
						Set Goals
					</a>
				)}
			</div>
		</div>
	)
}
