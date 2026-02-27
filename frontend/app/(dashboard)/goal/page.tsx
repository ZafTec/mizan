"use client";

import { FieldError } from "@/components/FieldError";
import Loading from "@/components/Loading";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { useActionState, useState } from "react";
import { createGoal } from "@/data/goal";

const ACTIVITY_FACTORS = {
	sedentary: { label: "Sedentary (desk job, little/no exercise)", value: 1.2 },
	light: { label: "Light (exercise 1–3 days/week)", value: 1.375 },
	moderate: { label: "Moderate (exercise 3–5 days/week)", value: 1.55 },
	active: { label: "Active (hard exercise 6–7 days/week)", value: 1.725 },
	extra: { label: "Extra active (very hard exercise + physical job)", value: 1.9 },
} as const;

type ActivityKey = keyof typeof ACTIVITY_FACTORS;

function calcTDEE(
	sex: "male" | "female",
	age: number,
	weightKg: number,
	heightCm: number,
	activity: ActivityKey,
): number {
	const bmr =
		sex === "male"
			? 10 * weightKg + 6.25 * heightCm - 5 * age + 5
			: 10 * weightKg + 6.25 * heightCm - 5 * age - 161;
	return Math.round(bmr * ACTIVITY_FACTORS[activity].value);
}

