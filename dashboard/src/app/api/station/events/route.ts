import { NextResponse } from "next/server";
import { requireStation } from "@/lib/stationAuth";
import { ingestEvents } from "@/lib/eventStore";

// POST { events: StationEvent[] } from the AVIS middleware's outbox.
// Idempotent: a batch retried after a network drop is de-duplicated by event id.
export async function POST(req: Request) {
  const auth = requireStation(req);
  if (auth instanceof NextResponse) return auth;
  const body = (await req.json().catch(() => null)) as { events?: unknown[] } | null;
  if (!body || !Array.isArray(body.events)) return NextResponse.json({ error: "body must be { events: [...] }" }, { status: 400 });
  return NextResponse.json(ingestEvents(auth.station, body.events));
}
