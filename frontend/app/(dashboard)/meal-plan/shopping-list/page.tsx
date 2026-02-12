'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { clientApi } from '@/lib/api.client';

interface ShoppingListItem {
  id: string;
  ingredientText: string;
  quantity: number;
  unit: string;
  isChecked: boolean;
}

interface ShoppingList {
  id: string;
  name: string;
  createdAt: string;
  items: ShoppingListItem[];
}

export default function ShoppingListPage() {
  const router = useRouter();
  const [shoppingLists, setShoppingLists] = useState<ShoppingList[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCompleted, setShowCompleted] = useState(false);

  useEffect(() => {
    loadShoppingLists();
  }, []);

  const loadShoppingLists = async () => {
    try {
      setLoading(true);
      const data = await clientApi<{ items: ShoppingList[] }>('/api/ShoppingLists');
      setShoppingLists(data.items || []);
    } catch (err) {
      setError('Failed to load shopping lists');
      console.error('Error loading shopping lists:', err);
    } finally {
      setLoading(false);
    }
  };

  const toggleItemChecked = async (itemId: string) => {
    try {
      await clientApi(`/api/ShoppingLists/items/${itemId}/toggle`, {
        method: 'PATCH'
      });

      setShoppingLists(prevLists =>
        prevLists.map(list => ({
          ...list,
          items: list.items.map(item =>
            item.id === itemId ? { ...item, isChecked: !item.isChecked } : item
          )
        }))
      );
    } catch (err) {
      console.error('Error toggling item:', err);
    }
  };

  const allItems = shoppingLists.flatMap(list => list.items);
  const displayedItems = showCompleted
    ? allItems
    : allItems.filter(item => !item.isChecked);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-slate-900">Shopping List</h1>
        <Link href="/meal-plan" className="btn-primary">
          Back to Meal Plan
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

      <div className="card p-6">
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-4">
          <h2 className="text-lg font-semibold text-slate-900">Your Shopping Items</h2>
          <div className="flex flex-col sm:flex-row gap-2">
            <button
              onClick={loadShoppingLists}
              className="btn-secondary flex items-center gap-2"
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

        {loading && allItems.length === 0 ? (
          <div className="flex justify-center items-center py-10">
            <i className="ri-loader-4-line animate-spin text-brand-500 text-3xl"></i>
          </div>
        ) : allItems.length === 0 ? (
          <div className="text-center py-10">
            <div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto mb-4">
              <i className="ri-shopping-cart-line text-3xl text-slate-400" />
            </div>
            <p className="text-slate-600 mb-4">No items in your shopping list</p>
            <p className="text-sm text-slate-500">Add meals to your meal plan to generate a shopping list</p>
            <Link
              href="/meal-plan/add"
              className="mt-4 inline-block btn-primary"
            >
              Add Meals to Plan
            </Link>
          </div>
        ) : (
          <div>
            {displayedItems.length === 0 && (
              <p className="text-slate-500 text-center py-4">All items have been checked off!</p>
            )}

            {displayedItems.length > 0 && (
              <ul className="space-y-2">
                {displayedItems.map((item) => (
                  <li
                    key={item.id}
                    className="flex items-center gap-3 p-2 hover:bg-slate-50 rounded-lg transition"
                  >
                    <input
                      type="checkbox"
                      checked={item.isChecked}
                      onChange={() => toggleItemChecked(item.id)}
                      className="h-5 w-5 cursor-pointer"
                    />
                    <span className={item.isChecked ? "line-through text-slate-400" : "text-slate-900"}>
                      {item.ingredientText}
                      {item.quantity && item.unit && (
                        <span className="text-slate-500 ml-2">
                          - {item.quantity} {item.unit}
                        </span>
                      )}
                    </span>
                  </li>
                ))}
              </ul>
            )}

            <div className="mt-6 border-t border-slate-200 pt-4">
              <div className="flex justify-between items-center">
                <div>
                  <span className="text-sm text-slate-500">
                    {allItems.length} total items
                  </span>
                  <span className="mx-2 text-slate-300">|</span>
                  <span className="text-sm text-slate-500">
                    {allItems.filter(item => item.isChecked).length} checked off
                  </span>
                </div>
                <button
                  onClick={() => window.print()}
                  className="btn-secondary flex items-center gap-2"
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
