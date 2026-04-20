"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { deleteMealPlan } from "@/data/mealPlan";
import { DeleteConfirmModal } from "@/components/DeleteConfirmModal";
import { appToast } from "@/lib/toast";

interface MealPlanListItemProps {
    plan: {
        id: string;
        name: string;
        startDate: string;
        endDate: string;
        recipes: { id: string; recipeId: string; recipeTitle?: string; date: string; mealType: string; servings: number }[];
    };
}

export default function MealPlanListItem({ plan }: MealPlanListItemProps) {
    const router = useRouter();
    const [showDelete, setShowDelete] = useState(false);

    const handleDelete = async () => {
        const success = await deleteMealPlan(plan.id);
        if (success) {
            appToast.success("Meal plan deleted");
            router.refresh();
        } else {
            appToast.error("Failed to delete meal plan");
        }
    };

    return (
        <>
            <div className="p-4 bg-slate-50 dark:bg-slate-800 rounded-xl">
                <div className="flex justify-between items-start gap-4">
                    <Link href={`/meal-plan/${plan.id}`} className="flex-1 min-w-0 group">
                        <h3 className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100 group-hover:text-brand-600 dark:group-hover:text-brand-400 transition-colors">
                            {plan.name || "Weekly Plan"}
                        </h3>
                        <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                            {plan.startDate} to {plan.endDate}
                        </p>
                        <p className="text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500 mt-1">
                            {plan.recipes?.length || 0} meals
                        </p>
                    </Link>
                    <div className="flex items-center gap-1">
                        <Link
                            href={`/meal-plan/${plan.id}`}
                            className="p-2 text-slate-400 hover:text-brand-600 dark:hover:text-brand-400 transition-colors"
                            title="View"
                        >
                            <i className="ri-eye-line" />
                        </Link>
                        <Link
                            href={`/meal-plan/${plan.id}/edit`}
                            className="p-2 text-slate-400 hover:text-brand-600 dark:hover:text-brand-400 transition-colors"
                            title="Edit"
                        >
                            <i className="ri-edit-line" />
                        </Link>
                        <button
                            onClick={() => setShowDelete(true)}
                            className="p-2 text-slate-400 hover:text-red-600 dark:hover:text-red-400 transition-colors"
                            title="Delete"
                        >
                            <i className="ri-delete-bin-line" />
                        </button>
                    </div>
                </div>
            </div>

            <DeleteConfirmModal
                isOpen={showDelete}
                onClose={() => setShowDelete(false)}
                onConfirm={handleDelete}
                itemName="Meal Plan"
            />
        </>
    );
}
