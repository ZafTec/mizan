import { getUserServer } from "@/helper/session";
import Link from "next/link";
import Image from "next/image";
import { getMealPlans } from "@/data/mealPlan";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";
import MealPlanListItem from "./MealPlanListItem";

import { logger } from "@/lib/logger";
const mealLogger = logger.createModuleLogger("meal-plan-page");

export const dynamic = 'force-dynamic';

export default async function MealPlanPage({
	searchParams,
}: {
	searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
	const user = await getUserServer();
	const params = await searchParams;
	const { page, sortBy, sortOrder } = parseListParams(params);
	const baseUrl = buildListUrl('/meal-plan', { sortBy, sortOrder });

	let mealPlans: Awaited<ReturnType<typeof getMealPlans>>['mealPlans'] = [];
	let totalCount = 0;
	let totalPages = 0;
	let loadError: string | null = null;

	try {
		const result = await getMealPlans(page, 20, sortBy ?? undefined, sortOrder);
		mealPlans = result.mealPlans;
		totalCount = result.totalCount;
		totalPages = result.totalPages;
	} catch (error) {
		mealLogger.error("Failed to load meal plans", {
			error: error instanceof Error ? error.message : String(error),
			userID: user.id,
		});
		loadError = "Failed to load meal plans";
	}

	if (loadError) {
		return (
			<div className="space-y-6" data-testid="meal-plan-page">
				<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
					<div>
						<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Meal Planning</h1>
						<p className="text-slate-500 dark:text-slate-400 mt-1">Plan your meals for the week ahead</p>
					</div>
					<Link href="/meal-plan/create" className="btn-primary">
						<i className="ri-add-line" />
						Create Meal Plan
					</Link>
				</div>

				<div className="card p-6">
					<div className="flex items-center gap-3 p-4 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400">
						<i className="ri-error-warning-line text-xl" />
						<p>An error occurred loading your meal plans. Please try again later.</p>
					</div>
				</div>
			</div>
		);
	}

	return (
		<div className="space-y-6" data-testid="meal-plan-page">
				{/* Page Header */}
				<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
					<div>
						<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Meal Planning</h1>
						<p className="text-slate-500 dark:text-slate-400 mt-1">Plan your meals for the week ahead</p>
					</div>
					<div className="flex gap-3">
						<Link href="/meal-plan/shopping-list" className="btn-secondary">
							<i className="ri-shopping-cart-line" />
							Shopping List
						</Link>
						<Link href="/meal-plan/create" className="btn-primary">
							<i className="ri-add-line" />
							Create Meal Plan
						</Link>
					</div>
				</div>

				{/* Quick Stats */}
				<div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
					{[
						{ label: "Meal Plans", value: mealPlans.length, icon: "ri-calendar-check-line", color: "bg-brand-600" },
						{ label: "This Week", value: mealPlans.filter(p => p.recipes?.length > 0).length, icon: "ri-calendar-line", color: "bg-accent-600" },
						{ label: "Total Meals", value: mealPlans.reduce((acc, p) => acc + (p.recipes?.length || 0), 0), icon: "ri-restaurant-line", color: "bg-slate-900 dark:bg-slate-100 dark:text-slate-900" },
						{ label: "Recipes", value: new Set(mealPlans.flatMap(p => p.recipes?.map(m => m.recipeId) || [])).size, icon: "ri-book-3-line", color: "bg-slate-700" },
					].map((stat) => (
						<div key={stat.label} className="card p-4">
							<div className="flex items-center gap-3">
								<div className={`w-10 h-10 rounded-xl ${stat.color} flex items-center justify-center text-white`}>
									<i className={`${stat.icon} text-white`} />
								</div>
								<div>
									<p className="text-2xl font-bold text-slate-900 dark:text-slate-100">{stat.value}</p>
									<p className="text-xs text-slate-500 dark:text-slate-400">{stat.label}</p>
								</div>
							</div>
						</div>
					))}
				</div>

				{/* Meal Plans */}
				<div className="card p-6">
					<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2 mb-4">
						<i className="ri-calendar-2-line text-brand-500 dark:text-brand-400" />
						Your Meal Plans
					</h2>
					{mealPlans.length > 0 ? (
						<div className="space-y-4">
							{mealPlans.map((plan) => (
								<MealPlanListItem key={plan.id} plan={plan} />
							))}
						</div>
					) : (
						<div className="text-center py-12 flex flex-col items-center">
							<div className="relative w-48 h-48 mb-6 opacity-90 drop-shadow-md">
								<Image src="/assets/meal-planner-empty.svg" alt="Empty Meal Planner" fill className="object-contain" />
							</div>
							<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No meal plans yet</h3>
							<p className="text-slate-500 mb-4">Start planning your meals for the week</p>
							<Link href="/meal-plan/create" className="btn-primary">
								<i className="ri-add-line" />
								Create Meal Plan
							</Link>
						</div>
					)}
				</div>

				{totalPages > 1 && (
					<Pagination
						currentPage={page}
						totalPages={totalPages}
						totalCount={totalCount}
						pageSize={20}
						baseUrl={baseUrl}
					/>
				)}
		</div>
	);
}
