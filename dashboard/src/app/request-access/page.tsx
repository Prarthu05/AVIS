"use client";

import { useEffect, useState } from "react";
import { Button, Card, Empty, Page, PageHeader, Select, api, fmtTime, inputCls, postJson } from "@/components/ui";
import { useMe } from "@/components/useMe";
import { roleLabel, roleLabels, type AccessRequest, type Role } from "@/lib/domain";

const DESCRIPTIONS: Record<string, string> = {
  engineer: "Upload VAs and map product steps to pages",
  approver: "Approve VA versions before stations use them",
  technician: "Work the fault feed - mark faults resolved",
  viewer: "Read-only dashboards",
};

// App Hub's self-service "request access": anyone signed in asks for a role, an Admin grants it.
export default function RequestAccessPage() {
  const me = useMe();
  const [role, setRole] = useState("engineer");
  const [note, setNote] = useState("");
  const [mine, setMine] = useState<AccessRequest[]>([]);
  const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null);

  const load = () => api<AccessRequest[]>("/api/access-requests").then(setMine).catch(() => {});
  useEffect(() => {
    load();
  }, []);

  async function submit() {
    setMessage(null);
    try {
      await postJson("/api/access-requests", { role, note });
      setNote("");
      setMessage({ text: "Sent - an Admin will review it.", error: false });
      load();
    } catch (e) {
      setMessage({ text: (e as Error).message, error: true });
    }
  }

  return (
    <Page wide={false}>
      <PageHeader eyebrow="Access" title="Request access" />
      <p className="mb-4 text-sm text-neutral-400">You currently have: <b className="text-neutral-200">{me ? roleLabels(me.roles) : "…"}</b></p>
      <Card className="mb-4">
        <div className="flex flex-col gap-3">
          <Select label="Role" value={role} onChange={setRole} options={(["engineer", "approver", "technician", "viewer"] as Role[]).map((r) => ({ value: r, label: `${roleLabel(r)} - ${DESCRIPTIONS[r]}` }))} />
          <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Why do you need it? (optional)" className={inputCls} />
          <div>
            <Button onClick={submit}>Send request</Button>
          </div>
          {message && <p className={`text-sm ${message.error ? "text-red-400" : "text-emerald-400"}`}>{message.text}</p>}
        </div>
      </Card>
      <Card title="Your pending requests">
        {mine.length === 0 ? <Empty>None.</Empty> : mine.map((r) => (
          <div key={r.id} className="flex justify-between border-t border-neutral-800 py-2 text-sm first:border-t-0">
            <span>{roleLabel(r.role)}</span>
            <span className="font-mono text-xs text-neutral-500">{fmtTime(r.createdAt)}</span>
          </div>
        ))}
      </Card>
    </Page>
  );
}
