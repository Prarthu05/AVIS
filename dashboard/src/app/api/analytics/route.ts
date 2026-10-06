import { NextResponse } from "next/server";
import { getSession } from "@/lib/requireSession";
import { listResolutions, readEvents } from "@/lib/eventStore";
import { checkStats, computeKpis, faultPareto, groupBy, stepTimes, summarizeUnits, timeSeries } from "@/lib/analytics";
import { parseRange } from "@/lib/range";

// Everything the Overview / Analytics pages chart, for one range and optional station/product filter.
export async function GET(req: Request) {
  if (!(await getSession())) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const params = new URL(req.url).searchParams;
  const { from, to, key } = parseRange(params);
  const station = params.get("station") || undefined;
  const product = params.get("product") || undefined;

  const events = readEvents(from, to, (e) => (!station || e.station === station) && (!product || e.product === product || !e.product));
  const units = summarizeUnits(events).filter((u) => !product || u.product === product);
  const faults = events.filter((e) => e.type === "fault");
  const resolutions = listResolutions();
  const days = (to.getTime() - from.getTime()) / 86_400_000;

  return NextResponse.json({
    range: { key, from: from.toISOString(), to: to.toISOString() },
    kpis: computeKpis(units, faults, resolutions),
    series: timeSeries(units, from, to, days <= 2 ? "hour" : "day", new Date().getTimezoneOffset()),
    paretoByCode: faultPareto(faults, resolutions, "code").slice(0, 12),
    paretoByPoint: faultPareto(faults, resolutions, "point"),
    checks: checkStats(units),
    stepTimes: stepTimes(units),
    byStation: groupBy(units, "station"),
    byProduct: groupBy(units, "product"),
    byOperator: groupBy(units, "ntid"),
    recentUnits: units.slice(0, 15),
  });
}
