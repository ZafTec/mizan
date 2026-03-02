"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import {
	updateMealPlan,
	removeRecipeFromMealPlan,
	updateMealPlanRecipe,
	addRecipeToMealPlan,
	type MealPlan,
	type MealPlanRecipe,
} from "@/data/mealPlan";
import { DeleteConfirmModal } from "@/components/DeleteConfirmModal";
import RecipeSearchModal from "@/app/(dashboard)/meal-plan/create/RecipeSearchModal";
import { toast } from "sonner";
import Loading from "@/components/Loading";

interface MealPlanEditFormProps {
	plan: MealPlan;
}

const MEAL_TYPES = ["breakfast", "lunch", "dinner", "snack"];

export default function MealPlanEditForm({ plan }: MealPlanEditFormProps) {
	const router = useRouter();
	const [name, setName] = useState(plan.name || "");
	const [startDate, setStartDate] = useState(plan.startDate);
	const [endDate, setEndDate] = useState(plan.endDate);
	const [recipes, setRecipes] = useState<MealPlanRecipe[]>(plan.recipes || []);
	const [saving, setSaving] = useState(false);
	const [showRecipeSearch, setShowRecipeSearch] = useState(false);
	const [deleteTarget, setDeleteTarget] = useState<MealPlanRecipe | null>(null);
	const [editingRecipe, setEditingRecipe] = useState<string | null>(null);

	const handleSaveMetadata = async () => {
		setSaving(true);
		const result = await updateMealPlan(plan.id, { name, startDate, endDate });
		setSaving(false);
		if (result.success) {
			toast.success("Meal plan updated");
			router.refresh();
		} else {
			toast.error(result.message || "Failed to update meal plan");
		}
	};

	const handleRemoveRecipe = async () => {
		if (!deleteTarget) return;
		const result = await removeRecipeFromMealPlan(plan.id, deleteTarget.id);
		if (result.success) {
			setRecipes((prev) => prev.filter((r) => r.id !== deleteTarget.id));
			toast.success("Recipe removed");
		} else {
			toast.error(result.message || "Failed to remove recipe");
		}
		setDeleteTarget(null);
	};

	const handleUpdateRecipe = async (recipe: MealPlanRecipe) => {
		const result = await updateMealPlanRecipe(plan.id, recipe.id, {
			date: recipe.date,
			mealType: recipe.mealType,
			servings: recipe.servings,
		});
		if (result.success) {
			toast.success("Recipe updated");
			setEditingRecipe(null);
		} else {
			toast.error(result.message || "Failed to update recipe");
		}
	};

	const handleRecipeFieldChange = (recipeId: string, field: keyof MealPlanRecipe, value: string | number) => {
		setRecipes((prev) =>
			prev.map((r) => (r.id === recipeId ? { ...r, [field]: value } : r))
		);
	};

	const handleAddRecipe = async (recipe: { id: string; title: string }, servings: number) => {
		setShowRecipeSearch(false);
		const success = await addRecipeToMealPlan(plan.id, {
			recipeId: recipe.id,
			date: startDate,
			mealType: "dinner",
			servings,
		});
		if (success) {
			toast.success("Recipe added");
			router.refresh();
		} else {
			toast.error("Failed to add recipe");
		}
	};

	return (
		<>
			<div className="card p-6">
				<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2 mb-4">
					<i className="ri-settings-3-line text-brand-500 dark:text-brand-400" />
					Plan Details
				</h2>
				<div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
					<div>
						<label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Name</label>
						<input
							type="text"
							value={name}
							onChange={(e) => setName(e.target.value)}
							className="input w-full"
							maxLength={255}
						/>
					</div>
					<div>
						<label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Start Date</label>
						<input
							type="date"
							value={startDate}
							onChange={(e) => setStartDate(e.target.value)}
							className="input w-full"
						/>
					</div>
					<div>
						<label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">End Date</label>
						<input
							type="date"
							value={endDate}
							onChange={(e) => setEndDate(e.target.value)}
							className="input w-full"
						/>
					</div>
				</div>
				<div className="mt-4 flex justify-end">
					<button onClick={handleSaveMetadata} disabled={saving} className="btn-primary">
						{saving && <Loading size="sm" />}
						Save Details
					</button>
				</div>
			</div>

			<div className="card p-6">
				<div className="flex items-center justify-between mb-4">
					<h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
						<i className="ri-restaurant-line text-brand-500 dark:text-brand-400" />
						Recipes ({recipes.length})
					</h2>
					<button onClick={() => setShowRecipeSearch(true)} className="btn-primary text-sm">
						<i className="ri-add-line" />
						Add Recipe
					</button>
				</div>

				{recipes.length > 0 ? (
					<div className="space-y-3">
						{recipes.map((recipe) => {
							const isEditing = editingRecipe === recipe.id;
							return (
								<div key={recipe.id} className="p-4 bg-slate-50 dark:bg-slate-800 rounded-xl">
									<div className="flex items-start justify-between gap-4">
										<div className="flex-1 min-w-0">
											<p className="text-sm font-medium text-slate-900 dark:text-slate-100">
												{recipe.recipeTitle || "Recipe"}
											</p>
											{!isEditing && (
												<p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
													{recipe.date} &middot; {recipe.mealType} &middot; {recipe.servings} serving{recipe.servings !== 1 ? "s" : ""}
													{recipe.caloriesPerServing ? ` · ${Math.round(recipe.caloriesPerServing * recipe.servings)} kcal` : ""}
												</p>
											)}
										</div>
										<div className="flex items-center gap-1">
											{isEditing ? (
												<>
													<button
														onClick={() => handleUpdateRecipe(recipe)}
														className="p-2 text-green-600 hover:text-green-700 transition-colors"
														title="Save"
													>
														<i className="ri-check-line" />
													</button>
													<button
														onClick={() => setEditingRecipe(null)}
														className="p-2 text-slate-400 hover:text-slate-600 transition-colors"
														title="Cancel"
													>
														<i className="ri-close-line" />
													</button>
												</>
											) : (
												<button
													onClick={() => setEditingRecipe(recipe.id)}
													className="p-2 text-slate-400 hover:text-brand-600 dark:hover:text-brand-400 transition-colors"
													title="Edit"
												>
													<i className="ri-edit-line" />
												</button>
											)}
											<button
												onClick={() => setDeleteTarget(recipe)}
												className="p-2 text-slate-400 hover:text-red-600 dark:hover:text-red-400 transition-colors"
												title="Remove"
											>
												<i className="ri-delete-bin-line" />
											</button>
										</div>
									</div>
									{isEditing && (
										<div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mt-3 pt-3 border-t border-slate-200 dark:border-slate-700">
											<div>
												<label className="block text-xs font-medium text-slate-500 mb-1">Date</label>
												<input
													type="date"
													value={recipe.date}
													onChange={(e) => handleRecipeFieldChange(recipe.id, "date", e.target.value)}
													className="input w-full text-sm"
												/>
											</div>
											<div>
												<label className="block text-xs font-medium text-slate-500 mb-1">Meal Type</label>
												<select
													value={recipe.mealType}
													onChange={(e) => handleRecipeFieldChange(recipe.id, "mealType", e.target.value)}
													className="input w-full text-sm"
												>
													{MEAL_TYPES.map((mt) => (
														<option key={mt} value={mt}>{mt.charAt(0).toUpperCase() + mt.slice(1)}</option>
													))}
												</select>
											</div>
											<div>
												<label className="block text-xs font-medium text-slate-500 mb-1">Servings</label>
												<input
													type="number"
													min={0.25}
													step={0.25}
													value={recipe.servings}
													onChange={(e) => handleRecipeFieldChange(recipe.id, "servings", parseFloat(e.target.value) || 1)}
													className="input w-full text-sm"
												/>
											</div>
										</div>
									)}
								</div>
							);
						})}
					</div>
				) : (
					<div className="text-center py-8 text-slate-500">
						<p>No recipes in this plan yet.</p>
					</div>
				)}
			</div>

			{showRecipeSearch && (
				<RecipeSearchModal
					onSelect={handleAddRecipe}
					onClose={() => setShowRecipeSearch(false)}
				/>
			)}

			<DeleteConfirmModal
				isOpen={!!deleteTarget}
				onClose={() => setDeleteTarget(null)}
				onConfirm={handleRemoveRecipe}
				itemName="Recipe"
			/>
		</>
	);
}
