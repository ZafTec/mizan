'use client';

import { useState, useEffect } from 'react';
import { format, startOfWeek, endOfWeek, parse } from 'date-fns';
import MealPlanningCalendar from './MealPlanningCalendar';
import { PerDayMealsAggregate } from '@/types/meal';
import { useRouter, useSearchParams } from 'next/navigation';

type PlannedMealType = {
  _id: string;
  date: Date;
  recipes: {
    recipe: {
      _id: string;
      name: string;
    };
    servings: number;
    mealTime: string;
  }[];
  totalCalories: number;
};

type ShoppingListItem = {
  ingredient: string;
  amount: number;
  unit: string;
  category: string;
};

interface MealPlanningCalendarWrapperProps {
  initialMeals: PerDayMealsAggregate[];
  initialPlannedMeals: PlannedMealType[];
}

export default function MealPlanningCalendarWrapper({ 
  initialMeals, 
  initialPlannedMeals
}: MealPlanningCalendarWrapperProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const weekParam = searchParams.get('week');
  
  // Initialize date from URL or use current date
  const initialDate = weekParam 
    ? parse(weekParam, 'yyyy-MM-dd', new Date())
    : new Date();
  
  const [currentDate, setCurrentDate] = useState(initialDate);
  const [weekStart, setWeekStart] = useState(startOfWeek(initialDate, { weekStartsOn: 1 }));
  const [weekEnd, setWeekEnd] = useState(endOfWeek(initialDate, { weekStartsOn: 1 }));
  const [plannedMeals, setPlannedMeals] = useState<PlannedMealType[]>(initialPlannedMeals);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [ingredients, setIngredients] = useState<ShoppingListItem[]>([]);
  const [loadingIngredients, setLoadingIngredients] = useState(false);

  // Update URL when week changes
  useEffect(() => {
    // Don't update URL on initial render if no week param
    if (!weekParam && format(weekStart, 'yyyy-MM-dd') === format(startOfWeek(new Date(), { weekStartsOn: 1 }), 'yyyy-MM-dd')) {
      return;
    }
    
    // Create a new URLSearchParams object
    const params = new URLSearchParams(searchParams.toString());
    params.set('week', format(currentDate, 'yyyy-MM-dd'));
    
    // Update URL without refreshing the page
    router.push(`/meal-plan?${params.toString()}`, { scroll: false });
  }, [currentDate, weekStart, router, searchParams, weekParam]);

  // Load meal plans for the selected week
  useEffect(() => {
    const fetchWeeklyMealPlans = async () => {
      setLoading(true);
      setError(null);
      
      try {
        const dateStr = format(currentDate, 'yyyy-MM-dd');
        const response = await fetch(`/api/meal-plan/get-weekly?date=${dateStr}`);
        
        if (!response.ok) {
          throw new Error('Failed to fetch weekly meal plans');
        }
        
        const data = await response.json();
        
        if (data.success) {
          setPlannedMeals(data.mealPlans.map((plan: any) => ({
            ...plan,
            date: new Date(plan.date)
          })));
        } else {
          throw new Error(data.error || 'Unknown error');
        }
      } catch (err: any) {
        console.error('Error fetching weekly meal plans:', err);
        setError(err.message || 'Failed to load meal plans');
      } finally {
        setLoading(false);
      }
    };
    
    // Check if we need to fetch data (initial load with week param or week changed)
    if (weekParam || 
        format(weekStart, 'yyyy-MM-dd') !== format(startOfWeek(new Date(), { weekStartsOn: 1 }), 'yyyy-MM-dd')) {
      fetchWeeklyMealPlans();
    }
  }, [currentDate, weekStart, weekParam]);
  
  // Fetch shopping list for the week
  useEffect(() => {
    const fetchWeeklyIngredients = async () => {
      setLoadingIngredients(true);
      
      try {
        const dateStr = format(currentDate, 'yyyy-MM-dd');
        const response = await fetch(`/api/meal-plan/shopping-list?date=${dateStr}`);
        
        if (!response.ok) {
          throw new Error('Failed to fetch shopping list');
        }
        
        const data = await response.json();
        
        if (data.success) {
          setIngredients(data.items || []);
        } else {
          throw new Error(data.error || 'Unknown error');
        }
      } catch (err: any) {
        console.error('Error fetching shopping list:', err);
        // Don't show error for ingredients, just keep previous ones
      } finally {
        setLoadingIngredients(false);
      }
    };
    
    fetchWeeklyIngredients();
  }, [currentDate, plannedMeals]); // Depend on planned meals so ingredients update when plans change

  // Handle week navigation
  const handleWeekChange = (newDate: Date) => {
    setCurrentDate(newDate);
    setWeekStart(startOfWeek(newDate, { weekStartsOn: 1 }));
    setWeekEnd(endOfWeek(newDate, { weekStartsOn: 1 }));
  };

  // Group ingredients by category
  const getGroupedIngredients = () => {
    const groupedIngredients: Record<string, { name: string, amount: string }[]> = {};
    
    ingredients.forEach(item => {
      const category = item.category || 'Other';
      if (!groupedIngredients[category]) {
        groupedIngredients[category] = [];
      }
      
      groupedIngredients[category].push({
        name: item.ingredient,
        amount: `${item.amount} ${item.unit}`
      });
    });
    
    return Object.entries(groupedIngredients).map(([category, items]) => ({
      category,
      items
    }));
  };

  return (
    <div className="relative">
      {loading && (
        <div className="absolute inset-0 bg-white/50 flex items-center justify-center z-10">
          <div className="bg-white p-4 rounded-lg shadow text-gray-700">
            Loading meal plans...
          </div>
        </div>
      )}
      
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
          {error}
        </div>
      )}
      
      <MealPlanningCalendar
        perDayMeals={initialMeals}
        plannedMeals={plannedMeals}
        onWeekChange={handleWeekChange}
        weeklyIngredients={getGroupedIngredients()}
        loadingIngredients={loadingIngredients}
      />
    </div>
  );
} 