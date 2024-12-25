'use client';

import Ingredient from "@/types/ingredient";
import { Schema } from "mongoose";
import { useEffect, useRef, useState } from "react";

type SelectedIngredient = {
	id: string | Schema.Types.ObjectId;
	name: string;
	amount: number | null;
	unit: "gram";
}


export default function Page() {
	const [name, setName] = useState('');
	const [description, setDescription] = useState('');
	const [instructions, setInstructions] = useState('');
	const [selectedIngredients, setSelectedIngredients] = useState<SelectedIngredient[]>([]);
	const [servings, setServings] = useState(1);

	const [tags, setTags] = useState<Set<string>>(new Set<string>());
	const [currentTag, setCurrentTag] = useState('');

	const [ingredientSearch, setIngredientSearch] = useState<Ingredient[]>([]);
	const [showIngredientDropdown, setShowIngredientDropdown] = useState(false);

	// dropdown options ref
	const dropdownRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		function handleClickOutside(event: MouseEvent) {
			if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
				setShowIngredientDropdown(false);
			}
		}

		document.addEventListener('mousedown', handleClickOutside);
		return () => {
			document.removeEventListener('mousedown', handleClickOutside);
		}
	}, []);

	function handleAddIngredient() {
		setSelectedIngredients([...selectedIngredients, { id: '', name: '', amount: null, unit: 'gram' }]);
	}

	function handleRemoveIngredient(index: number) {
		const newIngredients = selectedIngredients.filter((_, i) => i !== index);
		setSelectedIngredients(newIngredients);
	}

	function handleIngredientNameChange(index: number, value: string) {
		const newIngredients = [...selectedIngredients];
		newIngredients[index] = { ...newIngredients[index], name: value };
		setSelectedIngredients(newIngredients);
		setShowIngredientDropdown(true);

		fetch(`/api/ingredients/${value}`)
		.then(res => res.json())
		.then(data => {
			const ingredients = data.ingredients;
			console.log(ingredients);
			setIngredientSearch(ingredients);
		})
	}

	function handleAddTag(e: React.KeyboardEvent<HTMLInputElement>) {
		if (e.key === 'Enter' && currentTag.trim()) {
			e.preventDefault();
			// setTags([...tags, currentTag.trim()]);
			setTags(new Set([...tags, currentTag.trim().toUpperCase()]));
			setCurrentTag('');
		}
	}

	function handleRemoveTag(tagToRemove: string) {
		// setTags(tags.filter(tag => tag !== tagToRemove));
		const newTags = new Set(tags);
		newTags.delete(tagToRemove);
		setTags(newTags);
	}

	function handleIngredientSelect(ingredientIndex: number, selectedIngredient: Ingredient) {
		const newIngredients = [...selectedIngredients];
		newIngredients[ingredientIndex] = {
			id: selectedIngredient._id,
			name: selectedIngredient.name,
			amount: null,
			unit: 'gram',
		}
		setSelectedIngredients(newIngredients);
		setShowIngredientDropdown(false);
	}

	function handleIngredientAmountChange(index: number, value: number) {
		const newIngredients = [...selectedIngredients];
		newIngredients[index] = { ...newIngredients[index], amount: value };
		setSelectedIngredients(newIngredients);
	}

	function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		const recipeData = {
			name,
			description,
			selectedIngredients,
			instructions: instructions.split('\n').filter(line => line.trim()),
			servings,
			tags,
		}
		// API call here
		console.log(recipeData);
	}

	return (
		<div className="max-w-4xl mx-auto p-6">
			<h1 className="text-3xl font-bold mb-6">Add New Recipe</h1>
			<form onSubmit={handleSubmit} className="space-y-6">
				{/* Basic Information */}
				<div className="space-y-4">
					<div>
						<label className="block text-sm font-medium mb-1" htmlFor="recipe_name">Recipe Name</label>
						<input
							id="recipe_name"
							name="recipe_name"
							type="text"
							value={name}
							onChange={(e) => setName(e.target.value)}
							className="w-full p-2 border rounded-md"
							required
						/>
					</div>
					<div>
						<label className="block text-sm font-medium mb-1" htmlFor="description">Description</label>
						<textarea
							id="description"
							name="description"
							value={description}
							onChange={(e) => setDescription(e.target.value)}
							className="w-full p-2 border rounded-md"
							rows={3}
							required
						/>
					</div>
				</div>

				{/* Ingredients */}
				<div>
					<label className="block text-sm font-medium mb-2">Ingredients</label>
					<div className="space-y-2">
						{selectedIngredients.map((ing, index) => (
							<div key={index} className="flex gap-2 items-center">
								<div ref={dropdownRef} className="relative w-full">
									<input
										type="text"
										placeholder="Ingredient name"
										value={ing.name}
										onChange={(e) => handleIngredientNameChange(index, e.target.value)}
										className="flex-grow p-2 border rounded-md w-full"
									/>
									{showIngredientDropdown && (	
										ingredientSearch.length > 0 ? (
											<ul className="absolute z-10 w-full bg-white border rounded-md shadow-md mt-1">
												{ingredientSearch.map((ingredient) => (
													// TODO: display ingredient details with macros
													<li
														key={ingredient._id.toString()}
														className="p-2 hover:bg-gray-100 cursor-pointer"
														onClick={() => handleIngredientSelect(index, ingredient)}
													>
														{ingredient.name}
													</li>
												))}
											</ul>
										) : (
											<ul className="absolute z-10 w-full bg-white border rounded-md shadow-md mt-1">
												{/* TODO: a add ingredient button */}
												<li className="p-2">No ingredients found</li>
											</ul>
										)
									)}
								</div>

								<input
									type="number"
									placeholder="Amount"
									value={ing.amount?.toString() || ''}
									onChange={(e) => handleIngredientAmountChange(index, parseFloat(e.target.value))}
									className="w-24 p-2 border rounded-md"
								/>
								<input
									type="text"
									placeholder="Unit"
									defaultValue={ing.unit}
									disabled
									className="w-24 p-2 rounded-md"
								/>
								<button
									type="button"
									onClick={() => handleRemoveIngredient(index)}
									className="p-2 text-red-500"
								>
									<i className="ri-close-large-line"></i>
								</button>
							</div>
						))}
						<button
							type="button"
							onClick={handleAddIngredient}
							className="flex items-center gap-2 text-emerald-700"
						>
							<i className="ri-add-line"></i>
							Add Ingredient
						</button>
					</div>
				</div>

				{/* Instructions */}
				<div>
					<label className="block text-sm font-medium mb-1" htmlFor="instructions">Instructions</label>
					<textarea
						id="instructions"
						name="instructions"
						value={instructions}
						onChange={(e) => setInstructions(e.target.value)}
						className="w-full p-2 border rounded-md"
						rows={6}
						placeholder="Enter each instruction on a new line"
					/>
				</div>

				{/* Servings */}
				<div>
					<label className="block text-sm font-medium mb-1" htmlFor="servings">Servings</label>
					<input
						id="servings"
						name="servings"
						type="number"
						value={servings}
						onChange={(e) => setServings(parseInt(e.target.value))}
						className="w-24 p-2 border rounded-md"
						min="1"
					/>
				</div>

				{/* Tags */}
				<div>
					<label className="block text-sm font-medium mb-1" htmlFor="tag">Tags</label>
					<div className="flex flex-wrap gap-2 mb-2">
						{/* {tags.map((tag, index) => ( */}
						{Array.from(tags).map((tag, index) => (
							<span
								key={index}
								className="bg-orange-100 text-sm px-2 py-1 rounded-md flex items-center gap-1"
							>
								{tag}
								<button
									type="button"
									onClick={() => handleRemoveTag(tag)}
									className="text-orange-500"
								>
									{/* <X size={16} /> */}
									<i className="ri-close-large-line"></i>
								</button>
							</span>
						))}
					</div>
					<input
						id="tag"
						name="tag"
						type="text"
						value={currentTag}
						onChange={(e) => setCurrentTag(e.target.value)}
						onKeyDown={handleAddTag}
						className="w-full p-2 border rounded-md"
						placeholder="Type tag and press Enter"
					/>
				</div>

				<button
					type="submit"
					className="w-full bg-emerald-700 text-white py-2 px-4 rounded-md hover:bg-emerald-600"
				>
					Add Recipe
				</button>
			</form>
		</div>
	)
}
