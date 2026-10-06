import { randomUUID } from "crypto";
import type { AccessRequest, Person, Role } from "./domain";
import { readJson, writeJson } from "./jsonStore";

// Same shape and lookup rules as DowntimeApp's peopleStore (trim +
// lowercase NTID match), plus App Hub's self-service access requests.
const PEOPLE = "people.json";
const REQUESTS = "access-requests.json";

export function listPeople(): Person[] {
  return readJson<Person[]>(PEOPLE, []).map((p) => ({ ...p, roles: p.roles ?? [] }));
}

export function getPerson(id: string): Person | undefined {
  return listPeople().find((p) => p.id === id);
}

export function findPersonByNtid(ntid: string): Person | undefined {
  const needle = ntid.trim().toLowerCase();
  return listPeople().find((p) => (p.ntid ?? "").trim().toLowerCase() === needle);
}

export function createPerson(input: Omit<Person, "id">): Person {
  const list = listPeople();
  const person: Person = { id: randomUUID(), ...input, roles: input.roles ?? [] };
  list.push(person);
  writeJson(PEOPLE, list);
  return person;
}

export function updatePerson(id: string, input: Partial<Omit<Person, "id" | "roles">>): Person | null {
  const list = listPeople();
  const idx = list.findIndex((p) => p.id === id);
  if (idx < 0) return null;
  list[idx] = { ...list[idx], ...input, id, roles: list[idx].roles };
  writeJson(PEOPLE, list);
  return list[idx];
}

export function deletePerson(id: string): boolean {
  const list = listPeople();
  const next = list.filter((p) => p.id !== id);
  writeJson(PEOPLE, next);
  writeJson(REQUESTS, listAccessRequests().filter((r) => r.personId !== id));
  return next.length !== list.length;
}

export function setPersonRoles(id: string, roles: Role[]): Person | null {
  const list = listPeople();
  const idx = list.findIndex((p) => p.id === id);
  if (idx < 0) return null;
  list[idx] = { ...list[idx], roles: Array.from(new Set(roles)) };
  writeJson(PEOPLE, list);
  return list[idx];
}

// ---------- Access requests (App Hub's "request access" -> admin approves) ----------
export function listAccessRequests(): AccessRequest[] {
  return readJson<AccessRequest[]>(REQUESTS, []);
}

export function addAccessRequest(person: Person, role: Role, note: string): AccessRequest {
  const list = listAccessRequests();
  const existing = list.find((r) => r.personId === person.id && r.role === role);
  if (existing) return existing;
  const req: AccessRequest = {
    id: randomUUID(),
    personId: person.id,
    personName: person.name,
    ntid: person.ntid,
    role,
    note: note.slice(0, 500),
    createdAt: new Date().toISOString(),
  };
  list.push(req);
  writeJson(REQUESTS, list);
  return req;
}

/** Approve = add the role and clear the request in one step (no "ghost" pending request left behind). */
export function resolveAccessRequest(id: string, approve: boolean): AccessRequest | null {
  const list = listAccessRequests();
  const req = list.find((r) => r.id === id);
  if (!req) return null;
  if (approve) {
    const person = getPerson(req.personId);
    if (person) setPersonRoles(person.id, [...(person.roles ?? []), req.role]);
  }
  writeJson(REQUESTS, list.filter((r) => r.id !== id));
  return req;
}
