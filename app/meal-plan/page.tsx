import { getUserServer } from "@/helper/session";
import Link from "next/link";
import { getWeeklyMealPlans } from "@/data/mealPlan";
import { getMeal } from "@/data/meal";
import MealPlanningCalendarWrapper from "@/components/MealPlanningCalendarWrapper";

export default async function MealPlanPage() {
  const user = await getUserServer();
  const mealsAggregate = await getMeal(user._id);
  
  try {
    const mealPlans = await getWeeklyMealPlans(user._id);
    
    // Create a completely new object structure to avoid any circular references
    const plannedMeals = mealPlans.map(mealPlan => {
      const sanitizedRecipes = mealPlan.recipes.map(item => {
        return {
          recipe: {
            _id: item.recipe._id.toString(), // Convert MongoDB ID to string
            name: item.recipe.name
          },
          servings: item.servings,
          mealTime: item.mealTime
        };
      });
      
      return {
        _id: mealPlan._id.toString(), // Convert MongoDB ID to string
        date: mealPlan.date,
        recipes: sanitizedRecipes,
        totalCalories: mealPlan.totalMacros?.calories || 0
      };
    });

    return (
      <div className="flex flex-col gap-6">
        <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
          <h1 className="text-2xl sm:text-3xl font-bold">Meal Planning</h1>
          <div className="flex gap-3">
            <Link 
              href={`/meal-plan/add`}
              className="bg-emerald-700 text-white px-4 py-2 rounded-lg text-sm sm:text-base"
            >
              Add to Meal Plan
            </Link>
            <Link 
              href={`/meal-plan/shopping-list`}
              className="bg-emerald-100 text-emerald-700 px-4 py-2 rounded-lg text-sm sm:text-base"
            >
              Shopping List
            </Link>
          </div>
        </div>

        <div className="w-full">
          <MealPlanningCalendarWrapper 
            initialMeals={mealsAggregate} 
            initialPlannedMeals={plannedMeals}
          />
        </div>
      </div>
    );
  } catch (error) {
    console.error("Error loading meal plans:", error);
    
    // Return a UI with error state
    return (
      <div className="flex flex-col gap-6">
        <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
          <h1 className="text-2xl sm:text-3xl font-bold">Meal Planning</h1>
          <div className="flex gap-3">
            <Link 
              href="/meal-plan/add"
              className="bg-emerald-700 text-white px-4 py-2 rounded-lg text-sm sm:text-base"
            >
              Add to Meal Plan
            </Link>
          </div>
        </div>
        
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
          <p>An error occurred loading your meal plans. Please try again later.</p>
        </div>
      </div>
    );
  }
}
