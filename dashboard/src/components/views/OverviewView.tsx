"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { Activity, BadgeCheck, CircleAlert, Clock, Repeat, Siren, Target, TriangleAlert } from "lucide-react";
import { Card, Empty, KpiTile, Page, PageHeader, Pill, RangePicker, api, fmtTime, pct } from "@/components/ui";
import { HBars, ThroughputChart } from "@/components/charts";
import { formatDuration } from "@/lib/analytics";
import type { AnalyticsResponse, StationRow } from "./types";

// Live landing page: KPIs for the range, throughput, the station board and
// what's failing most. Refreshes every 30 s, like a floor display should.
export default function OverviewView({ station }: { station?: string }) {
  const [range, setRange] = useState("today");
  const [data, setData] = useState<AnalyticsResponse | null>(null);
  const [stations, setStations] = useState<StationRow[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    const q = new URLSearchParams({ range });
    if (station) q.set("station", station);
    api<AnalyticsResponse>(`/api/analytics?${q}`)
      .then((d) => {
        setData(d);
        setError(null);
      })
      .catch((e: Error) => setError(e.message));
    api<StationRow[]>("/api/stations").then(setStations).catch(() => {});
  }, [range, station]);

  useEffect(() => {
    load();
    const t = setInterval(load, 30_000);
    return () => clearInterval(t);
  }, [load]);

  const k = data?.kpis;
  const shown = station ? stations.filter((s) => s.name === station) : stations;

  return (
    <Page>
      <PageHeader eyebrow={station ? "Station" : "AVIS"} title={station ?? "Overview"}>
        <RangePicker value={range} onChange={setRange} />
      </PageHeader>
      {error && <p className="mb-4 text-sm text-red-400">{error}</p>}

      <div className="mb-4 grid grid-cols-2 gap-3 md:grid-cols-4 xl:grid-cols-8">
        <KpiTile label="Units" value={k ? String(k.started) : "–"} sub={k ? `${k.inProgress} in progress` : undefined} icon={Activity} />
        <KpiTile label="Confirmed OK" value={k ? String(k.confirmed) : "–"} sub={k ? `${k.notConfirmed + k.abandoned} not confirmed` : undefined} icon={BadgeCheck} tone="good" />
        <KpiTile label="First-pass yield" value={pct(k?.fpy)} sub="OK with no rework" icon={Target} tone={k?.fpy !== null && k?.fpy !== undefined && k.fpy < 90 ? "warn" : "default"} />
        <KpiTile label="Yield" value={pct(k?.yieldPct)} sub="OK after rework" icon={Target} />
        <KpiTile label="Reworks" value={k ? String(k.reworks) : "–"} sub="LJ / IV4 NG results" icon={Repeat} tone={k && k.reworks > 0 ? "warn" : "default"} />
        <KpiTile label="Escalations" value={k ? String(k.escalations) : "–"} sub="5th failed rework" icon={Siren} tone={k && k.escalations > 0 ? "bad" : "default"} />
        <KpiTile label="Avg cycle" value={formatDuration(k?.avgCycleSec)} sub="confirmed units" icon={Clock} />
        <KpiTile label="Open faults" value={k ? String(k.openFaults) : "–"} sub={k ? `${k.faults} total, ${k.criticalFaults} critical` : undefined} icon={TriangleAlert} tone={k && k.openFaults > 0 ? "bad" : "good"} />
      </div>

      <div className="mb-4 grid gap-4 lg:grid-cols-3">
        <Card title="Throughput" className="lg:col-span-2">
          {data ? <ThroughputChart points={data.series} /> : <Empty>Loading…</Empty>}
        </Card>
        <Card title="Faults by integration point" action={<Link href="/faults" className="text-xs font-semibold text-indigo-400 hover:text-indigo-300">Fault feed →</Link>}>
          <HBars rows={(data?.paretoByPoint ?? []).map((r) => ({ label: r.label, value: r.count, sub: r.open ? `${r.open} open` : undefined }))} />
        </Card>
      </div>

      <div className="mb-4 grid gap-4 lg:grid-cols-2">
        <Card title="Stations" action={<Link href="/stations" className="text-xs font-semibold text-indigo-400 hover:text-indigo-300">All stations →</Link>}>
          {shown.length === 0 ? (
            <Empty>No station has reported in yet.</Empty>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
                  <th className="pb-2">Station</th>
                  <th className="pb-2">Status</th>
                  <th className="pb-2">Now</th>
                  <th className="pb-2">Last seen</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((s) => (
                  <tr key={s.name} className="border-t border-neutral-800">
                    <td className="py-2">
                      <Link href={`/stations/${encodeURIComponent(s.name)}`} className="font-semibold text-neutral-100 hover:text-indigo-300">
                        {s.name}
                      </Link>
                    </td>
                    <td className="py-2">
                      <Pill value={s.online ? "online" : "offline"} />
                    </td>
                    <td className="py-2 text-xs text-neutral-400">
                      {s.lastHeartbeat?.assetId ? (
                        <>
                          <span className="font-mono text-neutral-200">{s.lastHeartbeat.assetId}</span>
                          {s.lastHeartbeat.step !== undefined && ` · step ${s.lastHeartbeat.step}`}
                          {s.lastHeartbeat.phase && <span className="text-neutral-500"> · {s.lastHeartbeat.phase}</span>}
                        </>
                      ) : (
                        s.lastHeartbeat?.phase ?? "–"
                      )}
                    </td>
                    <td className="py-2 font-mono text-xs text-neutral-500">{fmtTime(s.lastSeen)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
        <Card title="Top fault codes">
          <HBars rows={(data?.paretoByCode ?? []).slice(0, 8).map((r) => ({ label: r.label, value: r.count, sub: `${r.share}%` }))} />
        </Card>
      </div>

      <Card title="Latest units" action={<Link href="/units" className="text-xs font-semibold text-indigo-400 hover:text-indigo-300">Traceability →</Link>}>
        {!data || data.recentUnits.length === 0 ? (
          <Empty>No units in this range.</Empty>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
                  <th className="pb-2">Asset ID</th>
                  <th className="pb-2">Product</th>
                  <th className="pb-2">Station</th>
                  <th className="pb-2">Operator</th>
                  <th className="pb-2">Result</th>
                  <th className="pb-2">Reworks</th>
                  <th className="pb-2">Cycle</th>
                  <th className="pb-2">Started</th>
                </tr>
              </thead>
              <tbody>
                {data.recentUnits.map((u) => (
                  <tr key={u.unitId} className="border-t border-neutral-800">
                    <td className="py-2">
                      <Link href={`/units/${u.unitId}`} className="font-mono text-neutral-100 hover:text-indigo-300">
                        {u.assetId ?? u.unitId.slice(0, 8)}
                      </Link>
                    </td>
                    <td className="py-2 text-neutral-300">{u.product ?? u.material ?? "–"}</td>
                    <td className="py-2 text-neutral-400">{u.station}</td>
                    <td className="py-2 font-mono text-neutral-400">{u.ntid ?? "–"}</td>
                    <td className="py-2">
                      <span className="flex gap-1">
                        <Pill value={u.outcome} />
                        {u.escalated && <Pill value="escalated" />}
                      </span>
                    </td>
                    <td className="py-2 font-mono text-neutral-400">{u.reworks}</td>
                    <td className="py-2 font-mono text-neutral-400">{formatDuration(u.cycleSec)}</td>
                    <td className="py-2 font-mono text-xs text-neutral-500">{fmtTime(u.startedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
      {k && k.openFaults > 0 && (
        <p className="mt-3 flex items-center gap-1.5 text-xs text-red-400">
          <CircleAlert size={13} /> {k.openFaults} fault(s) still open in this range - see the Fault Feed.
        </p>
      )}
    </Page>
  );
}
