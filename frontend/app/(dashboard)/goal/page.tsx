"use client";

import { FieldError } from "@/components/FieldError";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { useActionState } from "react";
import { createGoal } from "@/data/goal";

export default function GoalForm() {
	const [formState, action, isPending] = useActionState(createGoal, EMPTY_FORM_STATE);

	return (
		<div className="max-w-3xl mx-auto space-y-6">
			{/* Header */}
			<div>
				<h1 className="text-2xl font-bold text-slate-900">Nutrition Goals</h1>
				<p className="text-slate-500 mt-1">Set your daily macro targets to track your progress</p>
			</div>

			{/* Info Card */}
			<div className="card p-6 bg-gradient-to-br from-brand-50 to-accent-50">
				<div className="flex items-start gap-4">
					<div className="w-12 h-12 rounded-xl bg-white flex items-center justify-center flex-shrink-0">
						<i className="ri-target-line text-xl text-brand-600" />
					</div>
					<div>
						<h3 className="font-semibold text-slate-900">Why Set Goals?</h3>
						<p className="text-sm text-slate-600 mt-1">
							Setting nutrition goals helps you stay on track. Our AI coach will use these targets
							to provide personalized recipe suggestions and track your daily progress.
						</p>
					</div>
				</div>
			</div>

			<form action={action} className="space-y-6">
				{/* Goal Name Card */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
						<i className="ri-bookmark-line text-brand-500" />
						Goal Name
					</h2>
					<div>
						<label htmlFor="name" className="label">What are you working towards?</label>
						<input
							type="text"
							id="name"
							name="name"
							className="input"
							placeholder="e.g., Weight Loss, Muscle Gain, Maintenance"
						/>
						<FieldError formState={formState} name="name" />
					</div>
				</div>

				{/* Macro Targets Card */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
						<i className="ri-pie-chart-2-line text-brand-500" />
						Daily Macro Targets
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
								placeholder="2000"
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
								placeholder="150"
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
								placeholder="200"
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
								placeholder="65"
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
								placeholder="30"
								className="input"
							/>
							<FieldError formState={formState} name="fiber" />
						</div>
					</div>
				</div>

				{/* Status Messages */}
				{formState.status === "success" && (
					<div className="flex items-center gap-2 p-4 rounded-xl bg-green-50 text-green-600">
						<i className="ri-checkbox-circle-line text-xl" />
						<span>Goal updated successfully!</span>
					</div>
				)}

				{formState.status === "error" && formState.message && (
					<div className="flex items-center gap-2 p-4 rounded-xl bg-red-50 text-red-600">
						<i className="ri-error-warning-line text-xl" />
						<span>{formState.message}</span>
					</div>
				)}

				{/* Submit Button */}
				<button
					type="submit"
					disabled={isPending}
					className="btn-primary w-full py-3"
				>
					{isPending ? (
						<>
							<svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
								<circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
								<path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
							</svg>
							Saving Goal...
						</>
					) : (
						<>
							<i className="ri-save-line text-xl" />
							Save Goal
						</>
					)}
				</button>
			</form>
		</div>
	);
}