import { getMealPlanById } from "@/data/mealPlan";
import { notFound } from "next/navigation";
import Link from "next/link";
import MealPlanEditForm from "./MealPlanEditForm";

export const dynamic = "force-dynamic";

export default async function MealPlanEditPage({
	params,
}: {
	params: Promise<{ id: string }>;
}) {
	const { id } = await params;
	const plan = await getMealPlanById(id);

	if (!plan) {
		notFound();
	}

	return (
		<div className="space-y-6" data-testid="meal-plan-edit-page">
			<div>
				<div className="flex items-center gap-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400 mb-1">
					<Link href="/meal-plan" className="hover:text-brand-600 dark:hover:text-brand-400 transition-colors">
						Meal Plans
					</Link>
					<i className="ri-arrow-right-s-line" />
					<Link href={`/meal-plan/${plan.id}`} className="hover:text-brand-600 dark:hover:text-brand-400 transition-colors">
						{plan.name || "Meal Plan"}
					</Link>
					<i className="ri-arrow-right-s-line" />
					<span className="text-charcoal-blue-900 dark:text-charcoal-blue-100">Edit</span>
				</div>
				<h1 className="text-2xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">Edit Meal Plan</h1>
			</div>

			<MealPlanEditForm plan={plan} />
		</div>
	);
}
