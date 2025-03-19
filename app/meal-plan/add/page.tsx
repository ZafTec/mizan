'use client';

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { format, parse } from 'date-fns';
import RecipeType from '@/types/recipe';

type SimplifiedRecipe = {
  _id: string;
  name: string;
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
  const [loadingExisting, setLoadingExisting] = useState(false);

  // Load all recipes
  useEffect(() => {
    const loadRecipes = async () => {
      await fetch('/api/recipes')
        .then(res => res.json())
        .then(data => {
          return data.recipes
        })
        .then((recipes: RecipeType[]) => {
          // Simplify recipe objects to avoid circular references
          const simplifiedRecipes = recipes.map(recipe => ({
            _id: recipe._id.toString(),
            name: recipe.name,
            calories: recipe.totalMacros.calories,
            protein: recipe.totalMacros.protein
          }));
          setRecipes(simplifiedRecipes);
          setFilteredRecipes(simplifiedRecipes);
        })
        .catch(err => {
          setError('Failed to load recipes');
          console.debug("==================")
          console.debug(err)
          console.debug("==================")
        })
        .finally(() => {
          setLoading(false);
        })
    };

    loadRecipes();
  }, []);

  // Load existing meal plan for the selected date
  useEffect(() => {
    const loadExistingMealPlan = async () => {
      if (!date) return;
      
      setLoadingExisting(true);
      try {
        const dateString = format(date, 'yyyy-MM-dd');
        const response = await fetch(`/api/meal-plan/get-by-date?date=${dateString}`);
        const data = await response.json();
        
        if (data.success && data.mealPlan) {
          setSelectedRecipes(data.mealPlan.recipes);
        } else {
          // If no meal plan exists for this date, clear any selected recipes
          setSelectedRecipes([]);
        }
      } catch (err) {
        console.error("Error loading existing meal plan:", err);
        // Don't set error state here to avoid confusing the user
      } finally {
        setLoadingExisting(false);
      }
    };

    loadExistingMealPlan();
  }, [date]);

  useEffect(() => {
    if (searchTerm.trim() === '') {
      setFilteredRecipes(recipes);
    } else {
      const filtered = recipes.filter(recipe => 
        recipe.name?.toLowerCase().includes(searchTerm.toLowerCase())
      );
      setFilteredRecipes(filtered);
    }
  }, [searchTerm, recipes]);

  const addRecipeToSelection = (recipe: any) => {
    setSelectedRecipes([
      ...selectedRecipes,
      {
        recipeId: recipe._id,
        recipeName: recipe.name,
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
    await fetch('/api/meal-plan/add', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        date: format(date, 'yyyy-MM-dd'),
        recipes: selectedRecipes.map(item => ({
          recipeId: item.recipeId,
          servings: item.servings,
          mealTime: item.mealTime
        }))
      })
    })
    .then(res => {
      if (!res.ok) {
        throw new Error('Failed to save meal plan');
      }
      // Redirect back to the meal plan page with week parameter if it exists
      if (weekParam) {
        router.push(`/meal-plan?week=${weekParam}`);
      } else {
        router.push('/meal-plan');
      }
      router.refresh();
    })
    .catch(err => {
      console.error('Error adding meal plan:', err);
      setError('Failed to save meal plan');
      setSubmitting(false);
    })
    .finally(() => {
      setSubmitting(false);
    })
  };

  return (
    <div className="flex flex-col gap-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">Add to Meal Plan</h1>
        <Link 
          href={weekParam ? `/meal-plan?week=${weekParam}` : "/meal-plan"}
          className="bg-gray-200 text-gray-700 px-4 py-2 rounded-lg"
        >
          Cancel
        </Link>
      </div>

      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* Date Selection */}
          <div className="bg-white p-4 rounded-lg shadow">
            <h2 className="text-lg font-semibold mb-3">Select Date</h2>
            <input
              type="date"
              value={format(date, 'yyyy-MM-dd')}
              onChange={(e) => setDate(new Date(e.target.value))}
              className="w-full p-2 border rounded"
            />
            {loadingExisting && (
              <div className="mt-2 text-sm text-gray-500">
                Loading saved plan...
              </div>
            )}
          </div>

          {/* Selected Recipes */}
          <div className="bg-white p-4 rounded-lg shadow">
            <h2 className="text-lg font-semibold mb-3">Selected Recipes</h2>
            {selectedRecipes.length === 0 ? (
              <p className="text-gray-500">No recipes selected yet</p>
            ) : (
              <ul className="space-y-4">
                {selectedRecipes.map((item, index) => (
                  <li key={`${item.recipeId}-${index}`} className="border-b pb-3">
                    <div className="flex justify-between items-center">
                      <span className="font-medium">{item.recipeName}</span>
                      <button
                        type="button"
                        onClick={() => removeRecipeFromSelection(index)}
                        className="text-red-500"
                      >
                        <i className="ri-delete-bin-line"></i>
                      </button>
                    </div>
                    <div className="grid grid-cols-2 gap-2 mt-2">
                      <div>
                        <label className="block text-sm text-gray-600">Servings</label>
                        <input
                          type="number"
                          min="1"
                          value={item.servings}
                          onChange={(e) => updateSelectedRecipe(index, 'servings', parseInt(e.target.value))}
                          className="w-full p-1 border rounded text-sm"
                        />
                      </div>
                      <div>
                        <label className="block text-sm text-gray-600">Meal Time</label>
                        <select
                          value={item.mealTime}
                          onChange={(e) => updateSelectedRecipe(index, 'mealTime', e.target.value)}
                          className="w-full p-1 border rounded text-sm"
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

        {/* Recipe Selection */}
        <div className="bg-white p-4 rounded-lg shadow">
          <h2 className="text-lg font-semibold mb-3">Choose Recipes</h2>
          <div className="mb-4">
            <input
              type="text"
              placeholder="Search recipes..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full p-2 border rounded"
            />
          </div>

          {loading ? (
            <p className="text-gray-500">Loading recipes...</p>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
              {filteredRecipes.map((recipe) => (
                <div 
                  key={recipe._id?.toString()}
                  className="border rounded-lg p-3 hover:bg-gray-50 cursor-pointer transition"
                  onClick={() => addRecipeToSelection(recipe)}
                >
                  <h3 className="font-medium">{recipe.name}</h3>
                  <p className="text-sm text-gray-600">
                    {Math.round(recipe.calories)} cal | {Math.round(recipe.protein)}g protein
                  </p>
                </div>
              ))}
              {filteredRecipes.length === 0 && (
                <p className="text-gray-500 col-span-full">No recipes found</p>
              )}
            </div>
          )}
        </div>

        <div className="flex justify-end">
          <button
            type="submit"
            disabled={submitting || selectedRecipes.length === 0}
            className="bg-emerald-700 text-white px-6 py-2 rounded-lg disabled:bg-gray-300 disabled:cursor-not-allowed"
          >
            {submitting ? 'Saving...' : 'Save Meal Plan'}
          </button>
        </div>
      </form>
    </div>
  );
}
