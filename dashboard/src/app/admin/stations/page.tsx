"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { Card, Empty, Page, PageHeader, api, fmtTime } from "@/components/ui";
import type { Station } from "@/lib/domain";

// How a station PC is connected: the AVIS middleware's "Dashboard" settings.
export default function StationSetupPage() {
  const [data, setData] = useState<{ stations: Station[]; keyConfigured: boolean } | null>(null);
  const origin = useSyncExternalStore(
    () => () => {},
    () => window.location.origin,
    () => "http://10.77.193.155:3230",
  );
  const load = () => api<{ stations: Station[]; keyConfigured: boolean }>("/api/admin/stations").then(setData).catch(() => {});
  useEffect(() => {
    load();
  }, []);

  async function remove(name: string) {
    if (!confirm(`Remove ${name} from the list? It reappears as soon as it reports in again.`)) return;
    await fetch(`/api/admin/stations?name=${encodeURIComponent(name)}`, { method: "DELETE" });
    load();
  }

  return (
    <Page wide={false}>
      <PageHeader eyebrow="Admin" title="Station Setup" />
      <Card title="Connect an AVIS station" className="mb-4">
        {!data?.keyConfigured && (
          <p className="mb-3 rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-300">
            AVIS_STATION_KEY isn&apos;t set in this dashboard&apos;s .env.local yet - stations can&apos;t connect until it is (see deploy/README.md).
          </p>
        )}
        <p className="mb-2 text-sm text-neutral-400">On each station PC, in AVIS&apos;s <span className="font-mono">appsettings.json</span>:</p>
        <pre className="overflow-x-auto rounded-lg bg-neutral-950 p-3 font-mono text-xs text-neutral-300">{`"Dashboard": {
  "Enabled": true,
  "BaseUrl": "${origin}"
}`}</pre>
        <p className="mt-2 text-sm text-neutral-400">
          and the Windows environment variable <span className="font-mono">AVIS_DASHBOARD_KEY</span> = the same value as this dashboard&apos;s
          <span className="font-mono"> AVIS_STATION_KEY</span>. The station name comes from <span className="font-mono">Station:Name</span>. The station then
          uploads every unit, check result and fault, and downloads the approved VAs (cached on the PC, so it keeps working if this Pi is offline).
        </p>
      </Card>
      <Card title="Stations that have reported in">
        {!data ? (
          <Empty>Loading…</Empty>
        ) : data.stations.length === 0 ? (
          <Empty>None yet.</Empty>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">
                <th className="pb-2">Station</th>
                <th className="pb-2">App version</th>
                <th className="pb-2">First seen</th>
                <th className="pb-2">Last seen</th>
                <th className="pb-2" />
              </tr>
            </thead>
            <tbody>
              {data.stations.map((s) => (
                <tr key={s.name} className="border-t border-neutral-800">
                  <td className="py-2 font-semibold">{s.name}</td>
                  <td className="py-2 font-mono text-xs text-neutral-400">{s.lastHeartbeat?.appVersion ?? "–"}</td>
                  <td className="py-2 font-mono text-xs text-neutral-500">{fmtTime(s.firstSeen)}</td>
                  <td className="py-2 font-mono text-xs text-neutral-500">{fmtTime(s.lastSeen)}</td>
                  <td className="py-2 text-right">
                    <button onClick={() => remove(s.name)} className="font-mono text-xs text-neutral-600 hover:text-red-400">remove</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </Page>
  );
}
