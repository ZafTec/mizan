import { NextRequest, NextResponse } from "next/server";

export async function POST(request: NextRequest) {
    if (process.env.NODE_ENV === "production") {
        return NextResponse.json({ error: "Not available" }, { status: 404 });
    }

    const { email, role } = await request.json();

    if (!email) {
        return NextResponse.json({ error: "email is required" }, { status: 400 });
    }

    const { db } = await import("@/db/client");
    const { users } = await import("@/db/schema");
    const { eq } = await import("drizzle-orm");

    await db
        .update(users)
        .set({ emailVerified: true, ...(role ? { role } : {}) })
        .where(eq(users.email, email));

    return NextResponse.json({ success: true });
}
