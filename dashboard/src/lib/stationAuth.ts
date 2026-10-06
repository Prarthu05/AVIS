import { timingSafeEqual } from "crypto";
import { NextResponse } from "next/server";

// Station-to-dashboard calls (AVIS middleware on each station PC) carry no
// browser session - they authenticate with a shared key, the same
// service-to-service pattern DowntimeApp uses for /api/command-center/.
// Every route under /api/station/ MUST call this first.
export const STATION_KEY_HEADER = "x-avis-station-key";
export const STATION_NAME_HEADER = "x-avis-station";

export function requireStation(req: Request): { station: string } | NextResponse {
  const expected = process.env.AVIS_STATION_KEY;
  if (!expected) return NextResponse.json({ error: "AVIS_STATION_KEY is not configured on the dashboard" }, { status: 503 });
  const given = req.headers.get(STATION_KEY_HEADER) ?? "";
  const a = Buffer.from(given);
  const b = Buffer.from(expected);
  if (a.length !== b.length || !timingSafeEqual(a, b)) return NextResponse.json({ error: "invalid station key" }, { status: 401 });
  const station = (req.headers.get(STATION_NAME_HEADER) ?? "").trim().slice(0, 80);
  if (!station) return NextResponse.json({ error: `${STATION_NAME_HEADER} header is required` }, { status: 400 });
  return { station };
}
