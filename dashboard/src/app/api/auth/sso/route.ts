import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { ensureBootstrapAdmin, normalizeNtid } from "@/lib/auth";
import { createPerson, findPersonByNtid } from "@/lib/peopleStore";
import { newSessionPayload, sessionCookieOptions, sessionSecret, signSession } from "@/lib/session";
import { APP_HUB_COOKIE_NAME, verifyAppHubCookie } from "@/lib/appHubSso";

const APP_HUB_URL = process.env.APP_HUB_URL || "http://10.77.193.155:4001";

// middleware.ts sends an unauthenticated PAGE request here instead of
// straight to /login whenever an app_hub_id cookie is present (a cheap,
// Edge-safe existence check -- the real HMAC verification only happens
// here, in a Node route, since it needs peopleStore's fs access either
// way). Falls back to /login on absolutely any failure -- an expired/
// forged cookie, App Hub unreachable, a resolved username with no
// AVIS access yet -- so a broken SSO hop never strands anyone
// worse than "log in manually," same as if this route didn't exist.
export async function GET(req: Request) {
  const url = new URL(req.url);
  // NOT url.origin: under `next start` with no reverse proxy in front,
  // a bare Route Handler's own `req.url` reports Next's internal bind
  // address (localhost:PORT) as its origin regardless of the Host header
  // the browser actually sent -- confirmed directly (curl against the
  // real LAN IP still got back a `Location: http://localhost:3211/...`).
  // Only Route Handlers have this problem; middleware.ts's own redirects
  // (NextRequest, Edge runtime) reliably reflect the real Host already.
  // The incoming Host header is the one thing that's actually trustworthy
  // here -- this app is plain HTTP only on the LAN (see session.ts).
  const origin = `http://${req.headers.get("host") ?? url.host}`;
  const next = url.searchParams.get("next") || "/";
  // Only ever redirect back into this same app -- an open `next` param
  // would let a crafted link bounce a real session token at an arbitrary
  // external origin.
  const safeNext = next.startsWith("/") && !next.startsWith("//") ? next : "/";
  const loginUrl = new URL(`/login?next=${encodeURIComponent(safeNext)}`, origin);

  const cookieStore = await cookies();
  const raw = cookieStore.get(APP_HUB_COOKIE_NAME)?.value;
  if (!raw) return NextResponse.redirect(loginUrl);

  const username = normalizeNtid(await verifyAppHubCookie(raw));
  if (!username) return NextResponse.redirect(loginUrl);

  await ensureBootstrapAdmin();

  let person = findPersonByNtid(username);
  if (!person) {
    // Auto-provisioned the same way Shift Passdown's own silent-NTLM path
    // does -- a real name/email if App Hub's whoami has one for this
    // username, otherwise the bare username stands in for both (the same
    // fallback App Hub's own dormant records already use). Still gets
    // only "viewer" by default -- an Admin still has to level them up for
    // any real access, same as the manual self-registration path in
    // /api/auth/login.
    let name = username;
    let email = "";
    try {
      const whoami = await fetch(`${APP_HUB_URL}/api/whoami`, { headers: { Cookie: `${APP_HUB_COOKIE_NAME}=${raw}` } });
      if (whoami.ok) {
        const data = (await whoami.json()) as { person?: { name?: string; email?: string } };
        if (data.person?.name) name = data.person.name;
        if (data.person?.email) email = data.person.email;
      }
    } catch {
      // App Hub unreachable -- fall through with the username-only fallback
    }
    person = createPerson({ name, email, ntid: username, roles: ["viewer"] });
  }

  if (!person.roles || person.roles.length === 0) return NextResponse.redirect(loginUrl);

  const token = await signSession(newSessionPayload(person.id, person.name, person.roles), sessionSecret());
  const res = NextResponse.redirect(new URL(safeNext, origin));
  const { name: cookieName, ...options } = sessionCookieOptions();
  res.cookies.set(cookieName, token, options);
  return res;
}
