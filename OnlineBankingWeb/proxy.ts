import { NextResponse, type NextRequest } from "next/server";

/**
 * Edge pre-check: no session cookie, no protected page. It cannot tell whether the session is still
 * alive (that would cost an API call per page), so the API stays the real gate and SessionGate is
 * the backstop. /login is not matched, and nothing redirects away from it because a cookie exists:
 * a stale cookie would otherwise bounce between the two.
 */
export function proxy(req: NextRequest) {
  if (!req.cookies.has("obk_session")) {
    const url = new URL("/login", req.url);
    url.searchParams.set("next", req.nextUrl.pathname + req.nextUrl.search);
    return NextResponse.redirect(url);
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/accounts/:path*", "/transfer/:path*", "/transfers/:path*"],
};
