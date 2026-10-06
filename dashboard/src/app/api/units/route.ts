import { NextResponse } from "next/server";
import { getSession } from "@/lib/requireSession";
import { readEvents } from "@/lib/eventStore";
import { summarizeUnits } from "@/lib/analytics";
import { parseRange } from "@/lib/range";

// Traceability search: ?q= matches Asset ID, NTID, WIP or product.
export async function GET(req: Request) {
  if (!(await getSession())) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const params = new URL(req.url).searchParams;
  const q = (params.get("q") ?? "").trim().toLowerCase();
  const { from, to } = parseRange(params.get("range") ? params : new URLSearchParams("range=30d"));
  const outcome = params.get("outcome");
  const units = summarizeUnits(readEvents(from, to)).filter(
    (u) =>
      (!q || [u.assetId, u.ntid, u.product, u.material, String(u.wipId ?? ""), u.station].some((v) => v?.toLowerCase().includes(q))) &&
      (!outcome || u.outcome === outcome || (outcome === "escalated" && u.escalated)),
  );
  return NextResponse.json(units.slice(0, 500));
}
