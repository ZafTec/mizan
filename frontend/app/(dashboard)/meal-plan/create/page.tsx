"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { createMealPlan } from "@/data/mealPlan";
import RecipeSearchModal from "./RecipeSearchModal";

const MEAL_TYPES = ["breakfast", "lunch", "dinner", "snack"] as const;
const MEAL_LABELS: Record<string, string> = {
	breakfast: "Breakfast",
	lunch: "Lunch",
	dinner: "Dinner",
	snack: "Snack",
};
const MEAL_ICONS: Record<string, string> = {
	breakfast: "ri-sun-line",
	lunch: "ri-restaurant-line",
	dinner: "ri-moon-line",
	snack: "ri-cake-2-line",
};

interface SlotRecipe {
	recipeId: string;
	title: string;
	servings: number;
	calories?: number;
}

type CalendarSlots = Record<string, Record<string, SlotRecipe[]>>;

function getMonday(date: Date): Date {
	const d = new Date(date);
	const day = d.getDay();
	const diff = d.getDate() - day + (day === 0 ? -6 : 1);
	d.setDate(diff);
	return d;
}

function formatDateKey(date: Date): string {
	return date.toISOString().split("T")[0];
}

function formatDateLabel(date: Date): string {
	return date.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" });
}

export default function CreateMealPlanPage() {
	const router = useRouter();
	const [name, setName] = useState("");
	const [startDate] = useState(() => getMonday(new Date()));
	const [slots, setSlots] = useState<CalendarSlots>({});
	const [activeSlot, setActiveSlot] = useState<{ dateKey: string; mealType: string } | null>(null);
	const [isSubmitting, setIsSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const days = Array.from({ length: 7 }, (_, i) => {
		const d = new Date(startDate);
		d.setDate(d.getDate() + i);
		return d;
	});
	const endDate = days[6];

	const handleAddRecipe = (recipe: { id: string; title: string; nutrition?: { caloriesPerServing?: number } }, servings: number) => {
		if (!activeSlot) return;
		const { dateKey, mealType } = activeSlot;
		setSlots((prev) => {
			const daySlots = prev[dateKey] || {};
			const mealSlots = daySlots[mealType] || [];
			return {
				...prev,
				[dateKey]: {
					...daySlots,
					[mealType]: [...mealSlots, {
						recipeId: recipe.id,
						title: recipe.title,
						servings,
						calories: recipe.nutrition?.caloriesPerServing,
					}],
				},
			};
		});
		setActiveSlot(null);
	};

	const handleRemoveRecipe = (dateKey: string, mealType: string, index: number) => {
		setSlots((prev) => {
			const daySlots = { ...prev[dateKey] };
			const mealSlots = [...(daySlots[mealType] || [])];
			mealSlots.splice(index, 1);
			daySlots[mealType] = mealSlots;
			return { ...prev, [dateKey]: daySlots };
		});
	};

	const totalRecipes = Object.values(slots).reduce(
		(sum, day) => sum + Object.values(day).reduce((s, meals) => s + meals.length, 0),
		0
	);

	const handleSubmit = async () => {
		if (totalRecipes === 0) {
			setError("Add at least one recipe to your meal plan");
			return;
		}

		setIsSubmitting(true);
		setError(null);

		const recipes: { recipeId: string; date: string; mealType: string; servings: number }[] = [];
		Object.entries(slots).forEach(([dateKey, daySlots]) => {
			Object.entries(daySlots).forEach(([mealType, mealRecipes]) => {
				mealRecipes.forEach((r) => {
					recipes.push({
						recipeId: r.recipeId,
						date: dateKey,
						mealType,
						servings: r.servings,
					});
				});
			});
		});

		try {
			const result = await createMealPlan({
				name: name || `Week of ${formatDateLabel(startDate)}`,
				startDate: formatDateKey(startDate),
				endDate: formatDateKey(endDate),
				recipes,
			});

			if (result?.success) {
				router.push("/meal-plan");
				router.refresh();
			} else {
				setError("Failed to create meal plan");
			}
		} catch {
			setError("Failed to create meal plan");
		} finally {
			setIsSubmitting(false);
		}
	};

	return (
		<div className="space-y-6">
			<div className="flex items-center gap-4">
				<Link href="/meal-plan" className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-slate-600" />
				</Link>
				<div className="flex-1">
					<h1 className="text-2xl font-bold text-slate-900">Create Meal Plan</h1>
					<p className="text-slate-500">
						{formatDateLabel(startDate)} - {formatDateLabel(endDate)}
					</p>
				</div>
				<button
					onClick={handleSubmit}
					disabled={isSubmitting || totalRecipes === 0}
					className="btn-primary disabled:opacity-50"
				>
					{isSubmitting ? (
						<><i className="ri-loader-4-line animate-spin" /> Saving...</>
					) : (
						<><i className="ri-check-line" /> Save Plan ({totalRecipes})</>
					)}
				</button>
			</div>

			<div className="card p-4">
				<label className="label">Plan Name (optional)</label>
				<input
					type="text"
					value={name}
					onChange={(e) => setName(e.target.value)}
					placeholder={`Week of ${formatDateLabel(startDate)}`}
					className="input"
				/>
			</div>

			{error && (
				<div className="flex items-center gap-2 p-4 rounded-xl bg-red-50 text-red-600">
					<i className="ri-error-warning-line text-xl" />
					<span>{error}</span>
				</div>
			)}

			{/* Weekly Calendar Grid */}
			<div className="grid grid-cols-1 lg:grid-cols-7 gap-3">
				{days.map((day) => {
					const dateKey = formatDateKey(day);
					const isToday = formatDateKey(new Date()) === dateKey;
					return (
						<div key={dateKey} className={`card p-3 ${isToday ? "ring-2 ring-brand-400" : ""}`}>
							<div className="text-center mb-3">
								<p className="text-xs font-medium text-slate-500 uppercase">
									{day.toLocaleDateString(undefined, { weekday: "short" })}
								</p>
								<p className={`text-lg font-bold ${isToday ? "text-brand-600" : "text-slate-900"}`}>
									{day.getDate()}
								</p>
							</div>
							<div className="space-y-2">
								{MEAL_TYPES.map((mealType) => {
									const mealRecipes = slots[dateKey]?.[mealType] || [];
									return (
										<div key={mealType}>
											<div className="flex items-center justify-between mb-1">
												<span className="text-[10px] font-medium text-slate-400 uppercase flex items-center gap-1">
													<i className={`${MEAL_ICONS[mealType]} text-xs`} />
													{MEAL_LABELS[mealType]}
												</span>
												<button
													onClick={() => setActiveSlot({ dateKey, mealType })}
													className="w-5 h-5 rounded-md bg-slate-100 hover:bg-brand-100 hover:text-brand-600 flex items-center justify-center text-slate-400 transition-colors"
												>
													<i className="ri-add-line text-xs" />
												</button>
											</div>
											{mealRecipes.map((recipe, idx) => (
												<div key={idx} className="group flex items-center gap-1.5 p-1.5 bg-slate-50 rounded-lg text-xs">
													<span className="flex-1 truncate text-slate-700">{recipe.title}</span>
													<span className="text-slate-400 shrink-0">{recipe.servings}x</span>
													<button
														onClick={() => handleRemoveRecipe(dateKey, mealType, idx)}
														className="opacity-0 group-hover:opacity-100 text-red-400 hover:text-red-600 transition-opacity"
													>
														<i className="ri-close-line" />
													</button>
												</div>
											))}
										</div>
									);
								})}
							</div>
						</div>
					);
				})}
			</div>

			{activeSlot && (
				<RecipeSearchModal
					onSelect={handleAddRecipe}
					onClose={() => setActiveSlot(null)}
				/>
			)}
		</div>
	);
}
