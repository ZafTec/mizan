'use client';

import { useActionState, useEffect, useRef } from "react";
import Link from "next/link";
import Loading from "@/components/Loading";
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
	const warningsRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		if (formState.status === "success" && !formState.warnings?.length) {
			router.push("/meals");
		}
		if (formState.status === "success" && formState.warnings?.length) {
			warningsRef.current?.scrollIntoView({ behavior: "smooth" });
		}
	}, [formState.status, formState.warnings, router]);

	return (
		<form action={action} className="space-y-6">
			<input type="hidden" name="recipeId" value={recipeId} />
			{/* Basic Info Card */}
			<div className="card p-6 space-y-5">
				<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
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
			<div className="card p-6 space-y-5 text-slate-900 dark:text-slate-100">
				<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
					<i className="ri-heart-pulse-line text-brand-500" />
					Nutritional Information
				</h2>

				<div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
					<div>
						<label htmlFor="calories" className="label text-slate-900 dark:text-slate-100">Calories (kcal)</label>
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
						<label htmlFor="protein" className="label text-slate-900 dark:text-slate-100">Protein (g)</label>
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
						<label htmlFor="carbs" className="label text-slate-900 dark:text-slate-100">Carbs (g)</label>
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
						<label htmlFor="fat" className="label text-slate-900 dark:text-slate-100">Fat (g)</label>
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
						<label htmlFor="fiber" className="label text-slate-900 dark:text-slate-100">Fiber (g)</label>
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
				{formState.status === "success" && formState.warnings?.length ? (
					<div ref={warningsRef} className="p-4 rounded-xl bg-amber-50 border border-amber-200">
						<div className="flex items-start gap-3">
							<i className="ri-error-warning-line text-xl text-amber-600 mt-0.5 shrink-0" />
							<div>
								<p className="font-semibold text-amber-800">Nutrition hint{formState.warnings.length > 1 ? "s" : ""}</p>
								<ul className="mt-2 space-y-1.5">
									{formState.warnings.map((w, i) => (
										<li key={i} className="text-sm text-amber-700">{w}</li>
									))}
								</ul>
								<p className="text-xs text-amber-500 mt-3">Your entry was saved. You can adjust it or continue to the diary.</p>
							</div>
						</div>
					</div>
				) : formState.status === "error" && formState.message ? (
					<div className="p-4 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 flex items-center gap-2">
						<i className="ri-error-warning-line text-xl" />
						<span>{formState.message}</span>
					</div>
				) : null}

				{formState.status === "success" && formState.warnings?.length ? (
					<Link href="/meals" className="btn-primary w-full py-3 flex items-center justify-center gap-2">
						<i className="ri-arrow-right-line text-xl" />
						Continue to Diary
					</Link>
				) : (
				<button
					type="submit"
					disabled={isPending}
					className="btn-primary w-full py-3.5 text-lg"
				>
					{isPending ? (
						<>
							<Loading size="sm" />
							Logging Meal...
						</>
					) : (
						<>
							<i className="ri-check-line text-xl" />
							Confirm & Log Meal
						</>
					)}
				</button>
				)}
			</div>
		</form>
	);
}
