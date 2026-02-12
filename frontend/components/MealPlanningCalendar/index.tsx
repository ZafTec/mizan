'use client';

import { useState, useEffect } from 'react';
import { toast } from "sonner";
import { PerDayMealsAggregate } from '@/types/meal';
import { format, addDays, startOfWeek, endOfWeek, addWeeks, subWeeks, isToday, isWithinInterval } from 'date-fns';
import { twMerge } from 'tailwind-merge';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { deleteMealPlan } from '@/data/mealPlan';
import { ID } from '@/types/id';

type PlannedMealRecipe = {
	recipe: {
		_id: ID;
		name: string;
		[key: string]: any; // Allow any additional properties
	};
	servings: number;
	mealTime: string;
}

interface MealPlanningCalendarProps {
	perDayMeals: PerDayMealsAggregate[];
	plannedMeals?: {
		_id: any; // Make _id more flexible
		date: Date;
		recipes: PlannedMealRecipe[];
		totalCalories: number;
	}[];
	onWeekChange?: (newDate: Date) => void;
	weeklyIngredients?: {
		category: string;
		items: {
			name: string;
			amount: string;
		}[];
	}[];
	loadingIngredients?: boolean;
}

export default function MealPlanningCalendar({ 
	perDayMeals, 
	plannedMeals = [], 
	onWeekChange,
	weeklyIngredients = [],
	loadingIngredients = false
}: MealPlanningCalendarProps) {
	const router = useRouter();
	const [currentDate, setCurrentDate] = useState(new Date());
	const [weekStart, setWeekStart] = useState(startOfWeek(currentDate, { weekStartsOn: 1 }));
	const [weekEnd, setWeekEnd] = useState(endOfWeek(currentDate, { weekStartsOn: 1 }));
	const [weekDays, setWeekDays] = useState<Date[]>([]);
	const [deleting, setDeleting] = useState(false);

	// Generate days for the week
	useEffect(() => {
		const days = [];
		for (let i = 0; i < 7; i++) {
			days.push(addDays(weekStart, i));
		}
		setWeekDays(days);
	}, [weekStart]);

	// Navigate to next/previous week
	const navigateWeek = (direction: 'next' | 'prev') => {
		const newDate = direction === 'next' 
			? addWeeks(currentDate, 1) 
			: subWeeks(currentDate, 1);
		
		setCurrentDate(newDate);
		setWeekStart(startOfWeek(newDate, { weekStartsOn: 1 }));
		setWeekEnd(endOfWeek(newDate, { weekStartsOn: 1 }));
		
		// Notify parent component about week change if callback is provided
		if (onWeekChange) {
			onWeekChange(newDate);
		}
	};

	// Get meals for a specific day
	const getMealsForDay = (day: Date) => {
		const dayStr = format(day, 'yyyy-MM-dd');
		return perDayMeals.find(meal => format(new Date(meal.date), 'yyyy-MM-dd') === dayStr);
	};

	// Get planned meals for a specific day
	const getPlannedMealsForDay = (day: Date) => {
		const dayStr = format(day, 'yyyy-MM-dd');
		return plannedMeals.find(meal => format(new Date(meal.date), 'yyyy-MM-dd') === dayStr);
	};

	// Handle meal plan deletion
	const handleDeleteMealPlan = async (mealPlanId: string) => {
		if (!mealPlanId) return;
		
		if (confirm('Are you sure you want to delete this meal plan?')) {
			try {
				setDeleting(true);
				await deleteMealPlan(mealPlanId);
				router.refresh();
			} catch (error) {
				console.error('Failed to delete meal plan:', error);
				toast.error('Failed to delete meal plan');
			} finally {
				setDeleting(false);
			}
		}
	};

	// Check if date is current week
	const isCurrentWeek = isWithinInterval(new Date(), {
		start: weekStart,
		end: weekEnd
	});

	return (
		<div className="bg-white rounded-lg shadow p-4 w-full">
			<div className="flex justify-between items-center mb-4">
				<h2 className="text-xl font-bold">Meal Planning Calendar</h2>
				<div className="flex items-center space-x-2">
					<button 
						onClick={() => navigateWeek('prev')}
						className="p-2 rounded-full hover:bg-gray-100"
						disabled={deleting}
					>
						<i className="ri-arrow-left-s-line"></i>
					</button>
					<span className="font-medium">
						{format(weekStart, 'MMM d')} - {format(weekEnd, 'MMM d, yyyy')}
					</span>
					<button 
						onClick={() => navigateWeek('next')}
						className="p-2 rounded-full hover:bg-gray-100"
						disabled={deleting}
					>
						<i className="ri-arrow-right-s-line"></i>
					</button>
					{!isCurrentWeek && (
						<button 
							onClick={() => {
								const today = new Date();
								setCurrentDate(today);
								setWeekStart(startOfWeek(today, { weekStartsOn: 1 }));
								setWeekEnd(endOfWeek(today, { weekStartsOn: 1 }));
								// Notify parent component about week change
								if (onWeekChange) {
									onWeekChange(today);
								}
							}}
							className="ml-2 text-sm bg-emerald-100 text-emerald-700 px-2 py-1 rounded"
							disabled={deleting}
						>
							Today
						</button>
					)}
				</div>
			</div>

			{/* Calendar grid */}
			<div className="grid grid-cols-7 gap-4">
				{/* Day headers */}
				{weekDays.map((day) => (
					<div 
						key={format(day, 'yyyy-MM-dd')} 
						className="text-center"
					>
						<div className="text-sm font-medium">{format(day, 'EEE')}</div>
						<div className={twMerge(
							"w-10 h-10 rounded-full mx-auto flex items-center justify-center",
							isToday(day) ? "bg-emerald-600 text-white" : ""
						)}>
							{format(day, 'd')}
						</div>
					</div>
				))}

				{/* Meal cells */}
				{weekDays.map((day) => {
					const dayMeals = getMealsForDay(day);
					const plannedMealsForDay = getPlannedMealsForDay(day);
					
					return (
						<div 
							key={`meals-${format(day, 'yyyy-MM-dd')}`} 
							className="border rounded-lg p-2 min-h-[200px]"
						>
							<div className="flex flex-col h-full">
								{/* Actual meals */}
								{dayMeals && dayMeals.meals.length > 0 ? (
									<div className="text-sm text-gray-600 mb-2">
										<div className="flex justify-between">
											<span>Logged meals: {dayMeals.meals.length}</span>
											<span>{Math.round(dayMeals.totalMacros.calories)} cal</span>
										</div>
										<div className="max-h-24 overflow-y-auto mt-1">
											{dayMeals.meals.map((meal) => (
												<div key={meal._id.toString()} className="text-xs bg-gray-50 p-1 rounded mb-1">
													{meal.name}
												</div>
											))}
										</div>
									</div>
								) : (
									<div className="text-sm text-gray-400 mb-2">No meals logged</div>
								)}

								{/* Planned meals */}
								{plannedMealsForDay && plannedMealsForDay.recipes.length > 0 ? (
								<div className="text-sm text-gray-600 mt-auto">
									<div className="flex justify-between">
										<span>Planned: {plannedMealsForDay.recipes.length}</span>
										<span>{Math.round(plannedMealsForDay.totalCalories)} cal</span>
									</div>
									<div className="max-h-24 overflow-y-auto mt-1">
										{plannedMealsForDay.recipes.map((recipe, idx) => (
											<div key={`${recipe.recipe._id.toString()}-${idx}`} className="text-xs bg-emerald-50 p-1 rounded mb-1 flex justify-between items-center">
												<span>{recipe.recipe.name}</span>
											</div>
										))}
									</div>
									<div className="mt-2 flex justify-between">
										<Link 
											href={`/meal-plan/add?date=${format(day, 'yyyy-MM-dd')}`}
											className="text-emerald-600 hover:underline text-xs"
										>
											+ Add more/Edit
										</Link>
										{plannedMealsForDay._id && (
											<button
												onClick={() => handleDeleteMealPlan(plannedMealsForDay._id!.toString())}
												className="text-red-500 hover:text-red-700 text-xs"
												disabled={deleting}
											>
												Delete
											</button>
										)}
									</div>
								</div>
								) : (
									<div className="text-sm text-gray-400 mt-auto">
										<Link 
											href={`/meal-plan/add?date=${format(day, 'yyyy-MM-dd')}`}
											className="text-emerald-600 hover:underline"
										>
											+ Plan meal
										</Link>
									</div>
								)}
							</div>
						</div>
					);
				})}
			</div>

			{/* Shopping list section */}
			<div className="mt-6 border-t pt-4">
				<div className="flex justify-between items-center mb-3">
					<h3 className="font-bold">Weekly Ingredients</h3>
					<Link 
						href={`/meal-plan/shopping-list?week=${format(weekStart, 'yyyy-MM-dd')}`}
						className="text-sm bg-emerald-700 text-white px-3 py-1 rounded-lg"
					>
						View Shopping List
					</Link>
				</div>
				<div className="text-sm text-gray-600">
					<p>Based on your meal plan for this week, you&apos;ll need:</p>
					{loadingIngredients ? (
						<div className="mt-2 text-gray-500">Loading ingredients...</div>
					) : weeklyIngredients.length > 0 ? (
						<div className="mt-2 grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2">
							{weeklyIngredients.map(category => (
								<div key={category.category} className="bg-gray-50 rounded p-2">
									<div className="font-medium">{category.category}</div>
									<ul className="list-disc list-inside text-xs">
										{category.items.map((item, index) => (
											<li key={index}>{item.name} - {item.amount}</li>
										))}
									</ul>
								</div>
							))}
						</div>
					) : (
						<div className="mt-2 text-gray-500">No ingredients needed for this week.</div>
					)}
				</div>
			</div>
		</div>
	);
}
