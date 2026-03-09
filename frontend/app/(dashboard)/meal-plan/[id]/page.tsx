import { getMealPlanById } from "@/data/mealPlan";
import { notFound } from "next/navigation";
import Link from "next/link";
import MealPlanActions from "./MealPlanActions";

export const dynamic = "force-dynamic";

export default async function MealPlanDetailPage({
	params,
}: {
	params: Promise<{ id: string }>;
}) {
	const { id } = await params;
	const plan = await getMealPlanById(id);

	if (!plan) {
		notFound();
	}

	const recipesByDate = plan.recipes.reduce<Record<string, typeof plan.recipes>>((acc, r) => {
		const key = r.date;
		if (!acc[key]) acc[key] = [];
		acc[key].push(r);
		return acc;
	}, {});

	const sortedDates = Object.keys(recipesByDate).sort();

	return (
		<div className="space-y-6" data-testid="meal-plan-detail-page">
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<div className="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400 mb-1">
						<Link href="/meal-plan" className="hover:text-brand-600 dark:hover:text-brand-400 transition-colors">
							Meal Plans
						</Link>
						<i className="ri-arrow-right-s-line" />
						<span className="text-slate-900 dark:text-slate-100">{plan.name || "Meal Plan"}</span>
					</div>
					<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">{plan.name || "Meal Plan"}</h1>
					<p className="text-slate-500 dark:text-slate-400 mt-1">
						{plan.startDate} to {plan.endDate}
					</p>
				</div>
				<MealPlanActions planId={plan.id} planName={plan.name} />
			</div>

			{plan.nutritionSummary && (
				<div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
					{[
						{ label: "Total Calories", value: Math.round(plan.nutritionSummary.totalCalories), unit: "kcal", icon: "ri-fire-line", color: "bg-accent-600" },
						{ label: "Avg/Day", value: Math.round(plan.nutritionSummary.avgCaloriesPerDay), unit: "kcal", icon: "ri-bar-chart-line", color: "bg-brand-600" },
						{ label: "Protein", value: Math.round(plan.nutritionSummary.totalProteinGrams), unit: "g", icon: "ri-heart-pulse-line", color: "bg-slate-900 dark:bg-slate-100 dark:text-slate-900" },
						{ label: "Days", value: plan.nutritionSummary.daysCount, unit: "", icon: "ri-calendar-line", color: "bg-slate-700" },
					].map((stat) => (
						<div key={stat.label} className="card p-4">
							<div className="flex items-center gap-3">
								<div className={`w-10 h-10 rounded-xl ${stat.color} flex items-center justify-center text-white`}>
									<i className={`${stat.icon} text-current`} />
								</div>
								<div>
									<p className="text-2xl font-bold text-slate-900 dark:text-slate-100">
										{stat.value}{stat.unit && <span className="ml-1 text-sm font-normal text-slate-500 dark:text-slate-400">{stat.unit}</span>}
									</p>
									<p className="text-xs text-slate-500 dark:text-slate-400">{stat.label}</p>
								</div>
							</div>
						</div>
					))}
				</div>
			)}

			<div className="card p-6">
				<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2 mb-4">
					<i className="ri-restaurant-line text-brand-500 dark:text-brand-400" />
					Scheduled Meals
				</h2>

				{plan.recipes.length > 0 ? (
					<div className="space-y-6">
						{sortedDates.map((date) => (
							<div key={date}>
								<h3 className="text-sm font-medium text-slate-500 dark:text-slate-400 mb-3 flex items-center gap-2">
									<i className="ri-calendar-event-line" />
									{new Date(date + "T00:00:00").toLocaleDateString("en-US", { weekday: "long", month: "short", day: "numeric" })}
								</h3>
								<div className="space-y-2">
									{recipesByDate[date].map((recipe) => (
										<div key={recipe.id} className="flex items-center gap-4 p-3 bg-slate-50 dark:bg-slate-800 rounded-xl">
											<div className="w-8 h-8 rounded-lg bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-xs font-medium text-slate-600 dark:text-slate-300 uppercase">
												{recipe.mealType.slice(0, 1)}
											</div>
											<div className="flex-1 min-w-0">
												<Link
													href={`/recipes/${recipe.recipeId}`}
													className="text-sm font-medium text-slate-900 dark:text-slate-100 hover:text-brand-600 dark:hover:text-brand-400 transition-colors truncate block"
												>
													{recipe.recipeTitle || "Recipe"}
												</Link>
												<p className="text-xs text-slate-500 dark:text-slate-400">
													{recipe.mealType} &middot; {recipe.servings} serving{recipe.servings !== 1 ? "s" : ""}
													{recipe.caloriesPerServing ? ` · ${Math.round(recipe.caloriesPerServing * recipe.servings)} kcal` : ""}
												</p>
											</div>
										</div>
									))}
								</div>
							</div>
						))}
					</div>
				) : (
					<div className="text-center py-12">
						<div className="w-16 h-16 rounded-2xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center mx-auto mb-4">
							<i className="ri-restaurant-line text-3xl text-slate-400 dark:text-slate-500" />
						</div>
						<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No meals scheduled</h3>
						<p className="text-slate-500 dark:text-slate-400 mb-4">Add recipes to this meal plan</p>
						<Link href={`/meal-plan/${plan.id}/edit`} className="btn-primary">
							<i className="ri-add-line" />
							Edit Plan
						</Link>
					</div>
				)}
			</div>
		</div>
	);
}
