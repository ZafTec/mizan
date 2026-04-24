import { getUserServer } from "@/helper/session";
import Link from "next/link";
import { getMealPlans } from "@/data/mealPlan";
import Pagination from "@/components/Pagination";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";
import MealPlanListItem from "./MealPlanListItem";
import { AppFeatureIllustration } from "@/components/illustrations/AppFeatureIllustration";

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
				<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
					<div className="space-y-2">
						<p className="eyebrow">Planner</p>
						<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							Meal planning
						</h1>
					</div>
					<Link href="/meal-plan/create" className="btn-primary">
						<i className="ri-add-line" />
						Create Meal Plan
					</Link>
				</header>

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
				<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
					<div className="space-y-2">
						<p className="eyebrow">Planner</p>
						<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
							Meal planning
						</h1>
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
				</header>

				{/* Quick Stats */}
				<div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
					{[
						{ label: "Meal Plans", value: mealPlans.length, icon: "ri-calendar-check-line", color: "bg-brand-600" },
						{ label: "This Week", value: mealPlans.filter(p => p.recipes?.length > 0).length, icon: "ri-calendar-line", color: "bg-accent-600" },
						{ label: "Total Meals", value: mealPlans.reduce((acc, p) => acc + (p.recipes?.length || 0), 0), icon: "ri-restaurant-line", color: "bg-charcoal-blue-900 dark:bg-charcoal-blue-100 dark:text-charcoal-blue-900" },
						{ label: "Recipes", value: new Set(mealPlans.flatMap(p => p.recipes?.map(m => m.recipeId) || [])).size, icon: "ri-book-3-line", color: "bg-charcoal-blue-700" },
					].map((stat) => (
						<div key={stat.label} className="card p-4">
							<div className="flex items-center gap-3">
								<div className={`w-10 h-10 rounded-xl ${stat.color} flex items-center justify-center text-white`}>
									<i className={`${stat.icon} text-current`} />
								</div>
								<div>
									<p className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">{stat.value}</p>
									<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{stat.label}</p>
								</div>
							</div>
						</div>
					))}
				</div>

				{/* Meal Plans */}
				<div className="card p-6">
					<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2 mb-4">
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
							<div className="mb-6 w-full max-w-[18rem] opacity-95 drop-shadow-md">
								<AppFeatureIllustration variant="meal-plan" className="h-auto w-full" />
							</div>
							<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-2">No meal plans yet</h3>
							<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mb-4">No meal plans yet.</p>
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
