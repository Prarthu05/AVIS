import { NextResponse } from "next/server";
import { sessionCookieOptions } from "@/lib/session";
import { appHubCookieOptions } from "@/lib/appHubSso";

export async function POST() {
  const res = NextResponse.json({ ok: true });
  const { name: cookieName, ...options } = sessionCookieOptions();
  res.cookies.set(cookieName, "", { ...options, maxAge: 0 });

  // Also clears App Hub's own SSO cookie (piggybacked on login, see
  // /api/auth/login) -- otherwise "log out of AVIS" would silently
  // leave the app_hub_id cookie behind to sign back in on the very next
  // page load via middleware's SSO hop.
  const { name: appHubCookieName, ...appHubOptions } = appHubCookieOptions();
  res.cookies.set(appHubCookieName, "", { ...appHubOptions, maxAge: 0 });

  return res;
}
