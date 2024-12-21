'use client';

// import Ingredient from "@/types/ingredient";
import { useState } from "react";

type ingredient = {
	ingredient: string;
	amount: number;
	unit: string;
}

export default function Page() {
	const [name, setName] = useState('');
	const [description, setDescription] = useState('');
	const [ingredients, setIngredients] = useState<ingredient[]>([]);
	const [instructions, setInstructions] = useState('');
	const [servings, setServings] = useState(1);

	const [tags, setTags] = useState<Set<string>>(new Set<string>());
	const [currentTag, setCurrentTag] = useState('');

	function handleAddIngredient() {
		setIngredients([...ingredients, { ingredient: '', amount: 0, unit: 'g' }]);
	}

	function handleRemoveIngredient(index: number) {
		const newIngredients = ingredients.filter((_, i) => i !== index);
		setIngredients(newIngredients);
	}

	// function handleIngredientChange(index: number, field: keyof Ingredient, value: string | number) {
	// 	const newIngredients = [...ingredients];
	// 	newIngredients[index] = { ...newIngredients[index], [field]: value };
	// 	setIngredients(newIngredients);
	// }

	function handleAddTag(e: React.KeyboardEvent<HTMLInputElement>) {
		if (e.key === 'Enter' && currentTag.trim()) {
			e.preventDefault();
			// setTags([...tags, currentTag.trim()]);
			setTags(new Set([...tags, currentTag.trim()]));
			setCurrentTag('');
		}
	}

	function handleRemoveTag(tagToRemove: string) {
		// setTags(tags.filter(tag => tag !== tagToRemove));
		const newTags = new Set(tags);
		newTags.delete(tagToRemove);
		setTags(newTags);
	}

	function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		const recipeData = {
			name,
			description,
			ingredients,
			instructions: instructions.split('\n').filter(line => line.trim()),
			servings,
			tags,
		}
		// Add your API call here
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
						/>
					</div>
				</div>

				{/* Ingredients */}
				<div>
					<label className="block text-sm font-medium mb-2">Ingredients</label>
					<div className="space-y-2">
						{ingredients.map((ing, index) => (
							<div key={index} className="flex gap-2 items-center">
								<input
									type="text"
									placeholder="Ingredient"
									value={ing.ingredient}
									// onChange={(e) => handleIngredientChange(index, 'ingredient', e.target.value)}
									className="flex-grow p-2 border rounded-md"
								/>
								<input
									type="number"
									placeholder="Amount"
									value={ing.amount}
									// onChange={(e) => handleIngredientChange(index, 'amount', parseFloat(e.target.value))}
									className="w-24 p-2 border rounded-md"
								/>
								<input
									type="text"
									placeholder="Unit"
									value={ing.unit}
									// onChange={(e) => handleIngredientChange(index, 'unit', e.target.value)}
									className="w-24 p-2 border rounded-md"
								/>
								<button
									type="button"
									onClick={() => handleRemoveIngredient(index)}
									className="p-2 text-red-500"
								>
									{/* <X size={20} /> */}
									<i className="ri-close-large-line"></i>
								</button>
							</div>
						))}
						<button
							type="button"
							onClick={handleAddIngredient}
							className="flex items-center gap-2 text-emerald-700"
						>
							{/* <Plus size={20} />  */}
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
								className="bg-orange-100 px-2 py-1 rounded-md flex items-center gap-1"
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
