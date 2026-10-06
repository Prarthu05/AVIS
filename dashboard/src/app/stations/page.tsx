"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Card, Empty, Page, PageHeader, Pill, api, fmtTime } from "@/components/ui";
import type { StationRow } from "@/components/views/types";

const LINK_LABEL: Record<string, string> = { scanner: "Badge scanner", camera: "JabilEye", ifactory: "iFactory", lightguide: "LightGuide" };

export default function StationsPage() {
  const [stations, setStations] = useState<StationRow[] | null>(null);
  useEffect(() => {
    const load = () => api<StationRow[]>("/api/stations").then(setStations).catch(() => setStations([]));
    load();
    const t = setInterval(load, 15_000);
    return () => clearInterval(t);
  }, []);

  return (
    <Page>
      <PageHeader eyebrow="Live" title="Stations" />
      {!stations ? (
        <Empty>Loading…</Empty>
      ) : stations.length === 0 ? (
        <Card>
          <Empty>No AVIS station has reported in yet. Stations appear here automatically once their middleware is configured with this dashboard&apos;s URL and station key (see Station Setup).</Empty>
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {stations.map((s) => {
            const hb = s.lastHeartbeat;
            return (
              <Link key={s.name} href={`/stations/${encodeURIComponent(s.name)}`} className="rounded-xl border border-neutral-800 bg-neutral-900 p-4 hover:border-indigo-500/60">
                <div className="mb-3 flex items-center justify-between">
                  <span className="text-lg font-bold">{s.name}</span>
                  <Pill value={s.online ? "online" : "offline"} />
                </div>
                <div className="mb-3 text-sm text-neutral-300">
                  {hb?.message ?? hb?.phase ?? "No status yet"}
                  {hb?.assetId && (
                    <div className="mt-1 font-mono text-xs text-neutral-400">
                      {hb.assetId}
                      {hb.product && ` · ${hb.product}`}
                      {hb.step !== undefined && ` · step ${hb.step}`}
                      {hb.ntid && ` · ${hb.ntid}`}
                    </div>
                  )}
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {Object.entries(hb?.links ?? {}).map(([k, v]) => (
                    <span key={k} className={`rounded-full px-2 py-0.5 font-mono text-[10px] ${v === "Ok" ? "bg-emerald-500/15 text-emerald-300" : v === "Error" ? "bg-red-500/20 text-red-300" : "bg-neutral-800 text-neutral-500"}`}>
                      ● {LINK_LABEL[k] ?? k}
                    </span>
                  ))}
                </div>
                <div className="mt-3 flex justify-between font-mono text-[10.5px] text-neutral-500">
                  <span>last seen {fmtTime(s.lastSeen)}</span>
                  <span className={s.vaInSync ? "text-emerald-400" : "text-amber-400"}>{s.vaInSync ? "VA up to date" : "VA update pending"}</span>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </Page>
  );
}
