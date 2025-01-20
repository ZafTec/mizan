
import { getTodayMeal } from "@/data/meal";
import { NextResponse } from "next/server";

export async function GET() {
    const meals = await getTodayMeal();
    return NextResponse.json({ meals });
}