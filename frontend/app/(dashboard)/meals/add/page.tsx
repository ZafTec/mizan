"use client";

import { FieldError } from "@/components/FieldError";
import Loading from "@/components/Loading";
import { addMeal } from "@/data/meal";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useActionState, useEffect, useRef } from "react";

export default function Page() {
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
		<div className="max-w-3xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex items-center gap-4">
				<Link href="/meals" className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-slate-600" />
				</Link>
				<div>
					<h1 className="text-2xl font-bold text-slate-900">Log Meal</h1>
					<p className="text-slate-500">Track what you eat</p>
				</div>
			</div>

			<form action={action} className="space-y-6">
				{/* Basic Info Card */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
						<i className="ri-restaurant-2-line text-brand-500" />
						Meal Details
					</h2>

					<div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
						<div>
							<label htmlFor="name" className="label">Meal Name</label>
							<input
								type="text"
								id="name"
								name="name"
								className="input"
								placeholder="e.g., Lunch, Protein Shake"
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

				{/* Nutrition Card */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
						<i className="ri-heart-pulse-line text-brand-500" />
						Nutritional Information
					</h2>

					<div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
						<div>
							<label htmlFor="calories" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-full bg-orange-500" />
									Calories (kcal)
								</span>
							</label>
							<input
								type="number"
								id="calories"
								name="calories"
								min={0}
								defaultValue={0}
								className="input"
							/>
							<FieldError formState={formState} name="calories" />
						</div>
						<div>
							<label htmlFor="protein" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-full bg-red-500" />
									Protein (g)
								</span>
							</label>
							<input
								type="number"
								id="protein"
								name="protein"
								min={0}
								step="0.1"
								defaultValue={0}
								className="input"
							/>
							<FieldError formState={formState} name="protein" />
						</div>
						<div>
							<label htmlFor="carbs" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-full bg-amber-500" />
									Carbs (g)
								</span>
							</label>
							<input
								type="number"
								id="carbs"
								name="carbs"
								min={0}
								step="0.1"
								defaultValue={0}
								className="input"
							/>
							<FieldError formState={formState} name="carbs" />
						</div>
						<div>
							<label htmlFor="fat" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-full bg-yellow-500" />
									Fat (g)
								</span>
							</label>
							<input
								type="number"
								id="fat"
								name="fat"
								min={0}
								step="0.1"
								defaultValue={0}
								className="input"
							/>
							<FieldError formState={formState} name="fat" />
						</div>
						<div>
							<label htmlFor="fiber" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-full bg-green-500" />
									Fiber (g)
								</span>
							</label>
							<input
								type="number"
								id="fiber"
								name="fiber"
								min={0}
								step="0.1"
								defaultValue={0}
								className="input"
							/>
							<FieldError formState={formState} name="fiber" />
						</div>
					</div>
				</div>

				{/* Quick Add from Recipe */}
				<div className="card p-6 bg-linear-to-br from-accent-50 to-brand-50">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-xl bg-white flex items-center justify-center">
							<i className="ri-book-open-line text-xl text-brand-600" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">Add from Recipe</h3>
							<p className="text-sm text-slate-600">Auto-fill nutrition from your saved recipes</p>
						</div>
						<Link href="/recipes" className="btn-secondary text-sm">
							Browse Recipes
						</Link>
					</div>
				</div>

				{/* Warnings from backend nutrition consistency check */}
				{formState.status === "success" && formState.warnings?.length ? (
					<div ref={warningsRef} className="space-y-4">
						<div className="p-4 rounded-xl bg-amber-50 border border-amber-200">
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
						<Link href="/meals" className="btn-primary w-full py-3 flex items-center justify-center gap-2">
							<i className="ri-arrow-right-line text-xl" />
							Continue to Diary
						</Link>
					</div>
				) : formState.status === "error" && formState.message ? (
					<div className="flex items-center gap-2 p-4 rounded-xl bg-red-50 text-red-600">
						<i className="ri-error-warning-line text-xl" />
						<span>{formState.message}</span>
					</div>
				) : null}

				{/* Submit Button */}
				<button
					type="submit"
					disabled={isPending}
					className="btn-primary w-full py-3"
				>
					{isPending ? (
						<>
							<Loading size="sm" />
							Logging Meal...
						</>
					) : (
						<>
							<i className="ri-check-line text-xl" />
							Log Meal
						</>
					)}
				</button>
			</form>
		</div>
	);
}
