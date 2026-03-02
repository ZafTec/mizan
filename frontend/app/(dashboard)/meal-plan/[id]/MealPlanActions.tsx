"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { deleteMealPlan } from "@/data/mealPlan";
import { DeleteConfirmModal } from "@/components/DeleteConfirmModal";
import { toast } from "sonner";

interface MealPlanActionsProps {
	planId: string;
	planName: string;
}

export default function MealPlanActions({ planId, planName }: MealPlanActionsProps) {
	const router = useRouter();
	const [showDelete, setShowDelete] = useState(false);

	const handleDelete = async () => {
		const success = await deleteMealPlan(planId);
		if (success) {
			toast.success("Meal plan deleted");
			router.push("/meal-plan");
		} else {
			toast.error("Failed to delete meal plan");
		}
	};

	return (
		<>
			<div className="flex gap-2">
				<Link href={`/meal-plan/${planId}/edit`} className="btn-secondary">
					<i className="ri-edit-line" />
					Edit
				</Link>
				<button onClick={() => setShowDelete(true)} className="btn-danger">
					<i className="ri-delete-bin-line" />
					Delete
				</button>
			</div>

			<DeleteConfirmModal
				isOpen={showDelete}
				onClose={() => setShowDelete(false)}
				onConfirm={handleDelete}
				itemName={planName || "Meal Plan"}
			/>
		</>
	);
}
