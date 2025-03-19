
import { NextResponse } from "next/server";
import { getAllRecipes } from "@/data/recipe";

export async function GET() {
	const recipes = await getAllRecipes();
	return NextResponse.json({ recipes });
}
