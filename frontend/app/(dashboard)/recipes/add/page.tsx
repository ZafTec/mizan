'use client';

import { useEffect, useRef, useState } from "react";
import { getAllIngredient } from "@/data/ingredient";
import type { Ingredient } from "@/data/ingredient";

import { CldUploadWidget } from 'next-cloudinary';

const hasCloudinary = !!process.env.NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME;
import Image from 'next/image';
import Link from 'next/link';
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import Modal from "@/components/Modal";
import { appToast } from "@/lib/toast";
import Loading from "@/components/Loading";
import { useDebounce } from "@/lib/hooks/useDebounce";
import RecipeIngredientSearch from "@/components/Recipes/RecipeIngredientSearch";
import type { RecipeSearchResult } from "@/components/Recipes/RecipeIngredientSearch";

type SelectedIngredient = {
	type: "food" | "recipe";
	ingredient: Ingredient | null;
	subRecipe: RecipeSearchResult | null;
	name: string;
	amount: number | null;
	unit: string;
}

const MAX_RECIPE_IMAGES_TO_PREVIEW = 3;

export default function Page() {
	const [name, setName] = useState('');
	const [images, setImages] = useState<string[]>([]);
	const [description, setDescription] = useState('');
	const [instructions, setInstructions] = useState('');
	const [selectedIngredients, setSelectedIngredients] = useState<SelectedIngredient[]>([]);
	const [servings, setServings] = useState(1);
	const [prepTime, setPrepTime] = useState<number | null>(null);
	const [cookTime, setCookTime] = useState<number | null>(null);
	const [sourceUrl, setSourceUrl] = useState('');
	const [isSubmitting, setIsSubmitting] = useState(false);
	const [visibility, setVisibility] = useState<'public' | 'private' | 'household'>('private');

	const [tags, setTags] = useState<Set<string>>(new Set<string>());
	const [currentTag, setCurrentTag] = useState('');

	const [ingredientSearch, setIngredientSearch] = useState<Ingredient[]>([]);
	const [activeDropdownIndex, setActiveDropdownIndex] = useState<number | null>(null);
	const [ingredientQuery, setIngredientQuery] = useState("");
	const debouncedQuery = useDebounce(ingredientQuery, 300);

	const dropdownRef = useRef<HTMLDivElement>(null);
	const router = useRouter();
	const [isModalOpen, setIsModalOpen] = useState(false);

	const totalMacros = selectedIngredients.reduce(
		(acc, ing) => {
			if (!ing.amount) return acc;
			if (ing.type === "recipe" && ing.subRecipe?.nutrition) {
				const s = ing.amount;
				const n = ing.subRecipe.nutrition;
				acc.calories += (n.caloriesPerServing || 0) * s;
				acc.protein += (n.proteinGrams || 0) * s;
				acc.carbs += (n.carbsGrams || 0) * s;
				acc.fat += (n.fatGrams || 0) * s;
				acc.fiber += (n.fiberGrams || 0) * s;
			} else if (ing.type === "food" && ing.ingredient) {
				const ratio = ing.amount / 100;
				acc.calories += (ing.ingredient.caloriesPer100g || 0) * ratio;
				acc.protein += (ing.ingredient.proteinPer100g || 0) * ratio;
				acc.carbs += (ing.ingredient.carbsPer100g || 0) * ratio;
				acc.fat += (ing.ingredient.fatPer100g || 0) * ratio;
				acc.fiber += (ing.ingredient.fiberPer100g || 0) * ratio;
			}
			return acc;
		},
		{ calories: 0, protein: 0, carbs: 0, fat: 0, fiber: 0 }
	);

	useEffect(() => {
		function handleClickOutside(event: MouseEvent) {
			if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
				setActiveDropdownIndex(null);
			}
		}

		document.addEventListener('mousedown', handleClickOutside);
		return () => {
			document.removeEventListener('mousedown', handleClickOutside);
		}
	}, []);

	function handleAddIngredient() {
		setSelectedIngredients([...selectedIngredients, { type: "food", ingredient: null, subRecipe: null, name: '', amount: null, unit: 'gram' }]);
	}

	function handleIngredientTypeToggle(index: number, type: "food" | "recipe") {
		const newIngredients = [...selectedIngredients];
		newIngredients[index] = { type, ingredient: null, subRecipe: null, name: '', amount: null, unit: type === "food" ? "gram" : "serving" };
		setSelectedIngredients(newIngredients);
	}

	function handleSubRecipeSelect(index: number, recipe: RecipeSearchResult) {
		const newIngredients = [...selectedIngredients];
		newIngredients[index] = { ...newIngredients[index], subRecipe: recipe, name: recipe.title, amount: 1 };
		setSelectedIngredients(newIngredients);
		setActiveDropdownIndex(null);
	}

	function handleRemoveIngredient(index: number) {
		const newIngredients = selectedIngredients.filter((_, i) => i !== index);
		setSelectedIngredients(newIngredients);
	}

	function handleIngredientNameChange(index: number, value: string) {
		const newIngredients = [...selectedIngredients];
		newIngredients[index] = { ...newIngredients[index], name: value };
		setSelectedIngredients(newIngredients);
		setActiveDropdownIndex(index);
		setIngredientQuery(value);

		if (!value) {
			setIngredientSearch([]);
		}
	}

	useEffect(() => {
		if (!debouncedQuery) return;
		let cancelled = false;
		getAllIngredient(debouncedQuery, undefined, undefined, 1, 4).then((result) => {
			if (!cancelled) setIngredientSearch(result.ingredients);
		});
		return () => { cancelled = true; };
	}, [debouncedQuery]);

	function handleAddTag(e: React.KeyboardEvent<HTMLInputElement>) {
		if (e.key === 'Enter' && currentTag.trim()) {
			e.preventDefault();
			setTags(new Set([...tags, currentTag.trim().toUpperCase()]));
			setCurrentTag('');
		}
	}

	function handleRemoveTag(tagToRemove: string) {
		const newTags = new Set(tags);
		newTags.delete(tagToRemove);
		setTags(newTags);
	}

	function handleIngredientSelect(ingredientIndex: number, selectedIngredient: Ingredient) {
		const newIngredients = [...selectedIngredients];
		newIngredients[ingredientIndex] = {
			...newIngredients[ingredientIndex],
			ingredient: selectedIngredient,
			name: selectedIngredient.name,
			amount: null,
		}
		setSelectedIngredients(newIngredients);
		setActiveDropdownIndex(null);
	}

	function handleIngredientAmountChange(index: number, value: number) {
		const newIngredients = [...selectedIngredients];
		newIngredients[index] = { ...newIngredients[index], amount: value };
		setSelectedIngredients(newIngredients);
	}

	const [isLoggingMeal, setIsLoggingMeal] = useState(false);

	const handleLogMealDirectly = async (e: React.MouseEvent) => {
		e.preventDefault();
		if (!name) {
			appToast.error('Please enter a recipe name before logging');
			return;
		}

		if (selectedIngredients.length === 0 || totalMacros.calories === 0) {
			appToast.error('Please add ingredients before logging');
			return;
		}

		setIsLoggingMeal(true);
		
		// Divide by servings to get 1 serving of the recipe
		const ratio = 1 / Math.max(1, servings);
		
		const mealData = {
			name: `${name} (1 serving)`,
			mealType: "MEAL",
			entryDate: new Date().toISOString().split("T")[0],
			servings: 1,
			calories: Math.round(totalMacros.calories * ratio),
			proteinGrams: Number((totalMacros.protein * ratio).toFixed(1)),
			carbsGrams: Number((totalMacros.carbs * ratio).toFixed(1)),
			fatGrams: Number((totalMacros.fat * ratio).toFixed(1)),
			fiberGrams: Number((totalMacros.fiber * ratio).toFixed(1))
		};

		try {
			await clientApi('/api/Meals', {
				method: 'POST',
				body: mealData,
			});
			appToast.success('Meal logged successfully!');
			router.push('/meals');
		} catch (err) {
			console.error('[Recipe Create] Failed to log meal:', err);
			appToast.error(err, 'Failed to log meal. Please try again.');
		} finally {
			setIsLoggingMeal(false);
		}
	}

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		setIsSubmitting(true);

		const recipeData = {
			title: name,
			description,
			ingredients: selectedIngredients.map(ing => ({
				foodId: ing.type === "food" ? ing.ingredient?.id : undefined,
				subRecipeId: ing.type === "recipe" ? ing.subRecipe?.id : undefined,
				ingredientText: ing.name,
				amount: ing.amount!,
				unit: ing.type === "food" ? "gram" : "serving"
			})),
			instructions: instructions.split('\n').filter(line => line.trim()),
			servings,
			tags: Array.from(tags),
			prepTimeMinutes: prepTime || undefined,
			cookTimeMinutes: cookTime || undefined,
			sourceUrl: sourceUrl || undefined,
			imageUrl: images[0] || undefined,
			isPublic: visibility === 'public',
			householdId: visibility === 'household' ? null : undefined
		}


		try {
			await clientApi('/api/Recipes', {
				method: 'POST',
				body: recipeData,
			});
			appToast.success('Recipe created');
			router.push('/recipes');
		} catch (err) {
			console.error('[Recipe Create] Failed:', err);
			appToast.error(err, 'Failed to create recipe. Please try again.');
		} finally {
			setIsSubmitting(false);
		}
	}

	return (
		<div className="max-w-4xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex items-center justify-between">
				<div className="flex items-center gap-4">
					<Link href="/recipes" className="flex h-10 w-10 items-center justify-center rounded-xl bg-charcoal-blue-100 transition-colors hover:bg-charcoal-blue-200 dark:bg-charcoal-blue-900/75 dark:hover:bg-charcoal-blue-900">
						<i className="ri-arrow-left-line text-xl text-charcoal-blue-600 dark:text-charcoal-blue-300" />
					</Link>
					<div>
						<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">Create Recipe</h1>
						<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400">Add a new recipe to your collection</p>
					</div>
				</div>
				<button
					type="button"
					onClick={() => setIsModalOpen(true)}
					className="btn-secondary"
				>
					<i className="ri-pie-chart-2-line" />
					View Macros
				</button>
			</div>

			{/* Macros Modal */}
			<Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)}>
				<h2 className="mb-4 text-xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">Recipe Macros Overview</h2>
				<div className="overflow-x-auto">
					<table className="w-full text-sm">
						<thead>
							<tr className="bg-charcoal-blue-50 dark:bg-charcoal-blue-900/60">
								<th className="rounded-tl-xl p-3 text-left font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">Ingredient</th>
								<th className="p-3 text-right font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">Amount</th>
								<th className="p-3 text-right font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">Calories</th>
								<th className="p-3 text-right font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">Protein</th>
								<th className="p-3 text-right font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">Carbs</th>
								<th className="p-3 text-right font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">Fat</th>
								<th className="rounded-tr-xl p-3 text-right font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">Fiber</th>
							</tr>
						</thead>
						<tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
							{selectedIngredients.map((ing, idx) => {
								if (!ing.amount) return null;
								if (ing.type === "recipe" && ing.subRecipe?.nutrition) {
									const s = ing.amount;
									const n = ing.subRecipe.nutrition;
									return (
										<tr key={idx} className="hover:bg-charcoal-blue-50 dark:hover:bg-charcoal-blue-900/60">
											<td className="p-3 text-charcoal-blue-900 dark:text-charcoal-blue-100">{ing.subRecipe.title} <span className="text-xs text-brand-500 dark:text-brand-300">(recipe)</span></td>
											<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{ing.amount} srv</td>
											<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((n.caloriesPerServing || 0) * s).toFixed(0)}</td>
											<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((n.proteinGrams || 0) * s).toFixed(1)}g</td>
											<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((n.carbsGrams || 0) * s).toFixed(1)}g</td>
											<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((n.fatGrams || 0) * s).toFixed(1)}g</td>
											<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((n.fiberGrams || 0) * s).toFixed(1)}g</td>
										</tr>
									);
								}
								if (!ing.ingredient) return null;
								const ratio = ing.amount / 100;
								return (
									<tr key={idx} className="hover:bg-charcoal-blue-50 dark:hover:bg-charcoal-blue-900/60">
										<td className="p-3 text-charcoal-blue-900 dark:text-charcoal-blue-100">{ing.ingredient.name}</td>
										<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{ing.amount}g</td>
										<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((ing.ingredient.caloriesPer100g || 0) * ratio).toFixed(0)}</td>
										<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((ing.ingredient.proteinPer100g || 0) * ratio).toFixed(1)}g</td>
										<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((ing.ingredient.carbsPer100g || 0) * ratio).toFixed(1)}g</td>
										<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((ing.ingredient.fatPer100g || 0) * ratio).toFixed(1)}g</td>
										<td className="p-3 text-right text-charcoal-blue-600 dark:text-charcoal-blue-300">{((ing.ingredient.fiberPer100g || 0) * ratio).toFixed(1)}g</td>
									</tr>
								);
							})}
						</tbody>
						<tfoot>
							<tr className="bg-brand-50 font-semibold dark:bg-brand-950/50">
								<td className="rounded-bl-xl p-3 text-brand-900 dark:text-brand-100">Total</td>
								<td className="p-3 text-right text-brand-600 dark:text-brand-300"></td>
								<td className="p-3 text-right text-brand-600 dark:text-brand-300">{totalMacros.calories.toFixed(0)}</td>
								<td className="p-3 text-right text-brand-600 dark:text-brand-300">{totalMacros.protein.toFixed(1)}g</td>
								<td className="p-3 text-right text-brand-600 dark:text-brand-300">{totalMacros.carbs.toFixed(1)}g</td>
								<td className="p-3 text-right text-brand-600 dark:text-brand-300">{totalMacros.fat.toFixed(1)}g</td>
								<td className="rounded-br-xl p-3 text-right text-brand-600 dark:text-brand-300">{totalMacros.fiber.toFixed(1)}g</td>
							</tr>
						</tfoot>
					</table>
				</div>
			</Modal>

			<form onSubmit={handleSubmit} className="space-y-6">
				{/* Basic Information Card */}
				<div className="card p-6 space-y-5">
					<h2 className="flex items-center gap-2 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						<i className="ri-image-line text-brand-500" />
						Basic Information
					</h2>

					{hasCloudinary ? (
					<div>
						<label className="label">Recipe Images</label>
						<CldUploadWidget
							onSuccess={(result) => {
								if (result?.info && result.info instanceof Object) {
									setImages((prevImages) => {
										if (result?.info && result.info instanceof Object) {
											return [...prevImages, result.info.secure_url]
										}
										return prevImages;
									});
								}
							}}
							signatureEndpoint="/api/sign-cloudinary-params"
						>
							{({ open }) => (
								<div className="flex flex-wrap gap-3">
									<button
										type="button"
										onClick={(e) => {
											e.preventDefault();
											open();
										}}
									className="group flex h-24 w-24 flex-col items-center justify-center rounded-2xl border-2 border-dashed border-charcoal-blue-300 bg-charcoal-blue-50 transition-colors hover:border-brand-400 hover:bg-brand-50 dark:border-white/10 dark:bg-charcoal-blue-900/75 dark:hover:border-brand-400 dark:hover:bg-brand-950/50"
								>
										<i className="ri-image-add-line text-2xl text-charcoal-blue-400 group-hover:text-brand-500 dark:text-charcoal-blue-500 dark:group-hover:text-brand-300" />
										<span className="mt-1 text-xs text-charcoal-blue-400 group-hover:text-brand-500 dark:text-charcoal-blue-500 dark:group-hover:text-brand-300">Add</span>
									</button>
									{images.map((image, index) => {
										if (typeof image !== 'string' || index >= MAX_RECIPE_IMAGES_TO_PREVIEW) return null;
										return (
											<div key={index} className="relative h-24 w-24">
												<Image src={image} alt="Recipe" fill sizes="96px" className="rounded-2xl border-2 border-charcoal-blue-200 object-cover dark:border-white/10" />
												<button
													type="button"
													onClick={() => setImages(images.filter((_, i) => i !== index))}
													className="absolute -top-2 -right-2 w-6 h-6 bg-red-500 text-white rounded-full flex items-center justify-center hover:bg-red-600 transition-colors"
												>
													<i className="ri-close-line text-sm" />
												</button>
											</div>
										);
									})}
									{images.length > MAX_RECIPE_IMAGES_TO_PREVIEW && (
										<div className="flex h-24 w-24 items-center justify-center rounded-2xl bg-charcoal-blue-100 dark:bg-charcoal-blue-900/60">
											<span className="font-medium text-charcoal-blue-500 dark:text-charcoal-blue-400">+{images.length - MAX_RECIPE_IMAGES_TO_PREVIEW}</span>
										</div>
									)}
								</div>
							)}
						</CldUploadWidget>
					</div>

					) : null}
					{/* Recipe Name */}
					<div>
						<label htmlFor="recipe_name" className="label">Recipe Name</label>
						<input
							id="recipe_name"
							type="text"
							value={name}
							onChange={(e) => setName(e.target.value)}
							className="input"
							placeholder="e.g., Grilled Chicken Salad"
							required
						/>
					</div>

					{/* Description */}
					<div>
						<label htmlFor="description" className="label">Description</label>
						<textarea
							id="description"
							value={description}
							onChange={(e) => setDescription(e.target.value)}
							className="input min-h-25 resize-none"
							placeholder="Describe your recipe..."
							rows={3}
							required
						/>
					</div>
				</div>

				{/* Ingredients Card */}
				<div className="card p-6 space-y-4 relative overflow-visible! z-20">
					<h2 className="flex items-center gap-2 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						<i className="ri-list-check-2 text-brand-500" />
						Ingredients
					</h2>

					<div className="space-y-3">
						{selectedIngredients.map((ing, index) => (
							<div key={index} className="space-y-2">
								<div className="flex gap-1">
									<button type="button" onClick={() => handleIngredientTypeToggle(index, "food")}
										className={`rounded-lg px-3 py-1 text-xs font-medium transition-colors ${ing.type === "food" ? "bg-brand-500 text-white" : "bg-charcoal-blue-100 text-charcoal-blue-600 hover:bg-charcoal-blue-200 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300 dark:hover:bg-charcoal-blue-900"}`}>
										Food
									</button>
									<button type="button" onClick={() => handleIngredientTypeToggle(index, "recipe")}
										className={`rounded-lg px-3 py-1 text-xs font-medium transition-colors ${ing.type === "recipe" ? "bg-brand-500 text-white" : "bg-charcoal-blue-100 text-charcoal-blue-600 hover:bg-charcoal-blue-200 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300 dark:hover:bg-charcoal-blue-900"}`}>
										Recipe
									</button>
								</div>
								<div className="flex flex-col sm:flex-row gap-2 sm:gap-3 items-stretch sm:items-start">
									{ing.type === "food" ? (
										<div ref={dropdownRef} className="relative flex-1 min-w-0">
											<input
												type="text"
												placeholder="Search ingredient..."
												value={ing.name}
												onChange={(e) => handleIngredientNameChange(index, e.target.value)}
												className="input w-full"
											/>
											{activeDropdownIndex === index && (
												<div className="absolute z-50 mt-1 max-h-60 w-full overflow-y-auto rounded-xl border border-charcoal-blue-200 bg-white shadow-lg dark:border-white/10 dark:bg-charcoal-blue-950">
													{ingredientSearch.length > 0 ? (
														ingredientSearch.map((ingredient) => (
															<button
																key={ingredient.id}
																type="button"
																onClick={() => handleIngredientSelect(index, ingredient)}
																className="flex w-full items-center justify-between border-b border-charcoal-blue-100 p-3 text-left hover:bg-charcoal-blue-50 last:border-0 dark:border-white/10 dark:hover:bg-charcoal-blue-900/80"
															>
																<span className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{ingredient.name}</span>
																<span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{ingredient.caloriesPer100g} kcal/100g</span>
															</button>
														))
													) : (
														<div className="p-3 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">No ingredients found</div>
													)}
												</div>
											)}
										</div>
									) : (
										<div className="flex-1 min-w-0">
											<RecipeIngredientSearch
												value={ing.name}
												onChange={(v) => { const n = [...selectedIngredients]; n[index] = { ...n[index], name: v }; setSelectedIngredients(n); }}
												onSelect={(r) => handleSubRecipeSelect(index, r)}
											/>
										</div>
									)}
									<div className="flex gap-2 sm:gap-3 items-center">
										<input
											type="number"
											placeholder="Amount"
											value={ing.amount?.toString() || ''}
											onChange={(e) => handleIngredientAmountChange(index, parseFloat(e.target.value))}
											className="input w-24 sm:w-28"
											min="0"
											step={ing.type === "recipe" ? "0.25" : "0.1"}
										/>
										<span className="whitespace-nowrap rounded-xl bg-charcoal-blue-100 px-3 py-2.5 text-sm font-medium text-charcoal-blue-600 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300">
											{ing.type === "food" ? "grams" : "servings"}
										</span>
										<button
											type="button"
											onClick={() => handleRemoveIngredient(index)}
											className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-red-50 text-red-500 transition-colors hover:bg-red-100 dark:bg-red-500/10 dark:text-red-300 dark:hover:bg-red-500/15"
											aria-label="Remove ingredient"
										>
											<i className="ri-delete-bin-line" />
										</button>
									</div>
								</div>
							</div>
						))}
					</div>

					<button
						type="button"
						onClick={handleAddIngredient}
						className="flex items-center gap-2 text-brand-600 hover:text-brand-700 font-medium transition-colors"
					>
						<i className="ri-add-circle-line text-lg" />
						Add Ingredient
					</button>
				</div>

				{/* Instructions Card */}
				<div className="card p-6 space-y-4">
					<h2 className="flex items-center gap-2 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						<i className="ri-file-list-3-line text-brand-500" />
						Instructions
					</h2>
					<textarea
						id="instructions"
						value={instructions}
						onChange={(e) => setInstructions(e.target.value)}
						className="input min-h-45 resize-none"
						rows={6}
						placeholder="Enter each instruction on a new line...&#10;1. Preheat oven to 375°F&#10;2. Season the chicken&#10;3. Grill for 6-8 minutes per side"
					/>
				</div>

				{/* Additional Details Card */}
				<div className="card p-6 space-y-5">
					<h2 className="flex items-center gap-2 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						<i className="ri-settings-3-line text-brand-500" />
						Additional Details
					</h2>

					{/* Visibility */}
					<div>
						<label htmlFor="visibility" className="label">Visibility</label>
						<select
							id="visibility"
							value={visibility}
							onChange={(e) => setVisibility(e.target.value as 'public' | 'private' | 'household')}
							className="input"
						>
							<option value="private">Private (Only me)</option>
							<option value="household">Household (Shared with household members)</option>
							<option value="public">Public (Visible to everyone)</option>
						</select>
						<p className="mt-1.5 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							{visibility === 'private' && 'Only you can view and edit this recipe'}
							{visibility === 'household' && 'All members of your household can view this recipe'}
							{visibility === 'public' && 'Anyone can view this recipe'}
						</p>
					</div>

					{/* Servings + Times */}
					<div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
						<div>
							<label htmlFor="servings" className="label">Servings</label>
							<div className="flex items-center gap-3">
								<button
									type="button"
									onClick={() => setServings(Math.max(1, servings - 1))}
									className="flex h-10 w-10 items-center justify-center rounded-xl bg-charcoal-blue-100 transition-colors hover:bg-charcoal-blue-200 dark:bg-charcoal-blue-900/60 dark:hover:bg-charcoal-blue-900"
								>
									<i className="ri-subtract-line text-charcoal-blue-600 dark:text-charcoal-blue-300" />
								</button>
								<input
									id="servings"
									type="number"
									value={servings}
									onChange={(e) => setServings(parseInt(e.target.value) || 1)}
									className="input w-20 text-center"
									min="1"
								/>
								<button
									type="button"
									onClick={() => setServings(servings + 1)}
									className="flex h-10 w-10 items-center justify-center rounded-xl bg-charcoal-blue-100 transition-colors hover:bg-charcoal-blue-200 dark:bg-charcoal-blue-900/60 dark:hover:bg-charcoal-blue-900"
								>
									<i className="ri-add-line text-charcoal-blue-600 dark:text-charcoal-blue-300" />
								</button>
							</div>
						</div>
						<div>
							<label htmlFor="prepTime" className="label">Prep Time (min)</label>
							<input
								id="prepTime"
								type="number"
								value={prepTime ?? ''}
								onChange={(e) => setPrepTime(e.target.value ? parseInt(e.target.value) : null)}
								className="input"
								min="0"
								step="1"
								placeholder="15"
							/>
						</div>
						<div>
							<label htmlFor="cookTime" className="label">Cook Time (min)</label>
							<input
								id="cookTime"
								type="number"
								value={cookTime ?? ''}
								onChange={(e) => setCookTime(e.target.value ? parseInt(e.target.value) : null)}
								className="input"
								min="0"
								step="1"
								placeholder="30"
							/>
						</div>
					</div>

					{/* Source URL */}
					<div>
						<label htmlFor="sourceUrl" className="label">Source URL (optional)</label>
						<input
							id="sourceUrl"
							type="url"
							value={sourceUrl}
							onChange={(e) => setSourceUrl(e.target.value)}
							className="input"
							placeholder="https://..."
						/>
					</div>

					{/* Tags */}
					<div>
						<label htmlFor="tag" className="label">Tags</label>
						{tags.size > 0 && (
							<div className="flex flex-wrap gap-2 mb-3">
								{Array.from(tags).map((tag, index) => (
									<span key={index} className="inline-flex items-center gap-1.5 rounded-full bg-brand-100 px-3 py-1.5 text-sm font-medium text-brand-700 dark:bg-brand-950/60 dark:text-brand-200">
										{tag}
										<button type="button" onClick={() => handleRemoveTag(tag)} className="hover:text-brand-900 dark:hover:text-brand-50">
											<i className="ri-close-line" />
										</button>
									</span>
								))}
							</div>
						)}
						<input
							id="tag"
							type="text"
							value={currentTag}
							onChange={(e) => setCurrentTag(e.target.value)}
							onKeyDown={handleAddTag}
							className="input"
							placeholder="Type a tag and press Enter (e.g., HIGH-PROTEIN, QUICK)"
						/>
					</div>
				</div>

				{/* Macro Summary */}
				{selectedIngredients.some(ing => (ing.ingredient || ing.subRecipe) && ing.amount) && (
					<div className="card p-6 bg-white/70 dark:bg-charcoal-blue-950/60">
						<h2 className="mb-4 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Nutrition Summary</h2>
						<div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
							<div className="rounded-xl bg-white p-3 text-center dark:bg-charcoal-blue-950/80">
								<p className="text-2xl font-bold text-orange-600">{totalMacros.calories.toFixed(0)}</p>
								<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Calories</p>
							</div>
							<div className="rounded-xl bg-white p-3 text-center dark:bg-charcoal-blue-950/80">
								<p className="text-2xl font-bold text-red-600">{totalMacros.protein.toFixed(1)}g</p>
								<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Protein</p>
							</div>
							<div className="rounded-xl bg-white p-3 text-center dark:bg-charcoal-blue-950/80">
								<p className="text-2xl font-bold text-amber-600">{totalMacros.carbs.toFixed(1)}g</p>
								<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Carbs</p>
							</div>
							<div className="rounded-xl bg-white p-3 text-center dark:bg-charcoal-blue-950/80">
								<p className="text-2xl font-bold text-yellow-600">{totalMacros.fat.toFixed(1)}g</p>
								<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Fat</p>
							</div>
							<div className="rounded-xl bg-white p-3 text-center dark:bg-charcoal-blue-950/80">
								<p className="text-2xl font-bold text-green-600">{totalMacros.fiber.toFixed(1)}g</p>
								<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">Fiber</p>
							</div>
						</div>
					</div>
				)}

				{/* Action Buttons */}
				<div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
					<button
						type="button"
						onClick={handleLogMealDirectly}
						disabled={isLoggingMeal || isSubmitting}
						className="btn-secondary w-full py-4 text-lg"
					>
						{isLoggingMeal ? (
							<>
								<Loading size="sm" />
								Logging Meal...
							</>
						) : (
							<>
								<i className="ri-restaurant-line text-xl" />
								Log as Meal Directly
							</>
						)}
					</button>

					<button
						type="submit"
						disabled={isSubmitting || isLoggingMeal}
						className="btn-primary w-full py-4 text-lg"
					>
						{isSubmitting ? (
							<>
								<Loading size="sm" />
								Creating Recipe...
							</>
						) : (
							<>
								<i className="ri-check-line text-xl" />
								Create Recipe
							</>
						)}
					</button>
				</div>
			</form>
		</div>
	);
}