export default function GoalForm() {
	const [formState, action, isPending] = useActionState(createGoal, EMPTY_FORM_STATE);
	const [caloriesValue, setCaloriesValue] = useState("");

	// TDEE calculator state
	const [tdeeOpen, setTdeeOpen] = useState(false);
	const [sex, setSex] = useState<"male" | "female">("male");
	const [age, setAge] = useState("");
	const [weightVal, setWeightVal] = useState("");
	const [weightUnit, setWeightUnit] = useState<"kg" | "lb">("kg");
	const [heightCm, setHeightCm] = useState("");
	const [heightFt, setHeightFt] = useState("");
	const [heightIn, setHeightIn] = useState("0");
	const [heightUnit, setHeightUnit] = useState<"cm" | "ft">("cm");
	const [activity, setActivity] = useState<ActivityKey>("moderate");

	const tdeeResult = (() => {
		const a = parseInt(age);
		const w = parseFloat(weightVal);
		const hCm =
			heightUnit === "cm"
				? parseFloat(heightCm)
				: (parseInt(heightFt) || 0) * 30.48 + (parseFloat(heightIn) || 0) * 2.54;
		const wKg = weightUnit === "lb" ? w * 0.453592 : w;
		if (!isNaN(a) && a > 0 && !isNaN(wKg) && wKg > 0 && hCm > 0) {
			return calcTDEE(sex, a, wKg, hCm, activity);
		}
		return null;
	})();

	return (
		<div className="max-w-3xl mx-auto space-y-6" data-testid="goal-page">
			{/* Header */}
			<div>
				<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Nutrition Goals</h1>
				<p className="text-slate-500 dark:text-slate-400 mt-1">Set your daily macro targets to track your progress</p>
			</div>

			{/* Info Card */}
			<div className="card p-6 bg-linear-to-br from-brand-50 to-accent-50">
				<div className="flex items-start gap-4">
					<div className="w-12 h-12 rounded-xl bg-white dark:bg-slate-900 flex items-center justify-center shrink-0">
						<i className="ri-target-line text-xl text-brand-600 dark:text-brand-400" />
					</div>
					<div>
						<h3 className="font-semibold text-slate-900 dark:text-slate-100">Why Set Goals?</h3>
						<p className="text-sm text-slate-600 dark:text-slate-400 mt-1">
							Setting nutrition goals helps you stay on track. Our AI coach will use these targets
							to provide personalized recipe suggestions and track your daily progress.
						</p>
					</div>
				</div>
			</div>

			{/* TDEE Calculator */}
			<div className="card overflow-hidden">
				<button
					type="button"
					onClick={() => setTdeeOpen((o) => !o)}
					className="w-full flex items-center justify-between p-6 text-left hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors"
				>
					<div className="flex items-center gap-3">
						<div className="w-9 h-9 rounded-lg bg-brand-100 dark:bg-brand-900/40 flex items-center justify-center shrink-0">
							<i className="ri-calculator-line text-brand-600 dark:text-brand-400" />
						</div>
						<div>
							<h2 className="font-semibold text-slate-900 dark:text-slate-100">Calculate TDEE</h2>
							<p className="text-sm text-slate-500 dark:text-slate-400">
								{tdeeResult
									? `Estimated: ${tdeeResult} kcal/day`
									: "Get your Total Daily Energy Expenditure"}
							</p>
						</div>
					</div>
					<i className={`ri-arrow-${tdeeOpen ? "up" : "down"}-s-line text-xl text-slate-400`} />
				</button>

				{tdeeOpen && (
					<div className="px-6 pb-6 space-y-5 border-t border-slate-100 dark:border-slate-800 pt-5">
						{/* Sex */}
						<div>
							<label className="label">Biological Sex</label>
							<div className="flex gap-2">
								{(["male", "female"] as const).map((s) => (
									<button
										key={s}
										type="button"
										onClick={() => setSex(s)}
										className={`flex-1 py-2 rounded-xl font-medium capitalize transition-colors ${
											sex === s
												? "bg-brand-500 text-white"
												: "bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-700"
										}`}
									>
										{s}
									</button>
								))}
							</div>
						</div>

						<div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
							{/* Age */}
							<div>
								<label className="label">Age (years)</label>
								<input
									type="number"
									className="input"
									placeholder="30"
									min={10}
									max={120}
									value={age}
									onChange={(e) => setAge(e.target.value)}
								/>
							</div>

							{/* Weight */}
							<div>
								<div className="flex items-center justify-between">
									<label className="label">Weight</label>
									<div className="flex gap-1 mb-1">
										{(["kg", "lb"] as const).map((u) => (
											<button
												key={u}
												type="button"
												onClick={() => setWeightUnit(u)}
												className={`text-xs px-2 py-0.5 rounded font-medium transition-colors ${
													weightUnit === u
														? "bg-brand-500 text-white"
														: "text-slate-400 hover:text-slate-600"
												}`}
											>
												{u}
											</button>
										))}
									</div>
								</div>
								<input
									type="number"
									className="input"
									placeholder={weightUnit === "kg" ? "70" : "154"}
									min={20}
									step="0.1"
									value={weightVal}
									onChange={(e) => setWeightVal(e.target.value)}
								/>
							</div>

							{/* Height */}
							<div>
								<div className="flex items-center justify-between">
									<label className="label">Height</label>
									<div className="flex gap-1 mb-1">
										{(["cm", "ft"] as const).map((u) => (
											<button
												key={u}
												type="button"
												onClick={() => setHeightUnit(u)}
												className={`text-xs px-2 py-0.5 rounded font-medium transition-colors ${
													heightUnit === u
														? "bg-brand-500 text-white"
														: "text-slate-400 hover:text-slate-600"
												}`}
											>
												{u}
											</button>
										))}
									</div>
								</div>
								{heightUnit === "cm" ? (
									<input
										type="number"
										className="input"
										placeholder="175"
										min={100}
										step="0.1"
										value={heightCm}
										onChange={(e) => setHeightCm(e.target.value)}
									/>
								) : (
									<div className="flex gap-2">
										<input
											type="number"
											className="input"
											placeholder="5 ft"
											min={3}
											max={8}
											value={heightFt}
											onChange={(e) => setHeightFt(e.target.value)}
										/>
										<input
											type="number"
											className="input"
											placeholder="9 in"
											min={0}
											max={11}
											value={heightIn}
											onChange={(e) => setHeightIn(e.target.value)}
										/>
									</div>
								)}
							</div>
						</div>

						{/* Activity Level */}
						<div>
							<label className="label">Activity Level</label>
							<select
								className="input"
								value={activity}
								onChange={(e) => setActivity(e.target.value as ActivityKey)}
							>
								{Object.entries(ACTIVITY_FACTORS).map(([key, { label }]) => (
									<option key={key} value={key}>
										{label}
									</option>
								))}
							</select>
						</div>

						{/* Result */}
						{tdeeResult ? (
							<div className="flex items-center justify-between gap-4 p-4 rounded-xl bg-brand-50 dark:bg-brand-950/60 border border-brand-200 dark:border-brand-800">
								<div>
									<p className="text-sm font-medium text-brand-600 dark:text-brand-400">
										Estimated TDEE
									</p>
									<p className="text-2xl font-bold text-brand-700 dark:text-brand-300">
										{tdeeResult} <span className="text-base font-normal">kcal/day</span>
									</p>
									<p className="text-xs text-brand-500 mt-0.5">Mifflin-St Jeor formula</p>
								</div>
								<button
									type="button"
									onClick={() => {
										setCaloriesValue(String(tdeeResult));
										setTdeeOpen(false);
									}}
									className="btn-primary shrink-0"
								>
									<i className="ri-check-line" />
									Use This
								</button>
							</div>
						) : (
							<p className="text-sm text-slate-400 dark:text-slate-500 text-center py-2">
								Fill in your details above to calculate
							</p>
						)}
					</div>
				)}
			</div>

			<form action={action} className="space-y-6">
				{/* Goal Name Card */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
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
					<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
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
								value={caloriesValue}
								onChange={(e) => setCaloriesValue(e.target.value)}
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
				{formState.status === "success" && !formState.warnings?.length && (
					<div className="flex items-center gap-2 p-4 rounded-xl bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-300">
						<i className="ri-checkbox-circle-line text-xl" />
						<span>Goal saved successfully!</span>
					</div>
				)}

				{formState.status === "success" && formState.warnings?.length ? (
					<div className="p-4 rounded-xl bg-amber-50 dark:bg-amber-950 border border-amber-200 dark:border-amber-800">
						<div className="flex items-start gap-3">
							<i className="ri-error-warning-line text-xl text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
							<div>
								<p className="font-semibold text-amber-800 dark:text-amber-300">Goal hint{formState.warnings.length > 1 ? "s" : ""}</p>
								<ul className="mt-2 space-y-1.5">
									{formState.warnings.map((w, i) => (
										<li key={i} className="text-sm text-amber-700 dark:text-amber-300">{w}</li>
									))}
								</ul>
								<p className="text-xs text-amber-500 dark:text-amber-400 mt-3">Your goal was saved. Review the hints above if you&#39;d like to adjust your targets.</p>
							</div>
						</div>
					</div>
				) : null}

				{formState.status === "error" && formState.message && (
					<div className="flex items-center gap-2 p-4 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-300">
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
							<Loading size="sm" />
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
