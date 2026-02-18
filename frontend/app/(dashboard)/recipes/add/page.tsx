'use client';

import { useEffect, useRef, useState } from "react";
import { getAllIngredient } from "@/data/ingredient";
import type { Ingredient } from "@/data/ingredient";

import { CldUploadWidget } from 'next-cloudinary';
import Image from 'next/image';
import Link from 'next/link';
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import Modal from "@/components/Modal";
import { toast } from "sonner";
import { Spinner } from "@/components/ui/spinner";
import { useDebounce } from "@/lib/hooks/useDebounce";

type SelectedIngredient = {
	ingredient: Ingredient | null;
	name: string;
	amount: number | null;
	unit: "gram";
}

const MAX_RECIPE_IMAGES_TO_PREVIEW = 3;

export default function Page() {
	const [name, setName] = useState('');
	const [images, setImages] = useState<string[]>([]);
	const [description, setDescription] = useState('');
	const [instructions, setInstructions] = useState('');
	const [selectedIngredients, setSelectedIngredients] = useState<SelectedIngredient[]>([]);
	const [servings, setServings] = useState(1);
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

	// Calculate total macros (macros are per 100g)
	const totalMacros = selectedIngredients.reduce(
		(acc, ing) => {
			if (ing.ingredient && ing.amount) {
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
		setSelectedIngredients([...selectedIngredients, { ingredient: null, name: '', amount: null, unit: 'gram' }]);
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
			ingredient: selectedIngredient,
			name: selectedIngredient.name,
			amount: null,
			unit: 'gram',
		}
		setSelectedIngredients(newIngredients);
		setActiveDropdownIndex(null);
	}

	function handleIngredientAmountChange(index: number, value: number) {
		const newIngredients = [...selectedIngredients];
		newIngredients[index] = { ...newIngredients[index], amount: value };
		setSelectedIngredients(newIngredients);
	}

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		setIsSubmitting(true);

		const recipeData = {
			title: name,
			description,
			ingredients: selectedIngredients.map(ing => ({
				foodId: ing.ingredient!.id,
				ingredientText: ing.name,
				amount: ing.amount!,
				unit: ing.unit
			})),
			instructions: instructions.split('\n').filter(line => line.trim()),
			servings,
			tags: Array.from(tags),
			// prepTimeMinutes: prepTime || undefined, // Assuming prepTime is not yet implemented
			// cookTimeMinutes: cookTime || undefined, // Assuming cookTime is not yet implemented
			imageUrl: images[0] || undefined,
			isPublic: visibility === 'public',
			householdId: visibility === 'household' ? null : undefined
			// NOTE: Nutrition is calculated on backend from ingredients
			// NOTE: HouseholdId will be set to actual household ID once household selection is implemented
		}


		try {
			await clientApi('/api/Recipes', {
				method: 'POST',
				body: recipeData,
			});
			router.push('/recipes');
		} catch (err) {
			console.error('[Recipe Create] Failed:', err);
			toast.error('Failed to create recipe. Please try again.');
		} finally {
			setIsSubmitting(false);
		}
	}

	return (
		<div className="max-w-4xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex items-center justify-between">
				<div className="flex items-center gap-4">
					<Link href="/recipes" className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
						<i className="ri-arrow-left-line text-xl text-slate-600" />
					</Link>
					<div>
						<h1 className="text-2xl font-bold text-slate-900">Create Recipe</h1>
						<p className="text-slate-500">Add a new recipe to your collection</p>
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
				<h2 className="text-xl font-bold text-slate-900 mb-4">Recipe Macros Overview</h2>
				<div className="overflow-x-auto">
					<table className="w-full text-sm">
						<thead>
							<tr className="bg-slate-50">
								<th className="p-3 text-left font-medium text-slate-600 rounded-tl-xl">Ingredient</th>
								<th className="p-3 text-right font-medium text-slate-600">Amount</th>
								<th className="p-3 text-right font-medium text-slate-600">Calories</th>
								<th className="p-3 text-right font-medium text-slate-600">Protein</th>
								<th className="p-3 text-right font-medium text-slate-600">Carbs</th>
								<th className="p-3 text-right font-medium text-slate-600">Fat</th>
								<th className="p-3 text-right font-medium text-slate-600 rounded-tr-xl">Fiber</th>
							</tr>
						</thead>
						<tbody className="divide-y divide-slate-100">
							{selectedIngredients.map((ing, idx) => {
								if (!ing.ingredient || !ing.amount) return null;
								const ratio = ing.amount / 100;
								return (
									<tr key={idx} className="hover:bg-slate-50">
										<td className="p-3 text-slate-900">{ing.ingredient.name}</td>
										<td className="p-3 text-right text-slate-600">{ing.amount}g</td>
										<td className="p-3 text-right text-slate-600">{((ing.ingredient.caloriesPer100g || 0) * ratio).toFixed(0)}</td>
										<td className="p-3 text-right text-slate-600">{((ing.ingredient.proteinPer100g || 0) * ratio).toFixed(1)}g</td>
										<td className="p-3 text-right text-slate-600">{((ing.ingredient.carbsPer100g || 0) * ratio).toFixed(1)}g</td>
										<td className="p-3 text-right text-slate-600">{((ing.ingredient.fatPer100g || 0) * ratio).toFixed(1)}g</td>
										<td className="p-3 text-right text-slate-600">{((ing.ingredient.fiberPer100g || 0) * ratio).toFixed(1)}g</td>
									</tr>
								);
							})}
						</tbody>
						<tfoot>
							<tr className="bg-brand-50 font-semibold">
								<td className="p-3 text-brand-900 rounded-bl-xl">Total</td>
								<td className="p-3 text-right text-brand-600"></td>
								<td className="p-3 text-right text-brand-600">{totalMacros.calories.toFixed(0)}</td>
								<td className="p-3 text-right text-brand-600">{totalMacros.protein.toFixed(1)}g</td>
								<td className="p-3 text-right text-brand-600">{totalMacros.carbs.toFixed(1)}g</td>
								<td className="p-3 text-right text-brand-600">{totalMacros.fat.toFixed(1)}g</td>
								<td className="p-3 text-right text-brand-600 rounded-br-xl">{totalMacros.fiber.toFixed(1)}g</td>
							</tr>
						</tfoot>
					</table>
				</div>
			</Modal>

			<form onSubmit={handleSubmit} className="space-y-6">
				{/* Basic Information Card */}
				<div className="card p-6 space-y-5">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
						<i className="ri-image-line text-brand-500" />
						Basic Information
					</h2>

					{/* Image Upload */}
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
										className="w-24 h-24 rounded-2xl border-2 border-dashed border-slate-300 hover:border-brand-400 bg-slate-50 hover:bg-brand-50 flex flex-col items-center justify-center transition-colors group"
									>
										<i className="ri-image-add-line text-2xl text-slate-400 group-hover:text-brand-500" />
										<span className="text-xs text-slate-400 group-hover:text-brand-500 mt-1">Add</span>
									</button>
									{images.map((image, index) => {
										if (typeof image !== 'string' || index >= MAX_RECIPE_IMAGES_TO_PREVIEW) return null;
										return (
											<div key={index} className="relative w-24 h-24">
												<Image src={image} alt="Recipe" fill className="rounded-2xl object-cover border-2 border-slate-200" />
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
										<div className="w-24 h-24 rounded-2xl bg-slate-100 flex items-center justify-center">
											<span className="text-slate-500 font-medium">+{images.length - MAX_RECIPE_IMAGES_TO_PREVIEW}</span>
										</div>
									)}
								</div>
							)}
						</CldUploadWidget>
					</div>

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
				<div className="card p-6 space-y-4 relative overflow-visible!">
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
						<i className="ri-list-check-2 text-brand-500" />
						Ingredients
					</h2>

					<div className="space-y-3">
						{selectedIngredients.map((ing, index) => (
							<div key={index} className="flex flex-col sm:flex-row gap-2 sm:gap-3 items-stretch sm:items-start">
								<div ref={dropdownRef} className="relative flex-1 min-w-0">
									<input
										type="text"
										placeholder="Search ingredient..."
										value={ing.name}
										onChange={(e) => handleIngredientNameChange(index, e.target.value)}
										className="input w-full"
									/>
									{activeDropdownIndex === index && (
										<div className="absolute z-50 w-full mt-1 bg-white border border-slate-200 rounded-xl shadow-lg overflow-hidden max-h-60 overflow-y-auto">
											{ingredientSearch.length > 0 ? (
												ingredientSearch.map((ingredient) => (
													<button
														key={ingredient.id}
														type="button"
														onClick={() => handleIngredientSelect(index, ingredient)}
														className="w-full p-3 text-left hover:bg-slate-50 flex items-center justify-between border-b border-slate-100 last:border-0"
													>
														<span className="font-medium text-slate-900">{ingredient.name}</span>
														<span className="text-xs text-slate-500">{ingredient.caloriesPer100g} kcal/100g</span>
													</button>
												))
											) : (
												<div className="p-3 text-slate-500 text-center">No ingredients found</div>
											)}
										</div>
									)}
								</div>
								<div className="flex gap-2 sm:gap-3 items-center">
									<input
										type="number"
										placeholder="Amount"
										value={ing.amount?.toString() || ''}
										onChange={(e) => handleIngredientAmountChange(index, parseFloat(e.target.value))}
										className="input w-24 sm:w-28"
										min="0"
										step="0.1"
									/>
									<span className="px-3 py-2.5 bg-slate-100 rounded-xl text-slate-600 text-sm font-medium whitespace-nowrap">grams</span>
									<button
										type="button"
										onClick={() => handleRemoveIngredient(index)}
										className="w-10 h-10 rounded-xl bg-red-50 hover:bg-red-100 text-red-500 flex items-center justify-center transition-colors shrink-0"
										aria-label="Remove ingredient"
									>
										<i className="ri-delete-bin-line" />
									</button>
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
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
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
					<h2 className="font-semibold text-slate-900 flex items-center gap-2">
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
						<p className="text-sm text-slate-500 mt-1.5">
							{visibility === 'private' && 'Only you can view and edit this recipe'}
							{visibility === 'household' && 'All members of your household can view this recipe'}
							{visibility === 'public' && 'Anyone can view this recipe'}
						</p>
					</div>

					{/* Servings */}
					<div>
						<label htmlFor="servings" className="label">Servings</label>
						<div className="flex items-center gap-3">
							<button
								type="button"
								onClick={() => setServings(Math.max(1, servings - 1))}
								className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors"
							>
								<i className="ri-subtract-line text-slate-600" />
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
								className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors"
							>
								<i className="ri-add-line text-slate-600" />
							</button>
						</div>
					</div>

					{/* Tags */}
					<div>
						<label htmlFor="tag" className="label">Tags</label>
						{tags.size > 0 && (
							<div className="flex flex-wrap gap-2 mb-3">
								{Array.from(tags).map((tag, index) => (
									<span key={index} className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-brand-100 text-brand-700 text-sm font-medium">
										{tag}
										<button type="button" onClick={() => handleRemoveTag(tag)} className="hover:text-brand-900">
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
				{selectedIngredients.some(ing => ing.ingredient && ing.amount) && (
					<div className="card p-6 bg-linear-to-br from-brand-50 to-accent-50">
						<h2 className="font-semibold text-slate-900 mb-4">Nutrition Summary</h2>
						<div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
							<div className="text-center p-3 bg-white rounded-xl">
								<p className="text-2xl font-bold text-orange-600">{totalMacros.calories.toFixed(0)}</p>
								<p className="text-xs text-slate-500">Calories</p>
							</div>
							<div className="text-center p-3 bg-white rounded-xl">
								<p className="text-2xl font-bold text-red-600">{totalMacros.protein.toFixed(1)}g</p>
								<p className="text-xs text-slate-500">Protein</p>
							</div>
							<div className="text-center p-3 bg-white rounded-xl">
								<p className="text-2xl font-bold text-amber-600">{totalMacros.carbs.toFixed(1)}g</p>
								<p className="text-xs text-slate-500">Carbs</p>
							</div>
							<div className="text-center p-3 bg-white rounded-xl">
								<p className="text-2xl font-bold text-yellow-600">{totalMacros.fat.toFixed(1)}g</p>
								<p className="text-xs text-slate-500">Fat</p>
							</div>
							<div className="text-center p-3 bg-white rounded-xl">
								<p className="text-2xl font-bold text-green-600">{totalMacros.fiber.toFixed(1)}g</p>
								<p className="text-xs text-slate-500">Fiber</p>
							</div>
						</div>
					</div>
				)}

				{/* Submit Button */}
				<button
					type="submit"
					disabled={isSubmitting}
					className="btn-primary w-full py-4 text-lg"
				>
					{isSubmitting ? (
						<>
							<Spinner />
							Creating Recipe...
						</>
					) : (
						<>
							<i className="ri-check-line text-xl" />
							Create Recipe
						</>
					)}
				</button>
			</form>
		</div>
	);
}
