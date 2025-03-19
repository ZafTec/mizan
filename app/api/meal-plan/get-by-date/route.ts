import { NextRequest, NextResponse } from "next/server";
import MealPlan from "@/model/mealPlan";
import MongoDBClient from "@/mongo/client";
import { getUserServer } from "@/helper/session";
import Recipe from "@/model/recipe";

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

    // Parse the date and create date range for that day
    const date = new Date(dateParam);
    const startOfDay = new Date(date);
    startOfDay.setHours(0, 0, 0, 0);
    
    const endOfDay = new Date(date);
    endOfDay.setHours(23, 59, 59, 999);

    // Connect to MongoDB
    await MongoDBClient();

    // Find meal plan for the specific date
    const mealPlan = await MealPlan.findOne({
      user: user._id,
      date: {
        $gte: startOfDay,
        $lte: endOfDay
      }
    }).populate({
      path: "recipes.recipe",
      model: "Recipe"
    });

    if (!mealPlan) {
      return NextResponse.json({ 
        success: true, 
        mealPlan: null
      });
    }

    // Format the response to include only necessary data
    const formattedMealPlan = {
      _id: mealPlan._id,
      date: mealPlan.date,
      recipes: mealPlan.recipes.map((item: any) => ({
        recipeId: item.recipe._id.toString(),
        recipeName: item.recipe.name,
        servings: item.servings,
        mealTime: item.mealTime
      }))
    };

    return NextResponse.json({ 
      success: true, 
      mealPlan: formattedMealPlan
    });
  } catch (error: any) {
    console.error('Error fetching meal plan:', error);
    return NextResponse.json({ 
      success: false, 
      error: error.message || 'Failed to fetch meal plan'
    }, { status: 500 });
  }
} 