import { headers } from "next/headers";
import { auth } from "@/lib/auth";

/**
 * GET /api/auth/token
 * Returns the current user's JWT token for API authentication
 */
export async function GET() {
  try {
    const session = await auth.api.getSession({
      headers: await headers(),
    });

    if (!session?.session) {
      return new Response(JSON.stringify({ error: "Unauthorized" }), {
        status: 401,
        headers: { "Content-Type": "application/json" },
      });
    }

    // The JWT token is in the session token (BetterAuth stores it there)
    const token = session.session.token;

    if (!token) {
      return new Response(JSON.stringify({ error: "No token available" }), {
        status: 401,
        headers: { "Content-Type": "application/json" },
      });
    }

    // Calculate token expiry (BetterAuth tokens typically have 15min TTL)
    const expiresAt = Date.now() + 14 * 60 * 1000; // 14 minutes from now (1min buffer)

    return new Response(
      JSON.stringify({ token, expiresAt }),
      {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }
    );
  } catch (error) {
    console.error("Failed to get API token:", error);
    return new Response(JSON.stringify({ error: "Internal server error" }), {
      status: 500,
      headers: { "Content-Type": "application/json" },
    });
  }
}
