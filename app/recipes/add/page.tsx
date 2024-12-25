'use client';

import Ingredient from "@/types/ingredient";
import { RecipeInput } from "@/types/recipe";
import { useEffect, useRef, useState } from "react";

import { CldUploadWidget } from 'next-cloudinary';
import Image from 'next/image';


type SelectedIngredient = {
	ingredient: Ingredient | null;
	name: string;
	amount: number | null;
	unit: "gram";
}

// TODO: variable according to screen size
const MAX_RECIPE_IMAGES_TO_PREVIEW = 1;


export default function Page() {
	const [name, setName] = useState('');
	const [images, setImages] = useState<string[]>([]);
	const [description, setDescription] = useState('');
	const [instructions, setInstructions] = useState('');
	const [selectedIngredients, setSelectedIngredients] = useState<SelectedIngredient[]>([]);
	const [servings, setServings] = useState(1);

	const [tags, setTags] = useState<Set<string>>(new Set<string>());
	const [currentTag, setCurrentTag] = useState('');

	const [ingredientSearch, setIngredientSearch] = useState<Ingredient[]>([]);
	const [activeDropdownIndex, setActiveDropdownIndex] = useState<number | null>(null);

	// dropdown options ref
	const dropdownRef = useRef<HTMLDivElement>(null);

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

		fetch(`/api/ingredients/${value}`)
		.then(res => res.json())
		.then(data => {
			const ingredients = data.ingredients;
			// console.log(ingredients);
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

	function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		const recipeData: RecipeInput = {
			name,
			description,
			// ingredients: selectedIngredients,
			ingredients: selectedIngredients.map(ing => ({
				ingredient: ing.ingredient!,
				amount: ing.amount!,
				unit: ing.unit,
			})),
			instructions: instructions.split('\n').filter(line => line.trim()),
			servings,
			tags: Array.from(tags),
			images: images,
		}

		fetch('/api/recipes/add', {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
			},
			body: JSON.stringify(recipeData),
		})
		.then(res => res.json())
		// .then(data => console.log(data))
	}

	return (
		<div className="max-w-4xl mx-auto p-6">
			<h1 className="text-3xl font-bold mb-6">Add New Recipe</h1>
			<form onSubmit={handleSubmit} className="space-y-6">
				{/* Basic Information */}
				<div className="space-y-4">
					<div>
						<label className="block text-sm font-medium mb-1" htmlFor="recipe_images">Recipe Images</label> 
						<CldUploadWidget
							// onQueuesEnd={(result, { widget }) => {
							// 	// widget.close();
							// }}
							onSuccess={(result) => {
								if (result?.info && result.info instanceof Object) {
									// console.log(result.info.secure_url);
									// setImages([...images, result.info.secure_url]);
									setImages((prevImages) => {
										if (result?.info && result.info instanceof Object) {
											return [...prevImages, result.info.secure_url]
										}
										return prevImages;
									});
								}
								// widget.close();
							}}
							signatureEndpoint="/api/sign-cloudinary-params"
						>
							{({ open }) => {
								return (
									<div className="flex flex-row gap-2 items-center">
										<button onClick={() => open()} className="bg-emerald-50 border-emerald-700 border-4 text-emerald-700 w-24 h-24 items-center rounded-md hover:bg-emerald-100 hover:border-emerald-600">
											<i className="ri-upload-cloud-2-line ri-3x"></i>
											<span className="sr-only">Upload Image</span>
										</button>
										{images.map((image, index) => {

											if (typeof image !== 'string' || index >= MAX_RECIPE_IMAGES_TO_PREVIEW) {
												return null;
											}
											
											return (
												<div key={index} className="relative w-24 h-24" draggable="false">
													<Image draggable="false" key={index} src={image} alt="Recipe image thumbnail" fill className="w-24 h-24 object-cover rounded-md m-1" />
													<button
														type="button"
														onClick={() => setImages(images.filter((_, i) => i !== index))}
														className="absolute items-center -top-3 -right-3 text-sm w-6 h-6 bg-red-600 opacity-70 text-white hover:opacity-85 rounded-full"
													>
														<i className="ri-close-line"></i>
													</button>
												</div>
											);
										})}

										{images.length > MAX_RECIPE_IMAGES_TO_PREVIEW && 
											<div className="w-24 h-24 bg-gray-200 flex items-center justify-center rounded-md">
												<p className="text-gray-500">+{images.length - MAX_RECIPE_IMAGES_TO_PREVIEW}</p>
											</div>
										}
									</div>
								);
							}}
						</CldUploadWidget>
					</div>
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
								<div ref={dropdownRef} className="relative w-full min-w-32">
									<input
										type="text"
										placeholder="Ingredient name"
										value={ing.name}
										onChange={(e) => handleIngredientNameChange(index, e.target.value)}
										className="flex-grow p-2 border rounded-md w-full"
									/>
									{activeDropdownIndex === index && (	
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
									className="w-fit max-w-20 md:max-w-24 p-2 border rounded-md"
								/>
								<input
									type="text"
									placeholder="Unit"
									defaultValue={ing.unit}
									disabled
									className="w-fit max-w-10 md:max-w-24 p-2 rounded-md"
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
