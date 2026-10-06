"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { Button, Card, Empty, Page, PageHeader, Pill, RangePicker, Select, api, fmtTime, inputCls, postJson } from "@/components/ui";
import { can, useMe } from "@/components/useMe";
import type { FaultRow, StationRow } from "@/components/views/types";

const POINTS = ["Config", "Badge Scanner", "JabilEye", "iFactory", "LightGuide", "LJ", "IV4", "Check", "Image Staging", "Station", "Display", "Middleware"];

// Dashboard: fault feed - every fault from every station with its error code,
// filterable, and worked by technicians (mark resolved with a note).
export default function FaultsPage() {
  const me = useMe();
  const [range, setRange] = useState("7d");
  const [status, setStatus] = useState("open");
  const [station, setStation] = useState("");
  const [point, setPoint] = useState("");
  const [severity, setSeverity] = useState("");
  const [faults, setFaults] = useState<FaultRow[] | null>(null);
  const [stations, setStations] = useState<StationRow[]>([]);
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    const q = new URLSearchParams({ range, status });
    if (station) q.set("station", station);
    if (point) q.set("point", point);
    if (severity) q.set("severity", severity);
    api<FaultRow[]>(`/api/faults?${q}`).then(setFaults).catch(() => setFaults([]));
  }, [range, status, station, point, severity]);

  useEffect(() => {
    load();
    const t = setInterval(load, 20_000);
    return () => clearInterval(t);
  }, [load]);
  useEffect(() => {
    api<StationRow[]>("/api/stations").then(setStations).catch(() => {});
  }, []);

  async function resolve(f: FaultRow, reopen = false) {
    setError(null);
    try {
      await postJson(`/api/faults/${f.id}/resolve`, reopen ? { reopen: true } : { note: notes[f.id] ?? "" });
      load();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  const canResolve = can(me, "technician", "engineer");

  return (
    <Page>
      <PageHeader eyebrow="Dashboard" title="Fault Feed">
        <Select label="Status" value={status} onChange={setStatus} options={[{ value: "open", label: "Open" }, { value: "resolved", label: "Resolved" }, { value: "all", label: "All" }]} />
        <Select label="Station" value={station} onChange={setStation} options={[{ value: "", label: "All" }, ...stations.map((s) => ({ value: s.name, label: s.name }))]} />
        <Select label="Point" value={point} onChange={setPoint} options={[{ value: "", label: "All" }, ...POINTS.map((p) => ({ value: p, label: p }))]} />
        <Select label="Severity" value={severity} onChange={setSeverity} options={[{ value: "", label: "All" }, ...["Critical", "Error", "Warning", "Info"].map((s) => ({ value: s, label: s }))]} />
        <RangePicker value={range} onChange={setRange} />
      </PageHeader>
      {error && <p className="mb-3 text-sm text-red-400">{error}</p>}
      <Card>
        {!faults ? (
          <Empty>Loading…</Empty>
        ) : faults.length === 0 ? (
          <Empty>{status === "open" ? "No open faults - all clear." : "No faults match."}</Empty>
        ) : (
          <div className="flex flex-col">
            {faults.map((f) => (
              <div key={f.id} className={`grid gap-3 border-t border-neutral-800 py-3 first:border-t-0 md:grid-cols-[9rem_1fr_16rem] ${f.severity === "Critical" ? "bg-red-500/5" : ""}`}>
                <div className="font-mono text-xs">
                  <div className="text-sm font-bold text-neutral-100">{f.faultCode}</div>
                  <div className="text-neutral-500">{fmtTime(f.at)}</div>
                  <div className="text-neutral-500">{f.station}</div>
                </div>
                <div>
                  <div className="mb-1 flex flex-wrap items-center gap-2">
                    <span className="font-semibold">{f.faultTitle}</span>
                    {f.severity && <Pill value={f.severity} />}
                    <span className="font-mono text-[10.5px] text-neutral-500">{f.integrationPoint}</span>
                  </div>
                  <div className="text-sm text-neutral-400">{f.message}</div>
                  {(f.assetId || f.ntid) && (
                    <div className="mt-1 font-mono text-[11px] text-neutral-500">
                      {f.unitId ? (
                        <Link href={`/units/${f.unitId}`} className="text-indigo-400 hover:text-indigo-300">
                          {f.assetId ?? "unit"}
                        </Link>
                      ) : (
                        f.assetId
                      )}
                      {f.ntid && ` · operator ${f.ntid}`}
                      {f.wipId && ` · WIP ${f.wipId}`}
                    </div>
                  )}
                </div>
                <div className="text-sm">
                  {f.resolution ? (
                    <div>
                      <div className="text-emerald-400">✓ Resolved by {f.resolution.resolvedBy}</div>
                      <div className="font-mono text-[11px] text-neutral-500">{fmtTime(f.resolution.resolvedAt)}</div>
                      {f.resolution.note && <div className="text-xs text-neutral-400">{f.resolution.note}</div>}
                      {canResolve && (
                        <button onClick={() => resolve(f, true)} className="mt-1 font-mono text-[11px] text-neutral-500 hover:text-neutral-200">
                          reopen
                        </button>
                      )}
                    </div>
                  ) : f.severity === "Info" ? (
                    <span className="text-xs text-neutral-500">Notice - no action needed</span>
                  ) : canResolve ? (
                    <div className="flex gap-2">
                      <input value={notes[f.id] ?? ""} onChange={(e) => setNotes((n) => ({ ...n, [f.id]: e.target.value }))} placeholder="What was done?" className={`${inputCls} min-w-0 flex-1 py-1.5 text-xs`} />
                      <Button onClick={() => resolve(f)}>Resolve</Button>
                    </div>
                  ) : (
                    <span className="text-xs text-amber-400">Open</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>
    </Page>
  );
}
