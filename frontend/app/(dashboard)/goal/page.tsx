"use client";

import { FieldError } from "@/components/FieldError";
import Loading from "@/components/Loading";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { useActionState, useState, useEffect } from "react";
import { createGoal, getCurrentGoal } from "@/data/goal";

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
	const [proteinValue, setProteinValue] = useState("");
	const [carbsValue, setCarbsValue] = useState("");
	const [fatValue, setFatValue] = useState("");
	const [fiberValue, setFiberValue] = useState("");
	const [goalType, setGoalType] = useState("");
	const [bodyFatValue, setBodyFatValue] = useState("");
	const [muscleMassValue, setMuscleMassValue] = useState("");
	const [pcalValue, setPcalValue] = useState("");
	const [targetWeightValue, setTargetWeightValue] = useState("");
	const [targetWeightUnit, setTargetWeightUnit] = useState<"kg" | "lb">("kg");
	const [targetDateValue, setTargetDateValue] = useState("");
	const [loadingGoal, setLoadingGoal] = useState(true);

	useEffect(() => {
		getCurrentGoal().then((goal) => {
			if (goal) {
				setCaloriesValue(goal.targetCalories?.toString() || "");
				setProteinValue(goal.targetProteinGrams?.toString() || "");
				setCarbsValue(goal.targetCarbsGrams?.toString() || "");
				setFatValue(goal.targetFatGrams?.toString() || "");
				setFiberValue(goal.targetFiberGrams?.toString() || "");
				setGoalType(goal.goalType || "");
				setBodyFatValue(goal.targetBodyFatPercentage?.toString() || "");
				setMuscleMassValue(goal.targetMuscleMassKg?.toString() || "");
				setPcalValue(goal.targetProteinCalorieRatio?.toString() || "");
				setTargetWeightValue(goal.targetWeight?.toString() || "");
				setTargetWeightUnit((goal.weightUnit as "kg" | "lb") || "kg");
				setTargetDateValue(goal.targetDate || "");
			}
			setLoadingGoal(false);
		});
	}, []);

	const pCalAutoProtein = (() => {
		const ratio = parseFloat(pcalValue);
		const cal = parseFloat(caloriesValue);
		if (!isNaN(ratio) && ratio > 0 && !isNaN(cal) && cal > 0) {
			return Math.round((ratio / 100) * cal / 4);
		}
		return null;
	})();

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
		<div className="max-w-3xl mx-auto space-y-6 lg:space-y-8" data-testid="goal-page">
			<header className="space-y-2">
				<p className="eyebrow">Targets</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Nutrition goals
				</h1>
				<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Set your daily macro targets.
				</p>
			</header>

			{/* TDEE Calculator */}
			<div className="card overflow-hidden">
				<button
					type="button"
					onClick={() => setTdeeOpen((o) => !o)}
					className="w-full flex items-center justify-between p-6 text-left hover:bg-charcoal-blue-50 dark:hover:bg-charcoal-blue-800/50 transition-colors"
				>
					<div className="flex items-center gap-3">
						<div className="w-9 h-9 rounded-lg bg-brand-100 dark:bg-brand-900/40 flex items-center justify-center shrink-0">
							<i className="ri-calculator-line text-brand-600 dark:text-brand-400" />
						</div>
						<div>
							<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Calculate TDEE</h2>
							<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
								{tdeeResult
									? `Estimated: ${tdeeResult} kcal/day`
									: "Estimate your daily energy expenditure"}
							</p>
						</div>
					</div>
					<i className={`ri-arrow-${tdeeOpen ? "up" : "down"}-s-line text-xl text-charcoal-blue-400`} />
				</button>

				{tdeeOpen && (
					<div className="px-6 pb-6 space-y-5 border-t border-charcoal-blue-100 dark:border-white/10 pt-5">
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
												: "bg-charcoal-blue-100 dark:bg-charcoal-blue-900/60 text-charcoal-blue-600 dark:text-charcoal-blue-400 hover:bg-charcoal-blue-200 dark:hover:bg-charcoal-blue-700"
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
														: "text-charcoal-blue-400 dark:text-charcoal-blue-500 hover:text-charcoal-blue-600 dark:hover:text-charcoal-blue-300"
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
														: "text-charcoal-blue-400 dark:text-charcoal-blue-500 hover:text-charcoal-blue-600 dark:hover:text-charcoal-blue-300"
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
							<p className="text-sm text-charcoal-blue-400 dark:text-charcoal-blue-500 text-center py-2">
								Fill in your details to calculate
							</p>
						)}
					</div>
				)}
			</div>

			<form action={action} className="space-y-6">
				{/* Hidden fields for new goal data */}
				<input type="hidden" name="targetWeight" value={targetWeightValue} />
				<input type="hidden" name="weightUnit" value={targetWeightUnit} />
				<input type="hidden" name="targetDate" value={targetDateValue} />

				{/* Goal Type Card */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2">
						<i className="ri-bookmark-line text-brand-500" />
						Goal Type
					</h2>
					<div>
						<label htmlFor="goalType" className="label">Goal type</label>
						<select
							id="goalType"
							name="goalType"
							className="input"
							value={goalType}
							onChange={(e) => setGoalType(e.target.value)}
						>
							<option value="">Select a goal type</option>
							<option value="weight_loss">Weight Loss</option>
							<option value="muscle_gain">Muscle Gain</option>
							<option value="maintenance">Maintenance</option>
							<option value="general">General Health</option>
						</select>
						<FieldError formState={formState} name="goalType" />
					</div>
				</div>

				{/* Macro Targets Card */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2">
						<i className="ri-pie-chart-2-line text-brand-500" />
						Daily Macro Targets
					</h2>

					<div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
						<div>
							<label htmlFor="calories" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-burnt-peach-500" />
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
									<span className="w-2 h-2 rounded-sm bg-verdigris-500" />
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
								value={proteinValue}
								onChange={(e) => setProteinValue(e.target.value)}
							/>
							{pCalAutoProtein !== null && !proteinValue && (
								<p className="text-xs text-indigo-500 mt-1">P/Cal suggests ~{pCalAutoProtein}g protein</p>
							)}
							<FieldError formState={formState} name="protein" />
						</div>
						<div>
							<label htmlFor="carbs" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-tuscan-sun-500" />
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
								value={carbsValue}
								onChange={(e) => setCarbsValue(e.target.value)}
							/>
							<FieldError formState={formState} name="carbs" />
						</div>
						<div>
							<label htmlFor="fat" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-sandy-brown-500" />
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
								value={fatValue}
								onChange={(e) => setFatValue(e.target.value)}
							/>
							<FieldError formState={formState} name="fat" />
						</div>
						<div>
							<label htmlFor="fiber" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-green-500" />
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
								value={fiberValue}
								onChange={(e) => setFiberValue(e.target.value)}
							/>
							<FieldError formState={formState} name="fiber" />
						</div>
					</div>
				</div>

				{/* Body Composition Goals */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2">
						<i className="ri-body-scan-line text-brand-500" />
						Body Composition Goals
					</h2>

					<div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
						<div>
							<label htmlFor="targetWeight" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-blue-500" />
									Target Weight
								</span>
							</label>
							<div className="flex gap-2">
								<input
									type="number"
									id="targetWeight"
									min={20}
									step={0.1}
									placeholder={targetWeightUnit === "kg" ? "70" : "154"}
									className="input flex-1"
									value={targetWeightValue}
									onChange={(e) => setTargetWeightValue(e.target.value)}
								/>
								<div className="flex gap-1 items-center">
									{(["kg", "lb"] as const).map((u) => (
										<button
											key={u}
											type="button"
											onClick={() => setTargetWeightUnit(u)}
											className={`text-xs px-2 py-1 rounded font-medium transition-colors ${
												targetWeightUnit === u
													? "bg-brand-500 text-white"
													: "text-charcoal-blue-400 hover:text-charcoal-blue-600"
											}`}
										>
											{u}
										</button>
									))}
								</div>
							</div>
						</div>
						<div>
							<label htmlFor="targetBodyFatPercentage" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-rose-500" />
									Target Body Fat (%)
								</span>
							</label>
							<input
								type="number"
								id="targetBodyFatPercentage"
								name="targetBodyFatPercentage"
								min={1}
								max={60}
								step={0.1}
								placeholder="e.g., 15"
								className="input"
								value={bodyFatValue}
								onChange={(e) => setBodyFatValue(e.target.value)}
							/>
							<FieldError formState={formState} name="targetBodyFatPercentage" />
						</div>
						<div>
							<label htmlFor="targetMuscleMassKg" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-violet-500" />
									Target Muscle Mass (kg)
								</span>
							</label>
							<input
								type="number"
								id="targetMuscleMassKg"
								name="targetMuscleMassKg"
								min={1}
								max={200}
								step={0.1}
								placeholder="e.g., 75"
								className="input"
								value={muscleMassValue}
								onChange={(e) => setMuscleMassValue(e.target.value)}
							/>
							<FieldError formState={formState} name="targetMuscleMassKg" />
						</div>
						<div>
							<label htmlFor="targetProteinCalorieRatio" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-indigo-500" />
									Target P/Cal Ratio (%)
								</span>
							</label>
							<input
								type="number"
								id="targetProteinCalorieRatio"
								name="targetProteinCalorieRatio"
								min={1}
								max={100}
								step={1}
								placeholder="30"
								className="input"
								value={pcalValue}
								onChange={(e) => setPcalValue(e.target.value)}
							/>
							<FieldError formState={formState} name="targetProteinCalorieRatio" />
						</div>
						<div>
							<label htmlFor="targetDate" className="label">
								<span className="flex items-center gap-1.5">
									<span className="w-2 h-2 rounded-sm bg-teal-500" />
									Target Date
								</span>
							</label>
							<input
								type="date"
								id="targetDate"
								min={new Date().toISOString().split("T")[0]}
								className="input"
								value={targetDateValue}
								onChange={(e) => setTargetDateValue(e.target.value)}
							/>
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
								<p className="text-xs text-amber-500 dark:text-amber-400 mt-3">Goal saved. Adjust targets if needed.</p>
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
