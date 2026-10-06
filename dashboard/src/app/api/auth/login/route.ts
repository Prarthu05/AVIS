import { NextResponse } from "next/server";
import { ensureBootstrapAdmin, normalizeNtid } from "@/lib/auth";
import { createPerson, findPersonByNtid } from "@/lib/peopleStore";
import { newSessionPayload, sessionCookieOptions, sessionSecret, signSession } from "@/lib/session";
import { appHubCookieOptions, signAppHubCookie } from "@/lib/appHubSso";

// The only sign-in path -- mirrors Shift Passdown's own manual-login
// fallback exactly: no password check at all, whoever claims an NTID
// gets treated as that person for lookup purposes. This still doesn't
// gate anything by itself beyond who a resolved NTID is -- the same
// access-control checks apply as everywhere else (a Person needs a role
// assigned before any session is actually issued).
//
// An NTID nobody's ever seen before self-registers rather than hard
// failing: submitted with just {ntid}, the response says
// needsRegistration so the client can ask for email + name, then submits
// again with all three. That second submission creates the Person on the
// spot with role "viewer" (see domain.ts's Role/Person comments) and
// signs them straight in -- an Admin levels them up to a real access
// level from /admin/access whenever they get to it, but nobody is ever
// hard-blocked from at least looking around.
//
// Windows SSO (NTLM) was tried and removed: this app was never added to
// anyone's trusted zone, so real credentials never came through -- see
// git history for the removed implementation if silent SSO is revisited
// later with the actual zone/policy piece in place.
export async function POST(req: Request) {
  const body = (await req.json().catch(() => ({}))) as { ntid?: string; email?: string; name?: string };
  const ntid = normalizeNtid(body.ntid);
  if (!ntid) {
    return NextResponse.json({ error: "Enter a valid NTID" }, { status: 400 });
  }

  // No-op once a real Admin exists -- only ever does something on a
  // freshly-deployed instance where nobody has the admin role yet.
  await ensureBootstrapAdmin();

  let person = findPersonByNtid(ntid);

  if (!person) {
    if (!body.email?.trim() || !body.name?.trim()) {
      return NextResponse.json({ needsRegistration: true }, { status: 404 });
    }
    person = createPerson({ name: body.name.trim().slice(0, 120), email: body.email.trim().slice(0, 200), ntid, roles: ["viewer"] });
  }

  if (!person.roles || person.roles.length === 0) {
    return NextResponse.json({ error: "NTID not recognized, or no access assigned yet" }, { status: 401 });
  }

  const token = await signSession(newSessionPayload(person.id, person.name, person.roles), sessionSecret());
  const res = NextResponse.json({ id: person.id, name: person.name, roles: person.roles });
  const { name: cookieName, ...options } = sessionCookieOptions();
  res.cookies.set(cookieName, token, options);

  // Piggyback App Hub's own SSO cookie on a real AVIS login too --
  // same reasoning as Shift Passdown's identity.js: this only round-trips
  // to App Hub if it's set here, on THIS app's own login response, not by
  // App Hub reaching in. Requires a real NTID, which every login path
  // above already has by this point.
  if (person.ntid) {
    const { name: appHubCookieName, ...appHubOptions } = appHubCookieOptions();
    res.cookies.set(appHubCookieName, await signAppHubCookie(person.ntid), appHubOptions);
  }

  return res;
}
