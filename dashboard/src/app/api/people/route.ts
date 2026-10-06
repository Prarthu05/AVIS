import { NextResponse } from "next/server";
import { createPerson, findPersonByNtid, listPeople } from "@/lib/peopleStore";
import { normalizeNtid } from "@/lib/auth";
import { getSession } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

// Admin-only (SENSITIVE_API_ACCESS covers /api/people).
export async function GET() {
  return NextResponse.json(listPeople());
}

export async function POST(req: Request) {
  const session = await getSession();
  const body = (await req.json()) as { name?: string; email?: string; ntid?: string };
  const ntid = body.ntid ? normalizeNtid(body.ntid) : undefined;
  if (!body.name?.trim()) return NextResponse.json({ error: "name is required" }, { status: 400 });
  if (body.ntid && !ntid) return NextResponse.json({ error: "invalid NTID" }, { status: 400 });
  if (ntid && findPersonByNtid(ntid)) return NextResponse.json({ error: "someone already has that NTID" }, { status: 409 });
  const person = createPerson({ name: body.name.trim(), email: (body.email ?? "").trim(), ntid: ntid ?? undefined, roles: [] });
  audit(session?.name ?? "?", "person.create", { personId: person.id, ntid });
  return NextResponse.json(person, { status: 201 });
}
