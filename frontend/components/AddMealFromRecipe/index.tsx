'use client';

import { useActionState, useEffect, useRef, useState } from "react";
import Link from "next/link";
import Loading from "@/components/Loading";
import { useRouter, useSearchParams } from "next/navigation";
import { addMeal } from "@/data/meal";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { FieldError } from "@/components/FieldError";
import { GamificationToaster } from "@/components/gamification/GamificationToaster";
import Macros from "@/types/macro";

interface AddMealFromRecipeProps {
	recipeId: string;
	name: string;
	macros: Macros;
}

const SERVING_PRESETS = [0.25, 0.5, 0.75, 1, 1.5, 2, 3];

export default function AddMealFromRecipe({ recipeId, name, macros }: AddMealFromRecipeProps) {
	const [formState, action, isPending] = useActionState(addMeal, EMPTY_FORM_STATE);
	const router = useRouter();
	const searchParams = useSearchParams();
	const warningsRef = useRef<HTMLDivElement>(null);
	const now = new Date();
	const today = now.toISOString().split("T")[0];
	const nowLocal = `${today}T${now.toTimeString().slice(0, 5)}`;
	const queryDate = searchParams.get("date") || today;
	const defaultDateTime = queryDate === today ? nowLocal : `${queryDate}T12:00`;

	const [servings, setServings] = useState(1);
	const [vals, setVals] = useState({
		calories: macros.calories.toFixed(0),
		protein: macros.protein.toFixed(1),
		carbs: macros.carbs.toFixed(1),
		fat: macros.fat.toFixed(1),
		fiber: macros.fiber.toFixed(1),
	});

	function scaleToServings(s: number) {
		setServings(s);
		setVals({
			calories: (macros.calories * s).toFixed(0),
			protein: (macros.protein * s).toFixed(1),
			carbs: (macros.carbs * s).toFixed(1),
			fat: (macros.fat * s).toFixed(1),
			fiber: (macros.fiber * s).toFixed(1),
		});
	}

	useEffect(() => {
		if (formState.status !== "success") return;
		const hasUnlocks = (formState.unlockedAchievements?.length ?? 0) > 0;
		const hasStreak = formState.streak?.extended ?? false;
		const hasWarnings = (formState.warnings?.length ?? 0) > 0;

		if (hasWarnings) {
			warningsRef.current?.scrollIntoView({ behavior: "smooth" });
			return;
		}
		const delay = hasUnlocks ? 3200 : hasStreak ? 1600 : 0;
		const t = setTimeout(() => router.push("/meals"), delay);
		return () => clearTimeout(t);
	}, [formState.status, formState.warnings, formState.streak, formState.unlockedAchievements, router]);

	return (
		<form action={action} className="space-y-6">
			<GamificationToaster
				streak={formState.streak}
				unlockedAchievements={formState.unlockedAchievements}
			/>
			<input type="hidden" name="recipeId" value={recipeId} />

			{/* Basic Info Card */}
			<div className="card p-6 space-y-5">
				<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2">
					<i className="ri-edit-line text-brand-500" />
					Log Details
				</h2>

				<div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
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
					<div>
						<label htmlFor="date" className="label">Date & Time</label>
						<input
							type="datetime-local"
							id="date"
							name="date"
							defaultValue={defaultDateTime}
							max={nowLocal}
							className="input"
						/>
						<FieldError formState={formState} name="date" />
					</div>
				</div>
			</div>

			{/* Serving Size Card */}
			<div className="card p-6 space-y-4">
				<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2">
					<i className="ri-scales-line text-brand-500" />
					Serving Size
				</h2>

				<div className="flex items-center gap-3">
					<button
						type="button"
						onClick={() => scaleToServings(Math.max(0.25, Math.round((servings - 0.25) * 4) / 4))}
						className="w-9 h-9 rounded-lg bg-charcoal-blue-100 dark:bg-charcoal-blue-900 flex items-center justify-center text-charcoal-blue-600 dark:text-charcoal-blue-300 hover:bg-charcoal-blue-200 dark:hover:bg-charcoal-blue-700 transition-colors font-bold"
					>
						−
					</button>
					<div className="flex-1">
						<input
							type="number"
							min={0.25}
							step={0.25}
							value={servings}
							onChange={(e) => {
								const v = parseFloat(e.target.value);
								if (!isNaN(v) && v > 0) scaleToServings(v);
							}}
							className="input text-center font-semibold"
						/>
					</div>
					<button
						type="button"
						onClick={() => scaleToServings(Math.round((servings + 0.25) * 4) / 4)}
						className="w-9 h-9 rounded-lg bg-charcoal-blue-100 dark:bg-charcoal-blue-900 flex items-center justify-center text-charcoal-blue-600 dark:text-charcoal-blue-300 hover:bg-charcoal-blue-200 dark:hover:bg-charcoal-blue-700 transition-colors font-bold"
					>
						+
					</button>
					<span className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400 shrink-0">servings</span>
				</div>

				{/* Quick presets */}
				<div className="flex flex-wrap gap-2">
					{SERVING_PRESETS.map((p) => (
						<button
							key={p}
							type="button"
							onClick={() => scaleToServings(p)}
							className={`px-3 py-1 rounded-lg text-sm font-medium transition-colors ${
								servings === p
									? "bg-brand-500 text-white"
									: "bg-charcoal-blue-100 dark:bg-charcoal-blue-900 text-charcoal-blue-600 dark:text-charcoal-blue-400 hover:bg-charcoal-blue-200 dark:hover:bg-charcoal-blue-700"
							}`}
						>
							{p}×
						</button>
					))}
				</div>
			</div>

			{/* Nutrition Info Card */}
			<div className="card p-6 space-y-5 text-charcoal-blue-900 dark:text-charcoal-blue-100">
				<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2">
					<i className="ri-heart-pulse-line text-brand-500" />
					Nutritional Information
					{servings !== 1 && (
						<span className="ml-auto text-xs font-normal text-brand-500 bg-brand-50 dark:bg-brand-950/40 px-2 py-0.5 rounded-full">
							scaled × {servings}
						</span>
					)}
				</h2>

				<div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
					<div>
						<label htmlFor="calories" className="label text-charcoal-blue-900 dark:text-charcoal-blue-100">Calories (kcal)</label>
						<input
							type="number"
							id="calories"
							name="calories"
							value={vals.calories}
							onChange={(e) => setVals((v) => ({ ...v, calories: e.target.value }))}
							className="input"
							min={0}
							required
						/>
						<FieldError formState={formState} name="calories" />
					</div>
					<div>
						<label htmlFor="protein" className="label text-charcoal-blue-900 dark:text-charcoal-blue-100">Protein (g)</label>
						<input
							type="number"
							id="protein"
							name="protein"
							value={vals.protein}
							onChange={(e) => setVals((v) => ({ ...v, protein: e.target.value }))}
							className="input"
							min={0}
							step="0.1"
							required
						/>
						<FieldError formState={formState} name="protein" />
					</div>
					<div>
						<label htmlFor="carbs" className="label text-charcoal-blue-900 dark:text-charcoal-blue-100">Carbs (g)</label>
						<input
							type="number"
							id="carbs"
							name="carbs"
							value={vals.carbs}
							onChange={(e) => setVals((v) => ({ ...v, carbs: e.target.value }))}
							className="input"
							min={0}
							step="0.1"
							required
						/>
						<FieldError formState={formState} name="carbs" />
					</div>
					<div>
						<label htmlFor="fat" className="label text-charcoal-blue-900 dark:text-charcoal-blue-100">Fat (g)</label>
						<input
							type="number"
							id="fat"
							name="fat"
							value={vals.fat}
							onChange={(e) => setVals((v) => ({ ...v, fat: e.target.value }))}
							className="input"
							min={0}
							step="0.1"
							required
						/>
						<FieldError formState={formState} name="fat" />
					</div>
					<div>
						<label htmlFor="fiber" className="label text-charcoal-blue-900 dark:text-charcoal-blue-100">Fiber (g)</label>
						<input
							type="number"
							id="fiber"
							name="fiber"
							value={vals.fiber}
							onChange={(e) => setVals((v) => ({ ...v, fiber: e.target.value }))}
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
					<div ref={warningsRef} className="p-4 rounded-xl bg-amber-50 dark:bg-amber-950 border border-amber-200 dark:border-amber-800">
						<div className="flex items-start gap-3">
							<i className="ri-error-warning-line text-xl text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
							<div>
								<p className="font-semibold text-amber-800 dark:text-amber-300">Nutrition hint{formState.warnings.length > 1 ? "s" : ""}</p>
								<ul className="mt-2 space-y-1.5">
									{formState.warnings.map((w, i) => (
										<li key={i} className="text-sm text-amber-700 dark:text-amber-300">{w}</li>
									))}
								</ul>
								<p className="text-xs text-amber-500 dark:text-amber-400 mt-3">Your entry was saved. You can adjust it or continue to the diary.</p>
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
