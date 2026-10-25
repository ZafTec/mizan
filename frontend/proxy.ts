import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const PROTECTED_PREFIXES = [
  "/today",
  "/history",
  "/progress",
  "/more",
  "/workout/active",
  "/profile",
  "/meals",
  "/meal-plan",
  "/shopping-lists",
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
  "/oauth",
];

const PROTECTED_PATTERNS = [/^\/recipes\/add/, /^\/recipes\/[^/]+\/edit/];

const SESSION_COOKIE = "mizan_session";

// The Telegram Mini App and its session exchange. A /tg page opens signed out
// and signs itself in from Telegram's initData (docs/TELEGRAM.md#mini-app), so
// it is never sent to /login, whatever the prefixes above come to include.
const SELF_SIGNED_PREFIXES = ["/tg", "/telegram"];

function isSelfSignedRoute(pathname: string): boolean {
  return SELF_SIGNED_PREFIXES.some(
    (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
  );
}

function isProtectedRoute(pathname: string): boolean {
  if (isSelfSignedRoute(pathname)) return false;
  if (PROTECTED_PREFIXES.some((prefix) => pathname.startsWith(prefix))) {
    return true;
  }
  return PROTECTED_PATTERNS.some((pattern) => pattern.test(pathname));
}

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  if (!isProtectedRoute(pathname)) {
    return NextResponse.next();
  }

  // Presence only. Whether the session is still valid is the API's call, and
  // this runs on every request - see docs/ARCHITECTURE.md#identity.
  const sessionCookie = request.cookies.get(SESSION_COOKIE);

  if (!sessionCookie?.value) {
    const loginUrl = new URL("/login", request.url);
    // The login page reads callbackUrl. The query string comes along so a connection
    // request survives signing in.
    loginUrl.searchParams.set("callbackUrl", pathname + search);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/((?!api|_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt).*)",
  ],
};
