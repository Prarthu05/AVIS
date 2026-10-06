"use client";

import { useEffect, useState } from "react";
import { Card, Empty, Page, PageHeader, api, fmtTime } from "@/components/ui";

interface Entry {
  ts: string;
  actor: string;
  action: string;
  details: Record<string, unknown>;
}

// Append-only history of every access / VA / fault decision (like App Hub's audit.log).
export default function AuditPage() {
  const [entries, setEntries] = useState<Entry[] | null>(null);
  useEffect(() => {
    api<Entry[]>("/api/admin/audit").then(setEntries).catch(() => setEntries([]));
  }, []);
  return (
    <Page wide={false}>
      <PageHeader eyebrow="Admin" title="Audit Log" />
      <Card>
        {!entries ? (
          <Empty>Loading…</Empty>
        ) : entries.length === 0 ? (
          <Empty>Nothing recorded yet.</Empty>
        ) : (
          entries.map((e, i) => (
            <div key={i} className="grid grid-cols-[9rem_8rem_1fr] gap-3 border-t border-neutral-800 py-2 text-sm first:border-t-0">
              <span className="font-mono text-xs text-neutral-500">{fmtTime(e.ts)}</span>
              <span className="truncate text-neutral-300">{e.actor}</span>
              <span>
                <span className="font-mono text-xs text-indigo-300">{e.action}</span>{" "}
                <span className="font-mono text-xs text-neutral-500">{Object.entries(e.details).map(([k, v]) => `${k}=${typeof v === "object" ? JSON.stringify(v) : String(v)}`).join("  ")}</span>
              </span>
            </div>
          ))
        )}
      </Card>
    </Page>
  );
}
