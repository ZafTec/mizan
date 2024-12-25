
import getUser from "@/context/AuthProvider";
import { getAllIngredient } from "@/data/ingredient";
import { NextResponse } from "next/server";

export async function GET() {

    const {  } = await getUser();

    const ingredients = await getAllIngredient();

	return NextResponse.json({ message: "Get Ingredients.", ingredients });
}
