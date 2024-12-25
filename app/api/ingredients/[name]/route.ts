import { getIngredientByName } from "@/data/ingredient";
import { NextRequest, NextResponse } from "next/server";

export async function GET(_: NextRequest, { params }: { params: Promise<{ name: string }> }) {
	const { name } = await params;

	const ingredients = await getIngredientByName(name);
	return NextResponse.json({ message: "Get Ingredients.", ingredients });
}
