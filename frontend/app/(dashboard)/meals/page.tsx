"use client";

import { getMeal, getDailyTotals, getNutritionRange, MealEntry, DailyNutritionSummary } from "@/data/meal";
import { getCurrentGoal, getGoalHistory, UserGoal } from "@/data/goal";
import { getStreak, StreakInfo } from "@/data/achievement";
import { useSession } from "@/lib/auth-client";
import { DeleteConfirmModal } from "@/components/DeleteConfirmModal";
import { deleteMeal } from "@/data/meal";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ComposedChart, BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import type { Formatter, ValueType, NameType } from "recharts/types/component/DefaultTooltipContent";
// Assuming radix-ui wrap or similar, if not I'll just use HTML progress or div.
// Actually I don't see components/ui/progress in file list. I will use custom div.
import Loading from "@/components/Loading";

interface DailyStat {
	date: string;
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
	fiber: number;
	proteinCalRatio: number;
	goalCalories?: number;
	goalProtein?: number;
	goalCarbs?: number;
	goalFat?: number;
	goalFiber?: number;
	goalPcal?: number;
}

const percentTooltipFormatter: Formatter<ValueType, NameType> = (value, name) => {
	const label = String(name ?? "");

	if (value === undefined) {
		return ["-", label];
	}

	if (Array.isArray(value)) {
		return [`${value.join(" / ")}%`, label];
	}

	return [`${value}%`, label];
};

const gramsTooltipFormatter: Formatter<ValueType, NameType> = (value, name) => {
	const label = String(name ?? "");

	if (value === undefined) {
		return ["-", label];
	}

	if (Array.isArray(value)) {
		return [`${value.join(" / ")}g`, label];
	}

	return [`${value}g`, label];
};

