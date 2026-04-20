"use client";

import Meal from "@/types/meal";
import MealTypePill from "./MealTypePill";
import { useRouter } from "next/navigation";
import { useState, useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import Loading from "@/components/Loading";

interface MealCardProps {
  meal: Meal;
}

export default function MealCard({ meal }: MealCardProps) {
	const router = useRouter();
	const [showConfirm, setShowConfirm] = useState(false);
	const [isLoading, setIsLoading] = useState(false);
	const popupRef = useRef<HTMLDivElement>(null);

	// Close confirmation popup when clicking outside
	useEffect(() => {
		const handleClickOutside = (event: MouseEvent) => {
			if (popupRef.current && !popupRef.current.contains(event.target as Node)) {
				setShowConfirm(false);
			}
		};

		if (showConfirm) {
			document.addEventListener("mousedown", handleClickOutside);
		} else {
			document.removeEventListener("mousedown", handleClickOutside);
		}

		return () => {
			document.removeEventListener("mousedown", handleClickOutside);
		};
	}, [showConfirm]);

	// Handle delete action
	async function handleDelete() {
		setIsLoading(true);
		try {
			const res = await fetch(`/api/meals/${meal._id}`, {
				method: "DELETE",
			});
			if (res.ok) {
				setIsLoading(false);
				setShowConfirm(false);
				router.refresh();
			} else {
				setIsLoading(false);
				console.error("Error deleting meal");
			}
		} catch (error) {
			setIsLoading(false);
			console.error("Error deleting meal: ", error);
		}
	}

	return (
		<div className="bg-white dark:bg-charcoal-blue-900 rounded-lg shadow-md p-4 min-w-fit relative">
			<div className="flex flex-row justify-between items-center">
				<h2 className="text-lg font-semibold">{meal.name}</h2>
				<button
					className="text-red-500 hover:text-red-700 transition-colors duration-200 focus:outline-none"
					onClick={() => setShowConfirm(true)}
					disabled={isLoading}
				>
					<i className="ri-delete-bin-6-line"></i>
				</button>
			</div>

			<MealTypePill mealType={meal.mealType} />

			<div className="grid grid-cols-2 gap-4 mt-4 text-sm">
				<div>
					<p className="text-gray-500 dark:text-gray-400">Calories</p>
					<p className="font-semibold">{meal.totalMacros.calories}</p>
				</div>
				<div>
					<p className="text-gray-500 dark:text-gray-400">Protein</p>
					<p className="font-semibold">{meal.totalMacros.protein}</p>
				</div>
				<div>
					<p className="text-gray-500 dark:text-gray-400">Carbs</p>
					<p className="font-semibold">{meal.totalMacros.carbs}</p>
				</div>
				<div>
					<p className="text-gray-500 dark:text-gray-400">Fat</p>
					<p className="font-semibold">{meal.totalMacros.fat}</p>
				</div>
			</div>

		{/* Confirmation popup */}
		{showConfirm && typeof document !== "undefined" && createPortal(
			<div className="fixed inset-0 z-[100] flex items-center justify-center overflow-y-auto bg-black/50 p-4">
				<div ref={popupRef} className="my-auto w-full max-w-sm rounded-2xl bg-white p-6 shadow-xl dark:bg-charcoal-blue-900">
					<p className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						Confirm Deletion
					</p>
					<p className="mt-2 text-charcoal-blue-600 dark:text-charcoal-blue-400">
						Are you sure you want to delete this meal?
					</p>
					<div className="mt-4 flex justify-end space-x-2 text-sm">
						<button
							className="btn-secondary"
							onClick={() => setShowConfirm(false)}
							disabled={isLoading}
						>
							Cancel
						</button>
						<button
							className="btn-danger flex items-center"
							onClick={handleDelete}
							disabled={isLoading}
						>
							{isLoading && <Loading size="sm" />}
							{isLoading ? "Deleting..." : "Delete"}
						</button>
					</div>
				</div>
			</div>,
			document.body
		)}
		</div>
	)
}
