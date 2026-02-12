"use client";

import { getMeal, getDailyTotals, MealEntry } from "@/data/meal";
import { getCurrentGoal, UserGoal } from "@/data/goal";
import { useSession } from "@/lib/auth-client";
import { DeleteConfirmModal } from "@/components/DeleteConfirmModal";
import { deleteMeal } from "@/data/meal";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from 'recharts';
// Assuming radix-ui wrap or similar, if not I'll just use HTML progress or div. 
// Actually I don't see components/ui/progress in file list. I will use custom div.

interface DailyStat {
	date: string;
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
}

export default function MealsPage() {
	const { data: session, isPending } = useSession();
	const searchParams = useSearchParams();
	const router = useRouter();

	// Date state
	const queryDate = searchParams.get("date") || new Date().toISOString().split("T")[0];

	const [todayMeals, setTodayMeals] = useState<MealEntry[]>([]);
	const [goal, setGoal] = useState<UserGoal | null>(null);
	const [history, setHistory] = useState<DailyStat[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [deleteModalOpen, setDeleteModalOpen] = useState(false);
	const [mealToDelete, setMealToDelete] = useState<{ id: string; name: string } | null>(null);

	// Navigation
	const handlePrevDay = () => {
		const d = new Date(queryDate);
		d.setDate(d.getDate() - 1);
		router.push(`/meals?date=${d.toISOString().split("T")[0]}`);
	};

	const handleNextDay = () => {
		const d = new Date(queryDate);
		d.setDate(d.getDate() + 1);
		router.push(`/meals?date=${d.toISOString().split("T")[0]}`);
	};

	const handleDeleteClick = (id: string, name: string) => {
		setMealToDelete({ id, name });
		setDeleteModalOpen(true);
	};

	const handleDeleteConfirm = async () => {
		if (!mealToDelete) return;
		const result = await deleteMeal(mealToDelete.id);
		if (result.success) {
			// Refresh the meal list
			const meals = await getMeal(queryDate);
			setTodayMeals(meals);
		}
		setMealToDelete(null);
	};

	useEffect(() => {
		async function loadData() {
			if (!session?.user) return;

			try {
				setLoading(true);
				setError(null);

				// Parallel fetch
				const [meals, userGoal] = await Promise.all([
					getMeal(queryDate),
					getCurrentGoal()
				]);

				setTodayMeals(meals);
				setGoal(userGoal);

				// Fetch history (last 7 days from selected date)
				// Note: ideally backend endpoint. For now client loop.
				const historyData: DailyStat[] = [];
				const dates: string[] = [];
				for (let i = 6; i >= 0; i--) {
					const d = new Date(queryDate);
					d.setDate(d.getDate() - i);
					dates.push(d.toISOString().split("T")[0]);
				}

				// Fetch totals for these dates
				const historyResults = await Promise.all(
					dates.map(d => getDailyTotals(d))
				);

				historyResults.forEach((res, idx) => {
					historyData.push({
						date: dates[idx].slice(5), // mm-dd
						calories: res?.calories || 0,
						protein: res?.protein || 0,
						carbs: res?.carbs || 0,
						fat: res?.fat || 0
					});
				});

				setHistory(historyData);

			} catch (err) {
				console.error("Failed to load data:", err);
				setError(err instanceof Error ? err.message : "Failed to load data");
			} finally {
				setLoading(false);
			}
		}

		loadData();
	}, [session, queryDate]);

	const totalCalories = todayMeals.reduce((sum, meal) => sum + (meal.calories || 0), 0);
	const totalProtein = todayMeals.reduce((sum, meal) => sum + (meal.proteinGrams || 0), 0);
	const totalCarbs = todayMeals.reduce((sum, meal) => sum + (meal.carbsGrams || 0), 0);
	const totalFat = todayMeals.reduce((sum, meal) => sum + (meal.fatGrams || 0), 0);

	// Goal percentages
	const caloriePct = goal?.targetCalories ? Math.min(100, (totalCalories / goal.targetCalories) * 100) : 0;
	const proteinPct = goal?.targetProteinGrams ? Math.min(100, (totalProtein / goal.targetProteinGrams) * 100) : 0;
	const carbsPct = goal?.targetCarbsGrams ? Math.min(100, (totalCarbs / goal.targetCarbsGrams) * 100) : 0;
	const fatPct = goal?.targetFatGrams ? Math.min(100, (totalFat / goal.targetFatGrams) * 100) : 0;

	if (isPending || (loading && !todayMeals.length)) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-500"></div>
			</div>
		);
	}

	if (!session?.user) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<p className="text-slate-500">Please log in to view your meals</p>
			</div>
		);
	}

	return (
		<div className="space-y-6">
			{/* Header & Date Nav */}
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<h1 className="text-2xl font-bold text-slate-900">Food Diary</h1>
					<p className="text-slate-500 mt-1">Track your daily nutrition intake</p>
				</div>
				<div className="flex items-center gap-4">
					<div className="flex items-center bg-white rounded-xl shadow-sm border border-slate-200 p-1">
						<button onClick={handlePrevDay} className="w-8 h-8 flex items-center justify-center hover:bg-slate-100 rounded-lg text-slate-600">
							<i className="ri-arrow-left-s-line" />
						</button>
						<span className="px-4 font-medium text-slate-900 min-w-[140px] text-center">
							{new Date(queryDate).toLocaleDateString("en-US", { weekday: 'short', month: 'short', day: 'numeric' })}
						</span>
						<button onClick={handleNextDay} className="w-8 h-8 flex items-center justify-center hover:bg-slate-100 rounded-lg text-slate-600">
							<i className="ri-arrow-right-s-line" />
						</button>
					</div>
					<Link href="/meals/add" className="btn-primary">
						<i className="ri-add-line" />
						Log Meal
					</Link>
				</div>
			</div>

			{/* Goals & Progress */}
			{goal && (
				<div className="card p-6">
					<h2 className="font-semibold text-slate-900 mb-4 flex items-center gap-2">
						<i className="ri-target-line text-brand-500" />
						Daily Goals
					</h2>
					<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
						{/* Calories */}
						<div className="space-y-2">
							<div className="flex justify-between text-sm">
								<span className="font-medium text-slate-700">Calories</span>
								<span className="text-slate-500">{Math.round(totalCalories)} / {goal.targetCalories} kcal</span>
							</div>
							<div className="h-2 bg-slate-100 rounded-full overflow-hidden">
								<div className="h-full bg-orange-500 rounded-full transition-all duration-500" style={{ width: `${caloriePct}%` }} />
							</div>
						</div>
						{/* Protein */}
						<div className="space-y-2">
							<div className="flex justify-between text-sm">
								<span className="font-medium text-slate-700">Protein</span>
								<span className="text-slate-500">{totalProtein.toFixed(1)} / {goal.targetProteinGrams} g</span>
							</div>
							<div className="h-2 bg-slate-100 rounded-full overflow-hidden">
								<div className="h-full bg-red-500 rounded-full transition-all duration-500" style={{ width: `${proteinPct}%` }} />
							</div>
						</div>
						{/* Carbs */}
						<div className="space-y-2">
							<div className="flex justify-between text-sm">
								<span className="font-medium text-slate-700">Carbs</span>
								<span className="text-slate-500">{totalCarbs.toFixed(1)} / {goal.targetCarbsGrams} g</span>
							</div>
							<div className="h-2 bg-slate-100 rounded-full overflow-hidden">
								<div className="h-full bg-amber-500 rounded-full transition-all duration-500" style={{ width: `${carbsPct}%` }} />
							</div>
						</div>
						{/* Fat */}
						<div className="space-y-2">
							<div className="flex justify-between text-sm">
								<span className="font-medium text-slate-700">Fat</span>
								<span className="text-slate-500">{totalFat.toFixed(1)} / {goal.targetFatGrams} g</span>
							</div>
							<div className="h-2 bg-slate-100 rounded-full overflow-hidden">
								<div className="h-full bg-yellow-500 rounded-full transition-all duration-500" style={{ width: `${fatPct}%` }} />
							</div>
						</div>
					</div>
				</div>
			)}

			{/* History Chart */}
			<div className="card p-6 h-[400px]">
				<h2 className="font-semibold text-slate-900 mb-6 flex items-center gap-2">
					<i className="ri-bar-chart-fill text-brand-500" />
					Last 7 Days (Calories)
				</h2>
				<ResponsiveContainer width="100%" height="85%">
					<BarChart data={history}>
						<CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
						<XAxis dataKey="date" stroke="#64748b" fontSize={12} tickLine={false} axisLine={false} />
						<YAxis stroke="#64748b" fontSize={12} tickLine={false} axisLine={false} />
						<Tooltip
							contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)' }}
							cursor={{ fill: '#f1f5f9' }}
						/>
						<Bar dataKey="calories" fill="#f97316" radius={[4, 4, 0, 0]} name="Calories" />
					</BarChart>
				</ResponsiveContainer>
			</div>


			{/* Meal List */}
			<div className="card p-6">
				<div className="flex items-center justify-between mb-4">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
						<i className="ri-restaurant-2-line text-brand-500" />
						Meals ({new Date(queryDate).toLocaleDateString()})
					</h2>
					<span className="text-sm text-slate-500">
						{todayMeals.length} meals logged
					</span>
				</div>

				{todayMeals.length > 0 ? (
					<div className="space-y-3">
						{todayMeals.map((meal) => (
							<div
								key={meal.id}
								className="flex items-center justify-between p-4 bg-slate-50 hover:bg-slate-100 rounded-xl transition-colors"
							>
								<div>
									<h3 className="font-medium text-slate-900">
										{meal.name || meal.mealType}
									</h3>
									<p className="text-sm text-slate-500 capitalize">{meal.mealType} • {new Date(meal.loggedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</p>
								</div>
								<div className="flex items-center gap-4">
									<div className="text-right">
										<p className="font-semibold text-orange-600">
											{meal.calories || 0} kcal
										</p>
										<div className="flex gap-2 text-xs text-slate-500 mt-1">
											<span>P: {meal.proteinGrams?.toFixed(1)}g</span>
											<span>C: {meal.carbsGrams?.toFixed(1)}g</span>
											<span>F: {meal.fatGrams?.toFixed(1)}g</span>
										</div>
									</div>
									<button
										onClick={() => handleDeleteClick(meal.id, meal.name || meal.mealType)}
										className="p-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors"
										title="Delete meal"
									>
										<i className="ri-delete-bin-line text-xl" />
									</button>
								</div>
							</div>
						))}
					</div>
				) : (
					<div className="text-center py-12">
						<div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto mb-4">
							<i className="ri-restaurant-line text-3xl text-slate-400" />
						</div>
						<h3 className="text-lg font-semibold text-slate-900 mb-2">
							No meals logged
						</h3>
						<Link href="/meals/add" className="btn-primary mt-4">
							Log Meal
						</Link>
					</div>
				)}
			</div>

			<DeleteConfirmModal
				isOpen={deleteModalOpen}
				onClose={() => setDeleteModalOpen(false)}
				onConfirm={handleDeleteConfirm}
				itemName={mealToDelete?.name || "Meal"}
			/>
		</div>
	);
}
