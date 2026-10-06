import { NextResponse } from "next/server";
import { getSession } from "@/lib/requireSession";
import { readEvents } from "@/lib/eventStore";
import { summarizeUnits } from "@/lib/analytics";

// One unit's full timeline - every event tagged with its correlation id.
// ?days= how far back to look (default 30).
export async function GET(req: Request, { params }: { params: Promise<{ unitId: string }> }) {
  if (!(await getSession())) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const { unitId } = await params;
  const days = Math.min(400, Math.max(1, Number(new URL(req.url).searchParams.get("days")) || 30));
  const to = new Date();
  const events = readEvents(new Date(to.getTime() - days * 86_400_000), to, (e) => e.unitId === unitId);
  if (events.length === 0) return NextResponse.json({ error: "not found" }, { status: 404 });
  return NextResponse.json({ unit: summarizeUnits(events)[0], events });
}
