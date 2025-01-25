import { deleteMeal } from "@/data/meal";
import { getUserServer } from "@/helper/session";
import { revalidatePath } from "next/cache";
import { NextRequest, NextResponse } from "next/server";

export async function DELETE(_: NextRequest, { params }: { params: Promise<{ id: string }> }) {
	const { id } = await params;
    const user = await getUserServer();

	try {
		await deleteMeal(id, user._id);
	} catch (error) {
		return NextResponse.json({ message: "Failed to delete meal", error }, { status: 400 });
	}

    revalidatePath("/meals");
    revalidatePath("/api/meals");
    revalidatePath('/');

	return NextResponse.json({ message: "Meal deleted successfully" }, { status: 200 });
}