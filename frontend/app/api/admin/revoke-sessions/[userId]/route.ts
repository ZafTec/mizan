import { auth } from "@/lib/auth";
import { db } from "@/db/client";
import { sessions } from "@/db/schema";
import { eq } from "drizzle-orm";

export async function POST(
  request: Request,
  { params }: { params: Promise<{ userId: string }> }
) {
  const session = await auth.api.getSession({ headers: request.headers });

  if (!session?.user) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }

  if (session.user.role !== "admin") {
    return Response.json({ error: "Forbidden" }, { status: 403 });
  }

  try {
    const { userId } = await params;

    // Validate UUID format
    const uuidRegex =
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!uuidRegex.test(userId)) {
      return Response.json(
        { error: "Invalid user ID format" },
        { status: 400 }
      );
    }

    await db.delete(sessions).where(eq(sessions.userId, userId));

    return Response.json({
      success: true,
      message: "All sessions revoked",
    });
  } catch (error) {
    console.error("Failed to revoke sessions");
    return Response.json(
      { error: "Failed to revoke sessions" },
      { status: 500 }
    );
  }
}
