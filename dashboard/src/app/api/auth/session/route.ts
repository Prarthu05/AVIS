import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { sessionCookieOptions, sessionSecret, verifySession } from "@/lib/session";

// How client components (which can't call next/headers directly) learn
// who's logged in -- used by client components such as
// the VA editor and fault feed.
export async function GET() {
  const cookieStore = await cookies();
  const token = cookieStore.get(sessionCookieOptions().name)?.value;
  const session = token ? await verifySession(token, sessionSecret()) : null;
  if (!session) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  return NextResponse.json({ id: session.personId, name: session.name, roles: session.roles });
}
