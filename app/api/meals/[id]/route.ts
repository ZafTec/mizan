import { deleteMeal } from "@/data/meal";
import { NextRequest, NextResponse } from "next/server";

export async function DELETE(_: NextRequest, { params }: { params: Promise<{ id: string }> }) {
    const { id } = await params;
    console.log("Deleting meal with id: ", id);

    try {
        await deleteMeal(id);
    } catch (error) {
        return NextResponse.json({ message: "Failed to delete meal", error });
    }

    return NextResponse.json({ message: "Meal deleted successfully" });
}