import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

// Define protected routes that require authentication
const AUTHENTICATED_ROUTES = [
  "/profile",
  "/meals",
  "/recipes",
  "/ingredients",
  "/meal-plan",
  "/goal",
  "/suggestions",
  "/trainer",
  "/admin",
];

/**
 * Proxy for Next.js 16 (replaces middleware.ts)
 *
 */
export function proxy(request: NextRequest) {
  return NextResponse.next();
}

export const config = {
  matcher: []
};
