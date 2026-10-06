import { NextResponse } from "next/server";
import { getSession } from "@/lib/requireSession";
import { isOnline, listStations } from "@/lib/eventStore";
import { buildStationVaMap } from "@/lib/vaStore";

// Live station board for everyone signed in: online/offline, current part/step, link states, VA map in sync.
export async function GET() {
  if (!(await getSession())) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const current = buildStationVaMap().version;
  return NextResponse.json(
    listStations().map((s) => ({ ...s, online: isOnline(s), vaInSync: s.vaMapVersion === current })),
  );
}
