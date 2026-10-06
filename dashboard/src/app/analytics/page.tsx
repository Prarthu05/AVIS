"use client";

import { useEffect, useMemo, useState } from "react";
import { Card, Empty, Page, PageHeader, RangePicker, Select, api, pct } from "@/components/ui";
import { HBars, ThroughputChart } from "@/components/charts";
import { formatDuration, type GroupRow } from "@/lib/analytics";
import type { AnalyticsResponse, StationRow, ProductRow } from "@/components/views/types";

function GroupTable({ rows, keyLabel }: { rows: GroupRow[]; keyLabel: string }) {
  if (rows.length === 0) return <Empty>No units in this range.</Empty>;
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
            <th className="pb-2">{keyLabel}</th>
            <th className="pb-2 text-right">Units</th>
            <th className="pb-2 text-right">Confirmed</th>
            <th className="pb-2 text-right">FPY</th>
            <th className="pb-2 text-right">Yield</th>
            <th className="pb-2 text-right">Reworks</th>
            <th className="pb-2 text-right">Escalations</th>
            <th className="pb-2 text-right">Avg cycle</th>
          </tr>
        </thead>
        <tbody className="font-mono">
          {rows.map((r) => (
            <tr key={r.key} className="border-t border-neutral-800">
              <td className="py-1.5 font-sans text-neutral-100">{r.key}</td>
              <td className="py-1.5 text-right text-neutral-300">{r.units}</td>
              <td className="py-1.5 text-right text-neutral-300">{r.confirmed}</td>
              <td className="py-1.5 text-right text-neutral-300">{pct(r.fpy)}</td>
              <td className="py-1.5 text-right text-neutral-300">{pct(r.yieldPct)}</td>
              <td className="py-1.5 text-right text-neutral-400">{r.reworks}</td>
              <td className={`py-1.5 text-right ${r.escalations ? "text-red-400" : "text-neutral-400"}`}>{r.escalations}</td>
              <td className="py-1.5 text-right text-neutral-400">{formatDuration(r.avgCycleSec)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function AnalyticsPage() {
  const [range, setRange] = useState("7d");
  const [station, setStation] = useState("");
  const [product, setProduct] = useState("");
  const [data, setData] = useState<AnalyticsResponse | null>(null);
  const [stations, setStations] = useState<StationRow[]>([]);
  const [products, setProducts] = useState<ProductRow[]>([]);

  useEffect(() => {
    api<StationRow[]>("/api/stations").then(setStations).catch(() => {});
    api<ProductRow[]>("/api/products").then(setProducts).catch(() => {});
  }, []);
  useEffect(() => {
    const q = new URLSearchParams({ range });
    if (station) q.set("station", station);
    if (product) q.set("product", product);
    api<AnalyticsResponse>(`/api/analytics?${q}`).then(setData).catch(() => setData(null));
  }, [range, station, product]);

  const stepProducts = useMemo(() => Array.from(new Set((data?.stepTimes ?? []).map((s) => s.product))), [data]);

  return (
    <Page>
      <PageHeader eyebrow="AVIS" title="Analytics">
        <Select label="Station" value={station} onChange={setStation} options={[{ value: "", label: "All" }, ...stations.map((s) => ({ value: s.name, label: s.name }))]} />
        <Select label="Product" value={product} onChange={setProduct} options={[{ value: "", label: "All" }, ...products.map((p) => ({ value: p.name, label: p.name }))]} />
        <RangePicker value={range} onChange={setRange} />
      </PageHeader>

      {!data ? (
        <Empty>Loading…</Empty>
      ) : (
        <div className="flex flex-col gap-4">
          <Card title={`Throughput - ${data.kpis.finished} finished, FPY ${pct(data.kpis.fpy)}, yield ${pct(data.kpis.yieldPct)}`}>
            <ThroughputChart points={data.series} />
          </Card>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card title="LJ / IV4 checks">
              {data.checks.length === 0 ? (
                <Empty>No check results in this range.</Empty>
              ) : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-left font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
                      <th className="pb-2">Check</th>
                      <th className="pb-2 text-right">Units</th>
                      <th className="pb-2 text-right">Attempts</th>
                      <th className="pb-2 text-right">NG</th>
                      <th className="pb-2 text-right">1st-try NG</th>
                      <th className="pb-2 text-right">NG rate</th>
                      <th className="pb-2 text-right">Escalated</th>
                    </tr>
                  </thead>
                  <tbody className="font-mono">
                    {data.checks.map((c) => (
                      <tr key={c.check} className="border-t border-neutral-800">
                        <td className="py-1.5 font-sans font-semibold text-neutral-100">{c.check}</td>
                        <td className="py-1.5 text-right text-neutral-300">{c.units}</td>
                        <td className="py-1.5 text-right text-neutral-300">{c.attempts}</td>
                        <td className="py-1.5 text-right text-neutral-300">{c.failures}</td>
                        <td className="py-1.5 text-right text-amber-400">{c.firstAttemptNgPct}%</td>
                        <td className="py-1.5 text-right text-neutral-400">{c.ngRatePct}%</td>
                        <td className={`py-1.5 text-right ${c.escalatedUnits ? "text-red-400" : "text-neutral-400"}`}>{c.escalatedUnits}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </Card>
            <Card title="Fault Pareto (by code)">
              <HBars rows={data.paretoByCode.map((r) => ({ label: r.label, value: r.count, sub: `${r.share}%` }))} />
            </Card>
          </div>

          <Card title="Time on each LightGuide step (bottlenecks)">
            {data.stepTimes.length === 0 ? (
              <Empty>No step timing yet - it comes from LightGuide step changes reported by the stations.</Empty>
            ) : (
              <div className="grid gap-6 lg:grid-cols-2">
                {stepProducts.map((p) => (
                  <div key={p}>
                    <div className="mb-2 text-sm font-semibold text-neutral-200">{p}</div>
                    <HBars
                      unit="s"
                      rows={data.stepTimes
                        .filter((s) => s.product === p)
                        .map((s) => ({ label: `Step ${s.step}${s.comment ? ` - ${s.comment}` : ""}`, value: s.avgSec, sub: `max ${formatDuration(s.maxSec)}` }))}
                    />
                  </div>
                ))}
              </div>
            )}
          </Card>

          <div className="grid gap-4 xl:grid-cols-2">
            <Card title="By station">
              <GroupTable rows={data.byStation} keyLabel="Station" />
            </Card>
            <Card title="By product">
              <GroupTable rows={data.byProduct} keyLabel="Product" />
            </Card>
          </div>
          <Card title="By operator (NTID)">
            <GroupTable rows={data.byOperator} keyLabel="NTID" />
          </Card>
        </div>
      )}
    </Page>
  );
}
