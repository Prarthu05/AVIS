import { NextResponse } from "next/server";
import { getSession } from "@/lib/requireSession";
import { listResolutions, readEvents } from "@/lib/eventStore";
import { isOpenFault } from "@/lib/analytics";
import { parseRange } from "@/lib/range";

// The fault feed: ?range=, &station=, &point=, &severity=, &status=open|resolved|all
export async function GET(req: Request) {
  if (!(await getSession())) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const params = new URL(req.url).searchParams;
  const { from, to } = parseRange(params.get("range") ? params : new URLSearchParams("range=7d"));
  const station = params.get("station");
  const point = params.get("point");
  const severity = params.get("severity");
  const status = params.get("status") ?? "all";
  const resolutions = listResolutions();
  const faults = readEvents(from, to, (e) => e.type === "fault")
    .filter((f) => (!station || f.station === station) && (!point || f.integrationPoint === point) && (!severity || f.severity === severity))
    .filter((f) => status === "all" || (status === "open" ? isOpenFault(f, resolutions) : !!resolutions[f.id]))
    .reverse()
    .slice(0, 1000)
    .map((f) => ({ ...f, resolution: resolutions[f.id] ?? null }));
  return NextResponse.json(faults);
}