const CHART_COLORS = {
	grid: "var(--border)",
	axis: "var(--muted-foreground)",
	tooltipBackground: "var(--popover)",
	tooltipBorder: "var(--border)",
	tooltipText: "var(--popover-foreground)",
	cursor: "color-mix(in oklab, var(--muted) 78%, transparent)",
};

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
	const [streak, setStreak] = useState<StreakInfo | null>(null);
	const [rangeDays, setRangeDays] = useState(7);
	const [goalHistory, setGoalHistory] = useState<UserGoal[]>([]);
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
			const [meals, streakInfo] = await Promise.all([
				getMeal(queryDate),
				getStreak()
			]);
			setTodayMeals(meals);
			setStreak(streakInfo);
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
				const [meals, userGoal, streakInfo, goalHist] = await Promise.all([
					getMeal(queryDate),
					getCurrentGoal(),
					getStreak(),
					getGoalHistory()
				]);

				setTodayMeals(meals);
				setGoal(userGoal);
				setStreak(streakInfo);
				setGoalHistory(goalHist);

				// Helper: find which goal was active on a given date
				const findGoalForDate = (dateStr: string): UserGoal | undefined => {
					const d = new Date(dateStr);
					for (const g of goalHist) {
						if (new Date(g.createdAt) <= d) return g;
					}
					return undefined;
				};

				// Fetch history using range endpoint
				const rangeData = await getNutritionRange(rangeDays, queryDate);
				setHistory(rangeData.map(d => {
					const dateGoal = findGoalForDate(d.date);
					return {
						date: d.date.slice(5),
						calories: d.calories,
						protein: d.protein,
						carbs: d.carbs,
						fat: d.fat,
						fiber: d.fiber,
						proteinCalRatio: d.calories > 0 ? Math.round((d.protein * 4 / d.calories) * 100) : 0,
						goalCalories: dateGoal?.targetCalories ?? undefined,
						goalProtein: dateGoal?.targetProteinGrams ?? undefined,
						goalCarbs: dateGoal?.targetCarbsGrams ?? undefined,
						goalFat: dateGoal?.targetFatGrams ?? undefined,
						goalFiber: dateGoal?.targetFiberGrams ?? undefined,
						goalPcal: dateGoal?.targetProteinCalorieRatio ?? undefined,
					};
				}));

			} catch (err) {
				console.error("Failed to load data:", err);
				setError(err instanceof Error ? err.message : "Failed to load data");
			} finally {
				setLoading(false);
			}
		}

		loadData();
	}, [session, queryDate, rangeDays]);

	const totalCalories = todayMeals.reduce((sum, meal) => sum + (meal.calories || 0), 0);
	const totalProtein = todayMeals.reduce((sum, meal) => sum + (meal.proteinGrams || 0), 0);
	const totalCarbs = todayMeals.reduce((sum, meal) => sum + (meal.carbsGrams || 0), 0);
	const totalFat = todayMeals.reduce((sum, meal) => sum + (meal.fatGrams || 0), 0);
	const totalFiber = todayMeals.reduce((sum, meal) => sum + (meal.fiberGrams || 0), 0);
	const dailyProteinCalRatio = totalCalories > 0 ? (totalProtein * 4 / totalCalories) * 100 : 0;

	// Goal percentages
	const caloriePct = goal?.targetCalories ? Math.min(100, (totalCalories / goal.targetCalories) * 100) : 0;
	const proteinPct = goal?.targetProteinGrams ? Math.min(100, (totalProtein / goal.targetProteinGrams) * 100) : 0;
	const carbsPct = goal?.targetCarbsGrams ? Math.min(100, (totalCarbs / goal.targetCarbsGrams) * 100) : 0;
	const fatPct = goal?.targetFatGrams ? Math.min(100, (totalFat / goal.targetFatGrams) * 100) : 0;

	if (isPending || (loading && !todayMeals.length)) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<Loading />
			</div>
		);
	}

	if (!session?.user) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<p className="text-slate-500 dark:text-slate-400">Please log in to view your meals</p>
			</div>
		);
	}

	return (
		<div className="space-y-6" data-testid="meal-list">
			{/* Header & Date Nav */}
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Diary</p>
					<div className="flex items-center gap-3">
						<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							Food diary
						</h1>
						{(streak?.currentStreak ?? 0) > 0 && (
							<span className="streak-gradient inline-flex items-center gap-1 rounded-2xl px-2.5 py-1 text-xs font-semibold text-white">
								<i className="ri-fire-fill" />
								{streak?.currentStreak ?? 0} day streak
							</span>
						)}
					</div>
					<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Track your daily nutrition intake.
					</p>
				</div>
				<div className="flex items-center gap-4">
					<div className="flex items-center rounded-xl border border-slate-200 bg-white p-1 shadow-sm dark:border-white/10 dark:bg-slate-950/75">
						<button onClick={handlePrevDay} className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-900/80">
							<i className="ri-arrow-left-s-line" />
						</button>
						<span className="min-w-35 px-4 text-center font-medium text-slate-900 dark:text-slate-100">
							{new Date(queryDate).toLocaleDateString("en-US", { weekday: 'short', month: 'short', day: 'numeric' })}
						</span>
						<button onClick={handleNextDay} className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-900/80">
							<i className="ri-arrow-right-s-line" />
						</button>
					</div>
					<Link href="/meals/add" className="btn-primary">
						<i className="ri-add-line" />
						Log Meal
					</Link>
				</div>
			</header>

			{/* Goals & Progress */}
			{goal && (
				<div className="card p-6">
					<h2 className="font-semibold text-slate-900 dark:text-slate-100 mb-4 flex items-center gap-2">
						<i className="ri-target-line text-brand-500" />
						Daily Goals
					</h2>
					<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
						{/* Calories */}
						<div className="space-y-2">
							<div className="flex justify-between text-sm">
								<span className="font-medium text-slate-700 dark:text-slate-200">Calories</span>
								<span className="text-slate-500 dark:text-slate-400">{Math.round(totalCalories)} / {goal.targetCalories} kcal</span>
							</div>
							<div className="h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800/80">
								<div className="h-full bg-orange-500 rounded-full transition-all duration-500" style={{ width: `${caloriePct}%` }} />
							</div>
						</div>
						{/* Protein */}
						<div className="space-y-2">
							<div className="flex justify-between text-sm">
								<span className="font-medium text-slate-700 dark:text-slate-200">Protein</span>
								<span className="text-slate-500 dark:text-slate-400">{totalProtein.toFixed(1)} / {goal.targetProteinGrams} g</span>
							</div>
							<div className="h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800/80">
								<div className="h-full bg-red-500 rounded-full transition-all duration-500" style={{ width: `${proteinPct}%` }} />
							</div>
						</div>
						{/* Carbs */}
						<div className="space-y-2">
							<div className="flex justify-between text-sm">
								<span className="font-medium text-slate-700 dark:text-slate-200">Carbs</span>
								<span className="text-slate-500 dark:text-slate-400">{totalCarbs.toFixed(1)} / {goal.targetCarbsGrams} g</span>
							</div>
							<div className="h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800/80">
								<div className="h-full bg-amber-500 rounded-full transition-all duration-500" style={{ width: `${carbsPct}%` }} />
							</div>
						</div>
						{/* Fat */}
						<div className="space-y-2">
							<div className="flex justify-between text-sm">
								<span className="font-medium text-slate-700 dark:text-slate-200">Fat</span>
								<span className="text-slate-500 dark:text-slate-400">{totalFat.toFixed(1)} / {goal.targetFatGrams} g</span>
							</div>
							<div className="h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800/80">
								<div className="h-full bg-yellow-500 rounded-full transition-all duration-500" style={{ width: `${fatPct}%` }} />
							</div>
						</div>
					</div>
					<div className="mt-3 flex flex-wrap gap-4 text-sm text-slate-500 dark:text-slate-400">
						{totalFiber > 0 && (
							<p>Fiber: <span className="font-medium text-green-600">{totalFiber.toFixed(1)}g</span></p>
						)}
						{dailyProteinCalRatio > 0 && (
							<p>P/Cal Ratio: <span className="font-medium text-violet-600">{dailyProteinCalRatio.toFixed(0)}%</span></p>
						)}
					</div>
				</div>
			)}

			{/* History Chart */}
			<div className="card p-6 h-100">
				<div className="flex items-center justify-between mb-6">
					<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
						<i className="ri-bar-chart-fill text-brand-500" />
						Last {rangeDays} Days (Calories)
					</h2>
					<div className="flex gap-1 rounded-lg bg-slate-100 p-1 dark:bg-slate-900/80">
						{[7, 14, 30].map(d => (
							<button key={d} onClick={() => setRangeDays(d)}
								className={`rounded-md px-3 py-1 text-sm font-medium transition-colors ${rangeDays === d ? "bg-white text-brand-600 shadow dark:bg-slate-950 dark:text-brand-300" : "text-slate-600 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-100"}`}>
								{d}d
							</button>
						))}
					</div>
				</div>
				<ResponsiveContainer width="100%" height="85%">
					<ComposedChart data={history}>
						<CartesianGrid strokeDasharray="3 3" vertical={false} stroke={CHART_COLORS.grid} />
						<XAxis dataKey="date" stroke={CHART_COLORS.axis} fontSize={12} tickLine={false} axisLine={false} />
						<YAxis stroke={CHART_COLORS.axis} fontSize={12} tickLine={false} axisLine={false} />
						<Tooltip
							contentStyle={{ borderRadius: '12px', border: `1px solid ${CHART_COLORS.tooltipBorder}`, backgroundColor: CHART_COLORS.tooltipBackground, color: CHART_COLORS.tooltipText, boxShadow: '0 20px 45px -28px rgb(15 23 42 / 0.45)' }}
							cursor={{ fill: CHART_COLORS.cursor }}
						/>
						<Bar dataKey="calories" fill="#f97316" radius={[4, 4, 0, 0]} name="Calories" />
						{history.some(d => d.goalCalories) && (
							<Line type="stepAfter" dataKey="goalCalories" stroke="#f97316" strokeDasharray="6 3" strokeWidth={2} dot={false} name="Calorie Goal" />
						)}
					</ComposedChart>
				</ResponsiveContainer>
			</div>

			{/* Macro Breakdown Chart */}
			<div className="card p-6 h-80">
				<h2 className="font-semibold text-slate-900 dark:text-slate-100 mb-4 flex items-center gap-2">
					<i className="ri-stack-fill text-brand-500" />
					Macro Breakdown (Last {rangeDays} Days)
				</h2>
				<ResponsiveContainer width="100%" height="85%">
					<BarChart data={history}>
						<CartesianGrid strokeDasharray="3 3" vertical={false} stroke={CHART_COLORS.grid} />
						<XAxis dataKey="date" stroke={CHART_COLORS.axis} fontSize={12} tickLine={false} axisLine={false} />
						<YAxis stroke={CHART_COLORS.axis} fontSize={12} tickLine={false} axisLine={false} unit="g" />
						<Tooltip contentStyle={{ borderRadius: '12px', border: `1px solid ${CHART_COLORS.tooltipBorder}`, backgroundColor: CHART_COLORS.tooltipBackground, color: CHART_COLORS.tooltipText, boxShadow: '0 20px 45px -28px rgb(15 23 42 / 0.45)' }} cursor={{ fill: CHART_COLORS.cursor }} />
						<Bar dataKey="protein" stackId="macros" fill="#ef4444" radius={[0, 0, 0, 0]} name="Protein (g)" />
						<Bar dataKey="carbs" stackId="macros" fill="#f59e0b" radius={[0, 0, 0, 0]} name="Carbs (g)" />
						<Bar dataKey="fat" stackId="macros" fill="#eab308" radius={[4, 4, 0, 0]} name="Fat (g)" />
					</BarChart>
				</ResponsiveContainer>
			</div>

			{/* P/Cal Ratio Trend */}
			<div className="card p-6 h-72">
				<h2 className="font-semibold text-slate-900 dark:text-slate-100 mb-4 flex items-center gap-2">
					<i className="ri-percent-line text-violet-500" />
					Protein/Calorie Ratio (Last {rangeDays} Days)
				</h2>
				<ResponsiveContainer width="100%" height="85%">
					<ComposedChart data={history}>
						<CartesianGrid strokeDasharray="3 3" vertical={false} stroke={CHART_COLORS.grid} />
						<XAxis dataKey="date" stroke={CHART_COLORS.axis} fontSize={12} tickLine={false} axisLine={false} />
						<YAxis stroke={CHART_COLORS.axis} fontSize={12} tickLine={false} axisLine={false} unit="%" domain={[0, 'auto']} />
						<Tooltip contentStyle={{ borderRadius: '12px', border: `1px solid ${CHART_COLORS.tooltipBorder}`, backgroundColor: CHART_COLORS.tooltipBackground, color: CHART_COLORS.tooltipText, boxShadow: '0 20px 45px -28px rgb(15 23 42 / 0.45)' }} formatter={percentTooltipFormatter} />
						<Line type="monotone" dataKey="proteinCalRatio" stroke="#8b5cf6" strokeWidth={2} dot={{ fill: '#8b5cf6', r: 3 }} name="P/Cal Ratio (%)" />
						{history.some(d => d.goalPcal) && (
							<Line type="stepAfter" dataKey="goalPcal" stroke="#8b5cf6" strokeDasharray="6 3" strokeWidth={2} dot={false} name="P/Cal Goal (%)" />
						)}
					</ComposedChart>
				</ResponsiveContainer>
			</div>

			{/* Fiber Trend */}
			{history.some(d => d.fiber > 0) && (
				<div className="card p-6 h-72">
					<h2 className="font-semibold text-slate-900 dark:text-slate-100 mb-4 flex items-center gap-2">
						<i className="ri-leaf-line text-green-500" />
						Fiber Intake (Last {rangeDays} Days)
					</h2>
					<ResponsiveContainer width="100%" height="85%">
						<ComposedChart data={history}>
							<CartesianGrid strokeDasharray="3 3" vertical={false} stroke={CHART_COLORS.grid} />
							<XAxis dataKey="date" stroke={CHART_COLORS.axis} fontSize={12} tickLine={false} axisLine={false} />
							<YAxis stroke={CHART_COLORS.axis} fontSize={12} tickLine={false} axisLine={false} unit="g" domain={[0, 'auto']} />
							<Tooltip contentStyle={{ borderRadius: '12px', border: `1px solid ${CHART_COLORS.tooltipBorder}`, backgroundColor: CHART_COLORS.tooltipBackground, color: CHART_COLORS.tooltipText, boxShadow: '0 20px 45px -28px rgb(15 23 42 / 0.45)' }} formatter={gramsTooltipFormatter} />
							<Line type="monotone" dataKey="fiber" stroke="#22c55e" strokeWidth={2} dot={{ fill: '#22c55e', r: 3 }} name="Fiber (g)" />
							{history.some(d => d.goalFiber) && (
								<Line type="stepAfter" dataKey="goalFiber" stroke="#22c55e" strokeDasharray="6 3" strokeWidth={2} dot={false} name="Fiber Goal (g)" />
							)}
						</ComposedChart>
					</ResponsiveContainer>
				</div>
			)}

			{/* Meal List */}
			<div className="card p-6">
				<div className="flex items-center justify-between mb-4">
					<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
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
								className="flex items-center justify-between rounded-xl bg-slate-50 p-4 transition-colors hover:bg-slate-100 dark:bg-slate-900/60 dark:hover:bg-slate-900/90"
							>
								<div>
									<h3 className="font-medium text-slate-900 dark:text-slate-100">
										{meal.name || meal.mealType}
									</h3>
									<p className="text-sm capitalize text-slate-500 dark:text-slate-400">{meal.mealType} • {new Date(meal.loggedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</p>
								</div>
								<div className="flex items-center gap-4">
									<div className="text-right">
										<p className="font-semibold text-orange-600">
											{meal.calories || 0} kcal
										</p>
										<div className="mt-1 flex gap-2 text-xs text-slate-500 dark:text-slate-400">
											<span>P: {meal.proteinGrams?.toFixed(1)}g</span>
											<span>C: {meal.carbsGrams?.toFixed(1)}g</span>
											<span>F: {meal.fatGrams?.toFixed(1)}g</span>
											{(meal.fiberGrams ?? 0) > 0 && <span>Fiber: {meal.fiberGrams?.toFixed(1)}g</span>}
											{(meal.calories || 0) > 0 && (meal.proteinGrams || 0) > 0 && <span className="text-violet-600">P/Cal: {((meal.proteinGrams! * 4 / meal.calories!) * 100).toFixed(0)}%</span>}
										</div>
									</div>
									<button
										onClick={() => handleDeleteClick(meal.id, meal.name || meal.mealType)}
										className="rounded-lg p-2 text-red-600 transition-colors hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-500/10"
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
						<div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-slate-100 dark:bg-slate-900/80">
							<i className="ri-restaurant-line text-3xl text-slate-400 dark:text-slate-500" />
						</div>
						<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">
							No meals logged
						</h3>
						<p className="text-slate-500 dark:text-slate-400">Log your first meal for this day to start tracking totals.</p>
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
