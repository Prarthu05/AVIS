import { NextResponse } from "next/server";
import { listStations, removeStation } from "@/lib/eventStore";
import { getSession } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

export async function GET() {
  return NextResponse.json({ stations: listStations(), keyConfigured: !!process.env.AVIS_STATION_KEY });
}

export async function DELETE(req: Request) {
  const session = await getSession();
  const name = new URL(req.url).searchParams.get("name");
  if (!name) return NextResponse.json({ error: "name is required" }, { status: 400 });
  removeStation(name);
  audit(session?.name ?? "?", "station.remove", { name });
  return NextResponse.json({ ok: true });
}
