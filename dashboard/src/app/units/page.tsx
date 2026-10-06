"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Card, Empty, Page, PageHeader, Pill, RangePicker, Select, api, fmtTime, inputCls } from "@/components/ui";
import { formatDuration } from "@/lib/analytics";
import type { UnitSummary } from "@/components/views/types";

// "What happened to unit X" - search by Asset ID, NTID, WIP, product or station.
export default function UnitsPage() {
  const [q, setQ] = useState("");
  const [range, setRange] = useState("7d");
  const [outcome, setOutcome] = useState("");
  const [units, setUnits] = useState<UnitSummary[] | null>(null);

  useEffect(() => {
    const t = setTimeout(() => {
      const params = new URLSearchParams({ q, range });
      if (outcome) params.set("outcome", outcome);
      api<UnitSummary[]>(`/api/units?${params}`).then(setUnits).catch(() => setUnits([]));
    }, 250);
    return () => clearTimeout(t);
  }, [q, range, outcome]);

  return (
    <Page>
      <PageHeader eyebrow="Traceability" title="Units">
        <Select
          label="Result"
          value={outcome}
          onChange={setOutcome}
          options={[
            { value: "", label: "All" },
            { value: "confirmed", label: "Confirmed" },
            { value: "not_confirmed", label: "Not confirmed" },
            { value: "escalated", label: "Escalated" },
            { value: "abandoned", label: "Abandoned" },
            { value: "in_progress", label: "In progress" },
          ]}
        />
        <RangePicker value={range} onChange={setRange} />
      </PageHeader>
      <input autoFocus value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search Asset ID, NTID, WIP, product or station…" className={`${inputCls} mb-4 w-full font-mono`} />
      <Card>
        {!units ? (
          <Empty>Loading…</Empty>
        ) : units.length === 0 ? (
          <Empty>No units match.</Empty>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
                  <th className="pb-2">Asset ID</th>
                  <th className="pb-2">Product</th>
                  <th className="pb-2">WIP</th>
                  <th className="pb-2">Station</th>
                  <th className="pb-2">Operator</th>
                  <th className="pb-2">Result</th>
                  <th className="pb-2">Checks</th>
                  <th className="pb-2">Cycle</th>
                  <th className="pb-2">Started</th>
                </tr>
              </thead>
              <tbody>
                {units.map((u) => (
                  <tr key={u.unitId} className="border-t border-neutral-800">
                    <td className="py-2">
                      <Link href={`/units/${u.unitId}`} className="font-mono text-neutral-100 hover:text-indigo-300">
                        {u.assetId ?? u.unitId.slice(0, 8)}
                      </Link>
                    </td>
                    <td className="py-2 text-neutral-300">{u.product ?? u.material ?? "–"}</td>
                    <td className="py-2 font-mono text-neutral-400">{u.wipId ?? "–"}</td>
                    <td className="py-2 text-neutral-400">{u.station}</td>
                    <td className="py-2 font-mono text-neutral-400">{u.ntid ?? "–"}</td>
                    <td className="py-2">
                      <span className="flex gap-1">
                        <Pill value={u.outcome} />
                        {u.escalated && <Pill value="escalated" />}
                      </span>
                    </td>
                    <td className="py-2 font-mono text-xs text-neutral-400">
                      {Object.entries(u.checks).map(([n, c]) => `${n} ${c.lastResult ?? "?"}${c.failures ? ` (${c.failures} NG)` : ""}`).join(" · ") || "–"}
                    </td>
                    <td className="py-2 font-mono text-neutral-400">{formatDuration(u.cycleSec)}</td>
                    <td className="py-2 font-mono text-xs text-neutral-500">{fmtTime(u.startedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </Page>
  );
}
