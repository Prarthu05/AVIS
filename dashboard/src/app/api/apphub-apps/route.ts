import { NextResponse } from "next/server";

const APP_HUB_URL = process.env.APP_HUB_URL || "http://10.77.193.155:4001";

// Backs AppHubSwitcher.tsx. A same-origin proxy rather than the browser
// calling App Hub directly, for the exact reason Shift Passdown's own
// copy of this route does it this way: App Hub's own SSO cookie (see
// appHubSso.ts) only round-trips correctly if the browser sends it
// straight to App Hub's own origin -- a server-to-server proxy forwards
// it instead, so App Hub never needs any CORS/credentials configuration
// for this app's origin. Fails open to an empty list (never a 500) so a
// down or unreachable App Hub never breaks AVIS's own UI, just
// quietly hides the switcher's contents.
export async function GET(req: Request) {
  try {
    const cookie = req.headers.get("cookie");
    const upstream = await fetch(`${APP_HUB_URL}/api/my-apps`, { headers: cookie ? { Cookie: cookie } : {} });
    const data = await upstream.json();
    return NextResponse.json(data);
  } catch {
    return NextResponse.json({ apps: [], lockedApps: [], isSuperAdmin: false });
  }
}
