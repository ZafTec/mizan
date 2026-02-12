'use client';

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { format, parse } from 'date-fns';
import { clientApi } from '@/lib/api.client';

type SimplifiedRecipe = {
  id: string;
  title: string;
  calories: number;
  protein: number;
}

type SelectedRecipe = {
  recipeId: string;
  recipeName: string;
  servings: number;
  mealTime: string;
}

export default function AddMealPlanPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const dateParam = searchParams.get('date');
  const weekParam = searchParams.get('week');

  const [date, setDate] = useState<Date>(
    dateParam
      ? parse(dateParam, 'yyyy-MM-dd', new Date())
      : new Date()
  );
  const [recipes, setRecipes] = useState<SimplifiedRecipe[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedRecipes, setSelectedRecipes] = useState<SelectedRecipe[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [filteredRecipes, setFilteredRecipes] = useState<SimplifiedRecipe[]>([]);

  useEffect(() => {
    const loadRecipes = async () => {
      try {
        const data = await clientApi<{ items: SimplifiedRecipe[] }>('/api/Recipes?IncludePublic=true&PageSize=50');
        const simplifiedRecipes = (data.items || []).map(recipe => ({
          id: recipe.id,
          title: recipe.title,
          calories: recipe.calories || 0,
          protein: recipe.protein || 0
        }));
        setRecipes(simplifiedRecipes);
        setFilteredRecipes(simplifiedRecipes);
      } catch (err) {
        setError('Failed to load recipes');
        console.error('Error loading recipes:', err);
      } finally {
        setLoading(false);
      }
    };

    loadRecipes();
  }, []);

  useEffect(() => {
    if (searchTerm.trim() === '') {
      setFilteredRecipes(recipes);
    } else {
      const filtered = recipes.filter(recipe =>
        recipe.title?.toLowerCase().includes(searchTerm.toLowerCase())
      );
      setFilteredRecipes(filtered);
    }
  }, [searchTerm, recipes]);

  const addRecipeToSelection = (recipe: SimplifiedRecipe) => {
    setSelectedRecipes([
      ...selectedRecipes,
      {
        recipeId: recipe.id,
        recipeName: recipe.title,
        servings: 1,
        mealTime: 'lunch'
      }
    ]);
  };

  const removeRecipeFromSelection = (index: number) => {
    const newSelectedRecipes = [...selectedRecipes];
    newSelectedRecipes.splice(index, 1);
    setSelectedRecipes(newSelectedRecipes);
  };

  const updateSelectedRecipe = (index: number, field: string, value: string | number) => {
    const newSelectedRecipes = [...selectedRecipes];
    newSelectedRecipes[index] = {
      ...newSelectedRecipes[index],
      [field]: value
    };
    setSelectedRecipes(newSelectedRecipes);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (selectedRecipes.length === 0) {
      setError('Please select at least one recipe');
      return;
    }

    setSubmitting(true);
    try {
      const dateString = format(date, 'yyyy-MM-dd');
      await clientApi('/api/MealPlans', {
        method: 'POST',
        body: {
          name: 'Meal Plan',
          startDate: dateString,
          endDate: dateString,
          recipes: selectedRecipes.map(item => ({
            recipeId: item.recipeId,
            date: dateString,
            mealType: item.mealTime,
            servings: item.servings
          }))
        }
      });

      if (weekParam) {
        router.push(`/meal-plan?week=${weekParam}`);
      } else {
        router.push('/meal-plan');
      }
      router.refresh();
    } catch (err) {
      console.error('Error adding meal plan:', err);
      setError('Failed to save meal plan');
      setSubmitting(false);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-slate-900">Add to Meal Plan</h1>
        <Link
          href={weekParam ? `/meal-plan?week=${weekParam}` : "/meal-plan"}
          className="btn-secondary"
        >
          Cancel
        </Link>
      </div>

      {error && (
        <div className="card p-4 bg-red-50 border border-red-200">
          <div className="flex items-center gap-2 text-red-700">
            <i className="ri-error-warning-line" />
            <span>{error}</span>
          </div>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="card p-4">
            <h2 className="text-lg font-semibold text-slate-900 mb-3">Select Date</h2>
            <input
              type="date"
              value={format(date, 'yyyy-MM-dd')}
              onChange={(e) => setDate(new Date(e.target.value))}
              className="input w-full"
            />
          </div>

          <div className="card p-4">
            <h2 className="text-lg font-semibold text-slate-900 mb-3">Selected Recipes</h2>
            {selectedRecipes.length === 0 ? (
              <p className="text-slate-500">No recipes selected yet</p>
            ) : (
              <ul className="space-y-4">
                {selectedRecipes.map((item, index) => (
                  <li key={`${item.recipeId}-${index}`} className="border-b border-slate-200 pb-3">
                    <div className="flex justify-between items-center">
                      <span className="font-medium text-slate-900">{item.recipeName}</span>
                      <button
                        type="button"
                        onClick={() => removeRecipeFromSelection(index)}
                        className="text-red-500 hover:text-red-700"
                      >
                        <i className="ri-delete-bin-line"></i>
                      </button>
                    </div>
                    <div className="grid grid-cols-2 gap-2 mt-2">
                      <div>
                        <label className="label">Servings</label>
                        <input
                          type="number"
                          min="1"
                          value={item.servings}
                          onChange={(e) => updateSelectedRecipe(index, 'servings', parseInt(e.target.value))}
                          className="input w-full"
                        />
                      </div>
                      <div>
                        <label className="label">Meal Time</label>
                        <select
                          value={item.mealTime}
                          onChange={(e) => updateSelectedRecipe(index, 'mealTime', e.target.value)}
                          className="input w-full"
                        >
                          <option value="breakfast">Breakfast</option>
                          <option value="lunch">Lunch</option>
                          <option value="dinner">Dinner</option>
                          <option value="snack">Snack</option>
                        </select>
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        <div className="card p-4">
          <h2 className="text-lg font-semibold text-slate-900 mb-3">Choose Recipes</h2>
          <div className="mb-4">
            <input
              type="text"
              placeholder="Search recipes..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="input w-full"
            />
          </div>

          {loading ? (
            <div className="flex justify-center py-8">
              <i className="ri-loader-4-line animate-spin text-brand-500 text-3xl" />
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
              {filteredRecipes.map((recipe) => (
                <div
                  key={recipe.id}
                  className="card p-3 hover:bg-slate-50 cursor-pointer transition"
                  onClick={() => addRecipeToSelection(recipe)}
                >
                  <h3 className="font-medium text-slate-900">{recipe.title}</h3>
                  <p className="text-sm text-slate-600">
                    {Math.round(recipe.calories)} cal | {Math.round(recipe.protein)}g protein
                  </p>
                </div>
              ))}
              {filteredRecipes.length === 0 && (
                <p className="text-slate-500 col-span-full text-center py-4">No recipes found</p>
              )}
            </div>
          )}
        </div>

        <div className="flex justify-end">
          <button
            type="submit"
            disabled={submitting || selectedRecipes.length === 0}
            className="btn-primary disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {submitting ? 'Saving...' : 'Save Meal Plan'}
          </button>
        </div>
      </form>
    </div>
  );
}
