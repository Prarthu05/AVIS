import { NextResponse, type NextRequest } from "next/server";
import { pageAccessAllowed, sensitiveApiAllowed } from "@/lib/accessControl";
import { newSessionPayload, sessionCookieOptions, sessionSecret, signSession, verifySession } from "@/lib/session";
import { APP_HUB_COOKIE_NAME } from "@/lib/appHubSso";

// First middleware in the project -- runs on the Edge runtime, so this
// file (and everything it imports) must stay free of Node-only APIs. That
// is exactly why password hashing (auth.ts, Node crypto.scrypt) is kept
// out of the session verification path (session.ts, Web Crypto only).
export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon\\.ico|jabil-logo.*\\.png).*)"],
};

// /api/station/ is called by the AVIS middleware on each station PC, which
// has no browser session - every route under it MUST authenticate itself
// with the shared station key (see lib/stationAuth.ts).
const OPEN_PREFIXES = ["/login", "/api/auth/", "/api/station/"];

function isOpen(pathname: string): boolean {
  return OPEN_PREFIXES.some((p) => pathname === p || pathname.startsWith(p));
}

export async function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;
  if (isOpen(pathname)) return NextResponse.next();

  const isApi = pathname.startsWith("/api/");
  const cookieName = sessionCookieOptions().name;
  const token = req.cookies.get(cookieName)?.value;
  const secret = sessionSecret();
  const session = token ? await verifySession(token, secret) : null;

  if (!session) {
    if (isApi) return NextResponse.json({ error: "not logged in" }, { status: 401 });
    // A page request with no session of our own, but a cookie App Hub (or
    // a sibling app) already set -- give /api/auth/sso a chance to
    // establish a real session from it before falling back to manual
    // login. Just an existence check here (Edge-safe, no crypto): the SSO
    // route does the actual HMAC verification, in Node, and redirects
    // back to /login itself on any failure, so a forged/expired cookie
    // never grants anything here.
    if (req.cookies.get(APP_HUB_COOKIE_NAME)?.value) {
      const ssoUrl = new URL("/api/auth/sso", req.url);
      ssoUrl.searchParams.set("next", pathname);
      return NextResponse.redirect(ssoUrl);
    }
    const loginUrl = new URL("/login", req.url);
    loginUrl.searchParams.set("next", pathname);
    return NextResponse.redirect(loginUrl);
  }

  const allowed = isApi ? sensitiveApiAllowed(pathname, session.roles) : pageAccessAllowed(pathname, session.roles);
  if (!allowed) {
    if (isApi) return NextResponse.json({ error: "forbidden" }, { status: 403 });
    return NextResponse.redirect(new URL("/", req.url));
  }

  // Sliding expiry -- reissue a refreshed cookie on every allowed request
  // so an active user's session doesn't expire mid-shift.
  const res = NextResponse.next();
  const refreshed = await signSession(newSessionPayload(session.personId, session.name, session.roles), secret);
  const { name, ...options } = sessionCookieOptions();
  res.cookies.set(name, refreshed, options);
  return res;
}
