'use client';

import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { getAllIngredient } from "@/data/ingredient";
import type { Ingredient } from "@/data/ingredient";
import { CldUploadWidget } from 'next-cloudinary';
import Image from 'next/image';
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import Modal from "@/components/Modal";
import type { Recipe } from "@/data/recipe";

type SelectedIngredient = {
    ingredient: Ingredient | null;
    name: string;
    amount: number | null;
    unit: "gram";
}

const MAX_RECIPE_IMAGES_TO_PREVIEW = 3;

interface EditRecipeFormProps {
    recipe: Recipe;
}

export default function EditRecipeForm({ recipe }: EditRecipeFormProps) {
    const [name, setName] = useState(recipe.title || "");
    const [images, setImages] = useState<string[]>(recipe.imageUrl ? [recipe.imageUrl] : []);
    const [description, setDescription] = useState(recipe.description || '');
    const [instructions, setInstructions] = useState((recipe.instructions || []).map(i => i.instruction || "").join('\n'));
    const [selectedIngredients, setSelectedIngredients] = useState<SelectedIngredient[]>(
        (recipe.ingredients || []).map(ing => ({
            ingredient: {
                id: ing.foodId!,
                name: ing.foodName || ing.ingredientText || "",
                caloriesPer100g: 0,
                proteinPer100g: 0,
                carbsPer100g: 0,
                fatPer100g: 0,
                fiberPer100g: 0,
                servingSize: 0,
                servingUnit: 'g',
                isVerified: false
            },
            name: ing.foodName || ing.ingredientText || "",
            amount: ing.amount ?? null,
            unit: 'gram'
        }))
    );
    const [servings, setServings] = useState(recipe.servings || 1);
    const [isSubmitting, setIsSubmitting] = useState(false);

    const [tags, setTags] = useState<Set<string>>(new Set<string>(recipe.tags || []));
    const [currentTag, setCurrentTag] = useState('');

    const [ingredientSearch, setIngredientSearch] = useState<Ingredient[]>([]);
    const [activeDropdownIndex, setActiveDropdownIndex] = useState<number | null>(null);

    const dropdownRef = useRef<HTMLDivElement>(null);
    const router = useRouter();
    const [isModalOpen, setIsModalOpen] = useState(false);

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

    async function handleIngredientNameChange(index: number, value: string) {
        const newIngredients = [...selectedIngredients];
        newIngredients[index] = { ...newIngredients[index], name: value };
        setSelectedIngredients(newIngredients);
        setActiveDropdownIndex(index);

        if (!value) {
            setIngredientSearch([]);
            return;
        }

        const result = await getAllIngredient(value, undefined, undefined, 1, 4);
        setIngredientSearch(result.ingredients);
    }

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
            id: recipe.id,
            title: name,
            description,
            ingredients: selectedIngredients.map(ing => ({
                foodId: ing.ingredient?.id,
                ingredientText: ing.name,
                amount: ing.amount!,
                unit: ing.unit
            })),
            instructions: (instructions || '').split('\n').filter(line => line.trim()),
            servings,
            tags: Array.from(tags),
            imageUrl: images[0] || undefined,
            isPublic: true
        }

        try {
            await clientApi(`/api/Recipes/${recipe.id}`, {
                method: "PUT",
                body: recipeData,
            });
            router.push(`/recipes/${recipe.id}`);
            router.refresh();
        } catch (err) {
            console.error('[Recipe Update] Failed:', err);
            toast.error('Failed to update recipe. Please try again.');
        } finally {
            setIsSubmitting(false);
        }
    }

    return (
        <form onSubmit={handleSubmit} className="space-y-6">
            {/* Basic Information Card */}
            <div className="card p-6 space-y-5">
                <h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
                    <i className="ri-image-line text-brand-500" />
                    Basic Information
                </h2>

                {/* Image Upload */}
                <div>
                    <label className="label">Recipe Images</label>
                    <CldUploadWidget
                        onSuccess={(result) => {
                            if (result?.info && result.info instanceof Object) {
                                setImages([result.info.secure_url]);
                            }
                        }}
                        signatureEndpoint="/api/sign-cloudinary-params"
                    >
                        {({ open }) => (
                            <div className="flex flex-wrap gap-3">
                                <button
                                    type="button"
                                    onClick={() => open()}
                                    className="w-24 h-24 rounded-2xl border-2 border-dashed border-slate-300 dark:border-slate-600 hover:border-brand-400 bg-slate-50 dark:bg-slate-800 hover:bg-brand-50 flex flex-col items-center justify-center transition-colors group"
                                >
                                    <i className="ri-image-add-line text-2xl text-slate-400 group-hover:text-brand-500" />
                                    <span className="text-xs text-slate-400 group-hover:text-brand-500 mt-1">Change</span>
                                </button>
                                {images.map((image, index) => (
                                    <div key={index} className="relative w-24 h-24">
                                        <Image src={image} alt="Recipe" fill className="rounded-2xl object-cover border-2 border-slate-200 dark:border-slate-700" />
                                    </div>
                                ))}
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
                <h2 className="font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
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
                                    <div className="absolute z-50 w-full mt-1 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl shadow-lg overflow-hidden max-h-60 overflow-y-auto">
                                        {ingredientSearch.length > 0 ? (
                                            ingredientSearch.map((ingredient) => (
                                                <button
                                                    key={ingredient.id}
                                                    type="button"
                                                    onClick={() => handleIngredientSelect(index, ingredient)}
                                                    className="w-full p-3 text-left hover:bg-slate-50 dark:hover:bg-slate-800 flex items-center justify-between border-b border-slate-100 dark:border-slate-800 last:border-0"
                                                >
                                                    <span className="font-medium text-slate-900 dark:text-slate-100">{ingredient.name}</span>
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
                    placeholder="Enter each instruction on a new line..."
                />
            </div>

            {/* Additional Details Card */}
            <div className="card p-6 space-y-5">
                <h2 className="font-semibold text-slate-900 flex items-center gap-2">
                    <i className="ri-settings-3-line text-brand-500" />
                    Additional Details
                </h2>

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
                        placeholder="Type a tag and press Enter"
                    />
                </div>
            </div>

            {/* Submit Button */}
            <button
                type="submit"
                disabled={isSubmitting}
                className="btn-primary w-full py-4 text-lg"
            >
                {isSubmitting ? (
                    <>
                        <svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
                            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                        </svg>
                        Updating Recipe...
                    </>
                ) : (
                    <>
                        <i className="ri-check-line text-xl" />
                        Save Changes
                    </>
                )}
            </button>
        </form>
    );
}
