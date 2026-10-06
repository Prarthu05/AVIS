import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import type { Role } from "./domain";
import { hasAnyRole } from "./accessControl";
import { sessionCookieOptions, sessionSecret, verifySession, type SessionPayload } from "./session";

// Same helpers as DowntimeApp's requireSession.ts.
export async function getSession(): Promise<SessionPayload | null> {
  const cookieStore = await cookies();
  const token = cookieStore.get(sessionCookieOptions().name)?.value;
  return token ? verifySession(token, sessionSecret()) : null;
}

//   const session = await requireRole(["engineer"]);
//   if (session instanceof NextResponse) return session;
export async function requireRole(allowed: Role[]): Promise<SessionPayload | NextResponse> {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  if (!hasAnyRole(session.roles, allowed)) return NextResponse.json({ error: "forbidden" }, { status: 403 });
  return session;
}
