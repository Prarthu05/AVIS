import { NextResponse } from "next/server";
import { requireStation } from "@/lib/stationAuth";
import { buildStationVaMap } from "@/lib/vaStore";
import { recordVaMapFetch } from "@/lib/eventStore";

// The approved (product, step) -> VA pages map every station downloads.
// Supports If-None-Match with the map's content version, so an unchanged map costs one tiny 304.
export async function GET(req: Request) {
  const auth = requireStation(req);
  if (auth instanceof NextResponse) return auth;
  const map = buildStationVaMap();
  recordVaMapFetch(auth.station, map.version);
  const etag = `"${map.version}"`;
  if (req.headers.get("if-none-match") === etag) return new Response(null, { status: 304, headers: { ETag: etag } });
  return NextResponse.json(map, { headers: { ETag: etag } });
}
