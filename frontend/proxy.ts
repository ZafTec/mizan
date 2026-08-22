import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const PROTECTED_PREFIXES = [
  "/profile",
  "/meals",
  "/meal-plan",
  "/goal",
  "/suggestions",
  "/trainer",
  "/trainers",
  "/admin",
  "/ingredients",
  "/body-measurements",
  "/workouts",
  "/exercises",
  "/achievements",
];

const PROTECTED_PATTERNS = [
  /^\/recipes\/add/,
  /^\/recipes\/[^/]+\/edit/,
];

const SESSION_COOKIE = "mizan_session";

function isProtectedRoute(pathname: string): boolean {
  if (PROTECTED_PREFIXES.some((prefix) => pathname.startsWith(prefix))) {
    return true;
  }
  return PROTECTED_PATTERNS.some((pattern) => pattern.test(pathname));
}

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  if (!isProtectedRoute(pathname)) {
    return NextResponse.next();
  }

  // Presence only. Whether the session is still valid is the API's call, and
  // this runs on every request - see docs/REFOCUS.md §6.
  const sessionCookie = request.cookies.get(SESSION_COOKIE);

  if (!sessionCookie?.value) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("redirect", pathname);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/((?!api|_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt).*)",
  ],
};
