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
 * IMPORTANT: This only does optimistic cookie checks, NOT database validation.
 * Actual session validation happens in Server Components via verifySession().
 *
 * Purpose: Fast redirects to prevent loading protected pages without session cookie.
 */
export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Check if route requires authentication (optimistic check)
  const requiresAuth = AUTHENTICATED_ROUTES.some((route) => pathname.startsWith(route));

  if (requiresAuth) {
    // Optimistic check: Does session cookie exist?
    // Note: We don't validate it here - that happens in Server Components
    const hasSessionCookie = request.cookies.has("better-auth.session_token");

    // Not authenticated - redirect to login
    if (!hasSessionCookie) {
      const loginUrl = new URL("/login", request.url);
      loginUrl.searchParams.set("from", pathname);
      return NextResponse.redirect(loginUrl);
    }
  }

  // Note: Admin/trainer role checks happen in Server Components
  // because we can't validate roles without database query
  return NextResponse.next();
}

export const config = {
  matcher: [
    "/admin/:path*",
    "/profile/:path*",
    "/meals/:path*",
    "/recipes/:path*",
    "/ingredients/:path*",
    "/meal-plan/:path*",
    "/goal/:path*",
    "/suggestions/:path*",
    "/trainer/:path*",
  ],
};
