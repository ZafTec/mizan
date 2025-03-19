'use client';

import { useState, useEffect } from 'react';
import { ShoppingListItem } from '@/types/mealPlan';
import { format, startOfWeek, endOfWeek, addWeeks, subWeeks, parse } from 'date-fns';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

export default function ShoppingListPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const weekParam = searchParams.get('week');
  
  // Initialize date from URL or use current date
  const initialDate = weekParam 
    ? parse(weekParam, 'yyyy-MM-dd', new Date())
    : new Date();
    
  const [items, setItems] = useState<ShoppingListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [currentDate, setCurrentDate] = useState<Date>(initialDate);
  const [startDate, setStartDate] = useState<Date>(startOfWeek(initialDate, { weekStartsOn: 1 }));
  const [endDate, setEndDate] = useState<Date>(endOfWeek(initialDate, { weekStartsOn: 1 }));
  const [showCompleted, setShowCompleted] = useState(false);
  const [completedItems, setCompletedItems] = useState<Set<string>>(new Set());

  // Update URL when week changes
  useEffect(() => {
    // Skip initial render if no week param and we're on current week
    if (!weekParam && format(startDate, 'yyyy-MM-dd') === format(startOfWeek(new Date(), { weekStartsOn: 1 }), 'yyyy-MM-dd')) {
      return;
    }
    
    // Create a new URLSearchParams object
    const params = new URLSearchParams(searchParams.toString());
    params.set('week', format(currentDate, 'yyyy-MM-dd'));
    
    // Update URL without refreshing the page
    router.push(`/meal-plan/shopping-list?${params.toString()}`, { scroll: false });
  }, [currentDate, startDate, router, searchParams, weekParam]);
  
  useEffect(() => {
    loadShoppingList();
  }, [currentDate]);

  useEffect(() => {
    setStartDate(startOfWeek(currentDate, { weekStartsOn: 1 }));
    setEndDate(endOfWeek(currentDate, { weekStartsOn: 1 }));
  }, [currentDate]);

  const loadShoppingList = async () => {
    try {
      setLoading(true);
      const dateParam = format(currentDate, 'yyyy-MM-dd');
      const res = await fetch(`/api/meal-plan/shopping-list?date=${dateParam}`);
      
      if (!res.ok) {
        throw new Error('Failed to load shopping list');
      }
      
      const data = await res.json();
      setItems(data.items || []);
      setLoading(false);
    } catch (err) {
      setError('Failed to load shopping list');
      setLoading(false);
    }
  };

  const navigateWeek = (direction: 'next' | 'prev') => {
    const newDate = direction === 'next' 
      ? addWeeks(currentDate, 1) 
      : subWeeks(currentDate, 1);
    
    setCurrentDate(newDate);
  };

  const toggleItemCompletion = (item: ShoppingListItem) => {
    const itemKey = `${item.ingredient}-${item.unit}`;
    const newCompleted = new Set(completedItems);
    
    if (newCompleted.has(itemKey)) {
      newCompleted.delete(itemKey);
    } else {
      newCompleted.add(itemKey);
    }
    
    setCompletedItems(newCompleted);
  };

  // Group items by category
  const groupedItems = items.reduce((groups, item) => {
    const category = item.category || 'Other';
    if (!groups[category]) {
      groups[category] = [];
    }
    groups[category].push(item);
    return groups;
  }, {} as Record<string, ShoppingListItem[]>);

  // Filter out completed items if needed
  const filteredGroupedItems = Object.entries(groupedItems).reduce((acc, [category, categoryItems]) => {
    const filteredItems = showCompleted 
      ? categoryItems 
      : categoryItems.filter(item => !completedItems.has(`${item.ingredient}-${item.unit}`));
    
    if (filteredItems.length > 0) {
      acc[category] = filteredItems;
    }
    return acc;
  }, {} as Record<string, ShoppingListItem[]>);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">Shopping List</h1>
        <Link 
          href={`/meal-plan?week=${format(currentDate, 'yyyy-MM-dd')}`}
          className="bg-emerald-700 text-white px-4 py-2 rounded-lg"
        >
          Back to Meal Plan
        </Link>
      </div>

      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
          {error}
        </div>
      )}

      <div className="bg-white p-4 rounded-lg shadow">
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-4">
          <div className="flex items-center gap-2">
            <button 
              onClick={() => navigateWeek('prev')}
              className="p-2 rounded-full hover:bg-gray-100"
              disabled={loading}
            >
              <i className="ri-arrow-left-s-line"></i>
            </button>
            <h2 className="text-lg font-semibold">
              Week of {format(startDate, 'MMM d')} - {format(endDate, 'MMM d, yyyy')}
            </h2>
            <button 
              onClick={() => navigateWeek('next')}
              className="p-2 rounded-full hover:bg-gray-100"
              disabled={loading}
            >
              <i className="ri-arrow-right-s-line"></i>
            </button>
          </div>
          <div className="flex flex-col sm:flex-row gap-2">
            <button
              onClick={loadShoppingList}
              className="bg-emerald-100 text-emerald-700 px-3 py-1 rounded text-sm flex items-center gap-1"
              disabled={loading}
            >
              {loading ? (
                <>
                  <i className="ri-loader-4-line animate-spin"></i>
                  Loading...
                </>
              ) : (
                <>
                  <i className="ri-refresh-line"></i>
                  Refresh List
                </>
              )}
            </button>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={showCompleted}
                onChange={() => setShowCompleted(!showCompleted)}
                className="h-4 w-4"
              />
              Show completed items
            </label>
          </div>
        </div>

        {loading && items.length === 0 ? (
          <div className="flex justify-center items-center py-10">
            <i className="ri-loader-4-line animate-spin text-emerald-700 text-3xl"></i>
          </div>
        ) : items.length === 0 ? (
          <div className="text-center py-10">
            <p className="text-gray-600 mb-4">No items in your shopping list</p>
            <p className="text-sm text-gray-500">Add meals to your meal plan to generate a shopping list</p>
            <Link
              href="/meal-plan/add"
              className="mt-4 inline-block bg-emerald-700 text-white px-4 py-2 rounded-lg"
            >
              Add Meals to Plan
            </Link>
          </div>
        ) : (
          <div>
            {Object.keys(filteredGroupedItems).length === 0 && (
              <p className="text-gray-500 text-center py-4">All items have been checked off!</p>
            )}
            
            {Object.entries(filteredGroupedItems).map(([category, categoryItems]) => (
              <div key={category} className="mb-6">
                <h3 className="font-semibold text-emerald-800 mb-2">{category}</h3>
                <ul className="space-y-2">
                  {categoryItems.map((item, index) => {
                    const itemKey = `${item.ingredient}-${item.unit}`;
                    const isCompleted = completedItems.has(itemKey);
                    
                    return (
                      <li 
                        key={`${item.ingredient}-${index}`}
                        className="flex items-center gap-2"
                      >
                        <input
                          type="checkbox"
                          checked={isCompleted}
                          onChange={() => toggleItemCompletion(item)}
                          className="h-5 w-5"
                        />
                        <span className={isCompleted ? "line-through text-gray-400" : ""}>
                          {item.ingredient} - {item.amount.toFixed(1)} {item.unit}
                        </span>
                      </li>
                    );
                  })}
                </ul>
              </div>
            ))}

            <div className="mt-6 border-t pt-4">
              <div className="flex justify-between items-center">
                <div>
                  <span className="text-sm text-gray-500">
                    {items.length} total items
                  </span>
                  <span className="mx-2 text-gray-300">|</span>
                  <span className="text-sm text-gray-500">
                    {Array.from(completedItems).length} checked off
                  </span>
                </div>
                <button 
                  onClick={() => window.print()}
                  className="bg-gray-100 text-gray-700 px-3 py-1 rounded text-sm flex items-center gap-1"
                >
                  <i className="ri-printer-line"></i>
                  Print List
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
