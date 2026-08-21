"use client";

import Link from "next/link";
import { ProWall } from "@/components/billing/ProWall";

/**
 * The free plan allows one meal plan; the backend enforces it in
 * CreateMealPlanCommand. This puts the same rule in front of the button so the
 * user meets it before planning a week of meals, not after - docs/REFOCUS.md §5.
 */
export default function CreateMealPlanAction({ atFreeCap }: { atFreeCap: boolean }) {
	const create = (
		<Link href="/meal-plan/create" className="btn-primary">
			<i className="ri-add-line" />
			Create Meal Plan
		</Link>
	);

	if (!atFreeCap) return create;

	return (
		<ProWall
			title="One meal plan on the free plan"
			message="Pro lifts the cap: unlimited meal plans, and shopping lists generated from any of them."
			lockedLabel="Create Meal Plan"
		>
			{create}
		</ProWall>
	);
}
