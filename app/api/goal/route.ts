

import { getCurrentGoal } from "@/data/goal";
import { getUserServer } from "@/helper/session";
import { NextRequest, NextResponse } from "next/server";


export async function GET(request: NextRequest) {
    try {
        const user = await getUserServer();
        
        const goal = await getCurrentGoal(user._id);

        if (!goal) {
            return NextResponse.json({ error: "Goal not found" }, { status: 404 });
        }

        return NextResponse.json({ user, goal }, { status: 200 });
    } catch (error: any) {
        return NextResponse.json({ serverError: error.message, error: error.message }, { status: 400 })
    }
}
