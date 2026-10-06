"use client";

import { useEffect, useState } from "react";
import { ALL_ROLES, roleLabel, type Role, type Person } from "@/lib/domain";


// People + Access, merged onto one Admin-only page (was two separate
// surfaces -- People under Industrial Engineering, Access under Admin).
// Teams (a separate group-membership concept) were removed entirely --
// escalation/NSR notification targeting and page access are both purely
// role-based now, so Role is the only axis Admin manages per person. A
// person can hold more than one role at once (see domain.ts's
// Role/Person comments), so role assignment is a checkbox group per
// person instead of a single dropdown.
export default function AccessAdmin() {
  const [people, setPeople] = useState<Person[]>([]);
  const [loading, setLoading] = useState(true);
  const [newPerson, setNewPerson] = useState({ name: "", email: "", ntid: "" });
  const [ntidDraft, setNtidDraft] = useState<Record<string, string>>({});

  function refresh() {
    fetch("/api/people")
      .then((r) => r.json())
      .then((p) => {
        setPeople(p);
        setLoading(false);
      });
  }
  useEffect(refresh, []);

  const [error, setError] = useState<string | null>(null);

  async function addPerson() {
    if (!newPerson.name.trim()) return;
    const res = await fetch("/api/people", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(newPerson) });
    if (!res.ok) {
      setError((await res.json().catch(() => ({}))).error ?? "Couldn't add that person");
      return;
    }
    setError(null);
    setNewPerson({ name: "", email: "", ntid: "" });
    refresh();
  }
  async function saveNtid(p: Person) {
    const ntid = (ntidDraft[p.id] ?? p.ntid ?? "").trim();
    const res = await fetch(`/api/people/${p.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ntid }) });
    if (!res.ok) {
      setError((await res.json().catch(() => ({}))).error ?? "Couldn't save the NTID");
      return;
    }
    setPeople((ps) => ps.map((x) => (x.id === p.id ? { ...x, ntid } : x)));
  }
  async function deletePerson(id: string) {
    if (!confirm("Remove this person?")) return;
    const res = await fetch(`/api/people/${id}`, { method: "DELETE" });
    if (!res.ok) setError((await res.json().catch(() => ({}))).error ?? "Couldn't remove");
    refresh();
  }

  async function toggleRole(p: Person, role: Role) {
    const current = p.roles ?? [];
    const roles = current.includes(role) ? current.filter((r) => r !== role) : [...current, role];
    const res = await fetch(`/api/admin/people/${p.id}/set-role`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ roles }),
    });
    if (!res.ok) {
      setError((await res.json().catch(() => ({}))).error ?? "Couldn't change roles");
      return;
    }
    setError(null);
    setPeople((ps) => ps.map((x) => (x.id === p.id ? { ...x, roles } : x)));
  }

  const noAccessCount = people.filter((p) => !p.roles || p.roles.length === 0).length;

  return (
    <div className="min-h-screen bg-neutral-950 text-neutral-50">
      <div className="mx-auto max-w-5xl px-5 py-8 sm:px-8">
        <div className="mb-1 font-mono text-[11px] uppercase tracking-wider text-neutral-500">Admin</div>
        <h1 className="mb-2 text-2xl font-bold">People &amp; Access</h1>
        <p className="mb-8 max-w-2xl text-sm text-neutral-400">
          Same sign-in as every app on this Pi: NTID only, shared with App Hub. Someone signing in for the first time gets
          Viewer access automatically; give them a role here (or grant their request under Approvals). Approver lets
          someone sign off VA versions; Engineer uploads and maps VAs; Technician works the fault feed.{" "}
          {noAccessCount} of {people.length} people currently have no access.
        </p>
        {error && <p className="mb-4 text-sm text-red-400">{error}</p>}

        {loading ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : (
          <>
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-neutral-400">People ({people.length})</h2>
            <div className="mb-4 grid gap-2 sm:grid-cols-[1fr_1fr_0.7fr_auto]">
              <input
                value={newPerson.name}
                onChange={(e) => setNewPerson((p) => ({ ...p, name: e.target.value }))}
                placeholder="Name"
                className="rounded-lg border border-neutral-700 bg-neutral-800 px-3 py-2 text-sm text-neutral-100 focus:border-indigo-400 focus:outline-none"
              />
              <input
                value={newPerson.email}
                onChange={(e) => setNewPerson((p) => ({ ...p, email: e.target.value }))}
                placeholder="Email (optional)"
                className="rounded-lg border border-neutral-700 bg-neutral-800 px-3 py-2 text-sm text-neutral-100 focus:border-indigo-400 focus:outline-none"
              />
              <input
                value={newPerson.ntid}
                onChange={(e) => setNewPerson((p) => ({ ...p, ntid: e.target.value }))}
                placeholder="NTID"
                className="rounded-lg border border-neutral-700 bg-neutral-800 px-3 py-2 font-mono text-sm text-neutral-100 focus:border-indigo-400 focus:outline-none"
              />
              <button onClick={addPerson} className="rounded-lg bg-indigo-500 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-400">
                Add
              </button>
            </div>
            <div className="flex flex-col gap-2">
              {people.map((p) => (
                <div key={p.id} className="rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-2.5">
                  <div className="flex flex-wrap items-center gap-3">
                    <div className="w-48 shrink-0">
                      <div className="text-sm text-neutral-100">{p.name}</div>
                      <div className="font-mono text-[10.5px] text-neutral-500">{p.email}</div>
                    </div>
                    <input
                      value={ntidDraft[p.id] ?? p.ntid ?? ""}
                      onChange={(e) => setNtidDraft((d) => ({ ...d, [p.id]: e.target.value }))}
                      onBlur={() => saveNtid(p)}
                      placeholder="NTID"
                      className="w-24 shrink-0 rounded-md border border-neutral-700 bg-neutral-800 px-2 py-1.5 font-mono text-xs text-neutral-100 focus:border-indigo-400 focus:outline-none"
                    />
                    <span className={`font-mono text-[10.5px] ${p.roles?.length ? "text-emerald-400" : "text-amber-400"}`}>
                      {p.roles?.length ? "has access" : "no access yet"}
                    </span>
                    <button onClick={() => deletePerson(p.id)} className="ml-auto font-mono text-xs text-neutral-600 hover:text-red-400">
                      remove
                    </button>
                  </div>
                  <div className="mt-2 flex flex-wrap items-center gap-1.5">
                    <span className="font-mono text-[9.5px] uppercase tracking-wider text-neutral-600">Roles</span>
                    {ALL_ROLES.map((r) => (
                      <button
                        key={r}
                        onClick={() => toggleRole(p, r)}
                        className={`rounded-full px-2.5 py-1 font-mono text-[10.5px] ${
                          p.roles?.includes(r) ? "bg-emerald-500/25 text-emerald-200" : "bg-neutral-800 text-neutral-500"
                        }`}
                      >
                        {roleLabel(r)}
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
