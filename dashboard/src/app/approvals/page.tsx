"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Button, Card, Empty, Page, PageHeader, api, fmtTime, postJson } from "@/components/ui";
import { can, useMe } from "@/components/useMe";
import type { DocRow } from "@/components/views/types";
import type { AccessRequest } from "@/lib/domain";
import { roleLabel } from "@/lib/domain";

// Everything waiting on a decision: VA versions (Approvers) and access requests (Admin).
export default function ApprovalsPage() {
  const me = useMe();
  const [docs, setDocs] = useState<DocRow[] | null>(null);
  const [requests, setRequests] = useState<AccessRequest[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = () => {
    api<DocRow[]>("/api/va?status=pending").then(setDocs).catch(() => setDocs([]));
    api<AccessRequest[]>("/api/access-requests").then(setRequests).catch(() => {});
  };
  useEffect(load, []);

  async function decide(r: AccessRequest, approve: boolean) {
    setError(null);
    try {
      await postJson(`/api/admin/access-requests/${r.id}`, { approve });
      load();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  const isAdmin = !!me?.roles.includes("admin");

  return (
    <Page wide={false}>
      <PageHeader eyebrow="Visual Aids" title="Approvals" />
      {error && <p className="mb-3 text-sm text-red-400">{error}</p>}
      <Card title="VA versions waiting for approval" className="mb-4">
        {!docs ? (
          <Empty>Loading…</Empty>
        ) : docs.length === 0 ? (
          <Empty>Nothing waiting.</Empty>
        ) : (
          docs.map((d) => (
            <Link key={d.id} href={`/va/doc/${d.id}`} className="flex flex-wrap items-center justify-between gap-2 border-t border-neutral-800 py-3 first:border-t-0 hover:text-indigo-300">
              <span>
                <span className="font-semibold">{d.productName}</span> <span className="font-mono text-neutral-400">v{d.version}</span> - {d.title}
                <div className="text-xs text-neutral-500">
                  {d.pages.length} pages, {d.mappings.length} steps · submitted by {d.submittedBy} {fmtTime(d.submittedAt)}
                </div>
              </span>
              <span className="text-xs font-semibold text-indigo-400">{can(me, "approver") ? "Review →" : "View →"}</span>
            </Link>
          ))
        )}
      </Card>
      {isAdmin && (
        <Card title="Access requests">
          {requests.length === 0 ? (
            <Empty>No one is waiting for access.</Empty>
          ) : (
            requests.map((r) => (
              <div key={r.id} className="flex flex-wrap items-center justify-between gap-3 border-t border-neutral-800 py-3 first:border-t-0">
                <div>
                  <span className="font-semibold">{r.personName}</span> <span className="font-mono text-xs text-neutral-500">{r.ntid}</span> asks for <b>{roleLabel(r.role)}</b>
                  {r.note && <div className="text-xs text-neutral-400">“{r.note}”</div>}
                  <div className="font-mono text-[10.5px] text-neutral-600">{fmtTime(r.createdAt)}</div>
                </div>
                <div className="flex gap-2">
                  <Button onClick={() => decide(r, true)}>Grant</Button>
                  <Button variant="ghost" onClick={() => decide(r, false)}>Dismiss</Button>
                </div>
              </div>
            ))
          )}
        </Card>
      )}
    </Page>
  );
}
