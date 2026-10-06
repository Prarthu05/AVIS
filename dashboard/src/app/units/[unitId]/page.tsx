"use client";

import { use, useEffect, useState } from "react";
import Link from "next/link";
import { Card, Empty, KpiTile, Page, PageHeader, Pill, api, fmtTime } from "@/components/ui";
import { formatDuration } from "@/lib/analytics";
import type { StationEvent, UnitSummary } from "@/components/views/types";

const EVENT_LABEL: Record<string, string> = {
  unit_started: "WIP started in iFactory (NTID + Asset ID)",
  step_changed: "LightGuide step",
  check_result: "Check result",
  unit_confirmed: "Station data confirmed OK - sent to iFactory",
  unit_not_confirmed: "Not confirmed",
  unit_abandoned: "Abandoned (next part arrived)",
  escalated: "Escalated - 5th failed rework",
  fault: "Fault",
};

function describe(e: StationEvent): string {
  switch (e.type) {
    case "step_changed":
      return `Step ${e.step}${e.stepComment ? ` - ${e.stepComment}` : ""}`;
    case "check_result":
      return `${e.check} = ${e.value ?? e.result}${e.attempt ? ` (attempt ${e.attempt})` : ""}${e.measurements ? " · " + Object.entries(e.measurements).map(([k, v]) => `${k} ${v}`).join(", ") : ""}`;
    case "fault":
      return `${e.faultCode} ${e.faultTitle ?? ""} - ${e.message ?? ""}`;
    default:
      return e.message ?? "";
  }
}

export default function UnitPage({ params }: { params: Promise<{ unitId: string }> }) {
  const { unitId } = use(params);
  const [data, setData] = useState<{ unit: UnitSummary; events: StationEvent[] } | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    api<{ unit: UnitSummary; events: StationEvent[] }>(`/api/units/${unitId}?days=90`).then(setData).catch((e: Error) => setError(e.message));
  }, [unitId]);

  if (error) return <Page><Empty>{error}</Empty></Page>;
  if (!data) return <Page><Empty>Loading…</Empty></Page>;
  const u = data.unit;

  return (
    <Page>
      <PageHeader eyebrow={`Unit · ${u.station}`} title={u.assetId ?? unitId}>
        <Pill value={u.outcome} />
        {u.escalated && <Pill value="escalated" />}
        <Link href="/units" className="text-xs font-semibold text-indigo-400 hover:text-indigo-300">← all units</Link>
      </PageHeader>
      <div className="mb-4 grid grid-cols-2 gap-3 md:grid-cols-6">
        <KpiTile label="Product" value={u.product ?? "–"} sub={u.material} />
        <KpiTile label="Operator" value={u.ntid ?? "–"} />
        <KpiTile label="WIP" value={u.wipId ? String(u.wipId) : "–"} />
        <KpiTile label="Cycle" value={formatDuration(u.cycleSec)} />
        <KpiTile label="Reworks" value={String(u.reworks)} tone={u.reworks ? "warn" : "default"} />
        <KpiTile label="Faults" value={String(u.faults)} tone={u.faults ? "bad" : "default"} />
      </div>
      <div className="grid gap-4 lg:grid-cols-3">
        <Card title="Timeline" className="lg:col-span-2">
          <ol className="relative ml-2 border-l border-neutral-800">
            {data.events.map((e) => (
              <li key={e.id} className="mb-3 ml-4">
                <span
                  className={`absolute -left-[5px] mt-1.5 h-2.5 w-2.5 rounded-full ${
                    e.type === "fault" || e.result === "NG" || e.type === "escalated" || e.type === "unit_not_confirmed" ? "bg-red-400" : e.type === "unit_confirmed" || e.result === "OK" ? "bg-emerald-400" : "bg-neutral-600"
                  }`}
                />
                <div className="flex flex-wrap items-baseline gap-2">
                  <span className="font-mono text-[11px] text-neutral-500">{fmtTime(e.at)}</span>
                  <span className="text-sm font-semibold text-neutral-200">{EVENT_LABEL[e.type] ?? e.type}</span>
                  {e.result && <Pill value={e.result} />}
                  {e.severity && <Pill value={e.severity} />}
                </div>
                <div className="text-sm text-neutral-400">{describe(e)}</div>
                {e.imagePath && <div className="font-mono text-[11px] text-neutral-600">IV4 image on station: {e.imagePath}</div>}
              </li>
            ))}
          </ol>
        </Card>
        <div className="flex flex-col gap-4">
          <Card title="Checks">
            {Object.keys(u.checks).length === 0 ? (
              <Empty>No check results.</Empty>
            ) : (
              Object.entries(u.checks).map(([n, c]) => (
                <div key={n} className="flex items-center justify-between border-t border-neutral-800 py-2 first:border-t-0">
                  <span className="font-semibold">{n}</span>
                  <span className="flex items-center gap-2 font-mono text-xs text-neutral-400">
                    {c.attempts} attempt(s), {c.failures} NG {c.lastResult && <Pill value={c.lastResult} />}
                  </span>
                </div>
              ))
            )}
          </Card>
          <Card title="LightGuide steps">
            {u.steps.length === 0 ? (
              <Empty>No steps reported.</Empty>
            ) : (
              u.steps.map((s, i) => (
                <div key={i} className="flex justify-between border-t border-neutral-800 py-1.5 text-sm first:border-t-0">
                  <span className="text-neutral-300">
                    Step {s.step}
                    {s.comment && <span className="text-neutral-500"> - {s.comment}</span>}
                  </span>
                  <span className="font-mono text-neutral-400">{formatDuration(s.durationSec)}</span>
                </div>
              ))
            )}
          </Card>
          <p className="font-mono text-[10.5px] text-neutral-600">Correlation id {u.unitId}</p>
        </div>
      </div>
    </Page>
  );
}
