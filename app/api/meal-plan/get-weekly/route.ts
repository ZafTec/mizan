import { NextRequest, NextResponse } from "next/server";
import MealPlan from "@/model/mealPlan";
import MongoDBClient from "@/mongo/client";
import { getUserServer } from "@/helper/session";
import { startOfWeek, endOfWeek } from "date-fns";

export async function GET(request: NextRequest) {
  try {
    const user = await getUserServer();
    const searchParams = request.nextUrl.searchParams;
    const dateParam = searchParams.get('date');
    
    if (!dateParam) {
      return NextResponse.json({ 
        success: false, 
        error: 'Date parameter is required'
      }, { status: 400 });
    }

    // Parse the date and get the week start/end
    const date = new Date(dateParam);
    const weekStart = startOfWeek(date, { weekStartsOn: 1 });
    const weekEnd = endOfWeek(date, { weekStartsOn: 1 });
    
    // Connect to MongoDB
    await MongoDBClient();

    // Find meal plans for the specific week
    const mealPlans = await MealPlan.find({
      user: user._id,
      date: {
        $gte: weekStart,
        $lte: weekEnd
      }
    }).populate({
      path: "recipes.recipe",
      model: "Recipe"
    });

    // Format the response to include only necessary data
    const formattedMealPlans = mealPlans.map(mealPlan => ({
      _id: mealPlan._id.toString(),
      date: mealPlan.date,
      recipes: mealPlan.recipes.map((item: any) => ({
        recipe: {
          _id: item.recipe._id.toString(),
          name: item.recipe.name || "Unnamed Recipe"
        },
        servings: item.servings || 1,
        mealTime: item.mealTime || "lunch"
      })),
      totalCalories: mealPlan.totalMacros?.calories || 0
    }));

    return NextResponse.json({ 
      success: true, 
      mealPlans: formattedMealPlans
    });
  } catch (error: any) {
    console.error('Error fetching weekly meal plans:', error);
    return NextResponse.json({ 
      success: false, 
      error: error.message || 'Failed to fetch weekly meal plans'
    }, { status: 500 });
  }
} 