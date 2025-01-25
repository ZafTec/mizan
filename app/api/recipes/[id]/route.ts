
import { getUserServer } from "@/helper/session";
import { revalidatePath } from "next/cache";
import { NextRequest, NextResponse } from "next/server";

export async function DELETE(_: NextRequest, { params }: { params: Promise<{ id: string }> }) {
    const { id } = await params;
    const user = await getUserServer();

    try {
        // await deleteMeal(id, user._id);
    } catch (error) {
        return NextResponse.json({ message: "Failed to delete recipe", error }, { status: 400 });
    }

    revalidatePath("/recipes");
    revalidatePath('/');

    return NextResponse.json({ message: "Recipe deleted successfully" }, { status: 200 });
}