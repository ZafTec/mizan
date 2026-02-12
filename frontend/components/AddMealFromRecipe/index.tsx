'use client';

import { useActionState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { addMeal } from "@/data/meal";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { FieldError } from "@/components/FieldError";
import Macros from "@/types/macro";

interface AddMealFromRecipeProps {
	recipeId: string;
	name: string;
	macros: Macros;
}

export default function AddMealFromRecipe({ recipeId, name, macros }: AddMealFromRecipeProps) {
	const [formState, action, isPending] = useActionState(addMeal, EMPTY_FORM_STATE);
	const router = useRouter();

	useEffect(() => {
		if (formState.status === "success") {
			router.push("/meals");
		}
	}, [formState.status, router]);

	return (
		<form action={action} className="space-y-6">
			<input type="hidden" name="recipeId" value={recipeId} />
			{/* Basic Info Card */}
			<div className="card p-6 space-y-5">
				<h2 className="font-semibold text-slate-900 flex items-center gap-2">
					<i className="ri-edit-line text-brand-500" />
					Log Details
				</h2>

				<div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
					<div>
						<label htmlFor="name" className="label">Meal Name</label>
						<input
							type="text"
							id="name"
							name="name"
							defaultValue={name}
							className="input"
							placeholder="e.g., Post-workout Lunch"
							required
						/>
						<FieldError formState={formState} name="name" />
					</div>
					<div>
						<label htmlFor="mealType" className="label">Meal Type</label>
						<select id="mealType" name="mealType" className="input">
							<option value="MEAL">Meal</option>
							<option value="SNACK">Snack</option>
							<option value="DRINK">Drink</option>
						</select>
						<FieldError formState={formState} name="mealType" />
					</div>
				</div>
			</div>

			{/* Nutrition Info Card */}
			<div className="card p-6 space-y-5 text-slate-900">
				<h2 className="font-semibold text-slate-900 flex items-center gap-2">
					<i className="ri-heart-pulse-line text-brand-500" />
					Nutritional Information
				</h2>

				<div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
					<div>
						<label htmlFor="calories" className="label text-slate-900">Calories (kcal)</label>
						<input
							type="number"
							id="calories"
							name="calories"
							defaultValue={macros.calories.toFixed(0)}
							className="input"
							min={0}
							required
						/>
						<FieldError formState={formState} name="calories" />
					</div>
					<div>
						<label htmlFor="protein" className="label text-slate-900">Protein (g)</label>
						<input
							type="number"
							id="protein"
							name="protein"
							defaultValue={macros.protein.toFixed(1)}
							className="input"
							min={0}
							step="0.1"
							required
						/>
						<FieldError formState={formState} name="protein" />
					</div>
					<div>
						<label htmlFor="carbs" className="label text-slate-900">Carbs (g)</label>
						<input
							type="number"
							id="carbs"
							name="carbs"
							defaultValue={macros.carbs.toFixed(1)}
							className="input"
							min={0}
							step="0.1"
							required
						/>
						<FieldError formState={formState} name="carbs" />
					</div>
					<div>
						<label htmlFor="fat" className="label text-slate-900">Fat (g)</label>
						<input
							type="number"
							id="fat"
							name="fat"
							defaultValue={macros.fat.toFixed(1)}
							className="input"
							min={0}
							step="0.1"
							required
						/>
						<FieldError formState={formState} name="fat" />
					</div>
					<div>
						<label htmlFor="fiber" className="label text-slate-900">Fiber (g)</label>
						<input
							type="number"
							id="fiber"
							name="fiber"
							defaultValue={macros.fiber.toFixed(1)}
							className="input"
							min={0}
							step="0.1"
							required
						/>
						<FieldError formState={formState} name="fiber" />
					</div>
				</div>
			</div>

			{/* Status & Submit */}
			<div className="space-y-4">
				{formState.status === "error" && formState.message && (
					<div className="p-4 rounded-xl bg-red-50 text-red-600 flex items-center gap-2">
						<i className="ri-error-warning-line text-xl" />
						<span>{formState.message}</span>
					</div>
				)}

				<button
					type="submit"
					disabled={isPending}
					className="btn-primary w-full py-3.5 text-lg"
				>
					{isPending ? (
						<>
							<svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
								<circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
								<path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
							</svg>
							Logging Meal...
						</>
					) : (
						<>
							<i className="ri-check-line text-xl" />
							Confirm & Log Meal
						</>
					)}
				</button>
			</div>
		</form>
	);
}
