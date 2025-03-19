import { NextRequest, NextResponse } from "next/server";
import { getUserServer } from "@/helper/session";
import { generateShoppingList } from "@/data/mealPlan";

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

    // Parse the date
    const date = new Date(dateParam);
    
    // Generate shopping list for the week
    const shoppingListItems = await generateShoppingList(user._id, date);
    
    return NextResponse.json({ 
      success: true, 
      items: shoppingListItems
    });
  } catch (error: any) {
    console.error('Error generating shopping list:', error);
    return NextResponse.json({ 
      success: false, 
      error: error.message || 'Failed to generate shopping list'
    }, { status: 500 });
  }
} 