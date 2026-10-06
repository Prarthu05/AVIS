import { createPerson, findPersonByNtid, listPeople, setPersonRoles } from "./peopleStore";

// Same bootstrap as DowntimeApp: until a real Admin exists, the NTID in
// BOOTSTRAP_ADMIN_NTID (.env.local) is promoted/created as Admin on its
// first sign-in. A no-op once any Admin exists.
export async function ensureBootstrapAdmin(): Promise<void> {
  const ntid = process.env.BOOTSTRAP_ADMIN_NTID;
  if (!ntid) return;
  if (listPeople().some((p) => p.roles?.includes("admin"))) return;

  const existing = findPersonByNtid(ntid);
  const person = existing ?? createPerson({ name: "Bootstrap Admin", email: "", ntid });
  setPersonRoles(person.id, [...(person.roles ?? []).filter((r) => r !== "viewer"), "admin"]);
}

// An NTID is a short login-style token - the same restriction Jig Tracker
// applies, so a forged/garbage value can never provision a junk account.
const NTID_RE = /^[A-Za-z0-9._-]{2,40}$/;

export function normalizeNtid(raw: string | null | undefined): string | null {
  const value = (raw ?? "").trim().split("\\").pop() ?? "";
  return NTID_RE.test(value) ? value : null;
}
