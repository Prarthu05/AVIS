import { NextResponse } from "next/server";
import { addAccessRequest, getPerson, listAccessRequests } from "@/lib/peopleStore";
import { ALL_ROLES, type Role } from "@/lib/domain";
import { getSession } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

// Anyone signed in can ask for a role (App Hub's "request access"); an Admin approves it.
export async function GET() {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const all = listAccessRequests();
  return NextResponse.json(session.roles.includes("admin") ? all : all.filter((r) => r.personId === session.personId));
}

export async function POST(req: Request) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const body = (await req.json()) as { role?: string; note?: string };
  const role = body.role as Role;
  if (!ALL_ROLES.includes(role) || role === "admin") return NextResponse.json({ error: "choose Approver, Engineer, Technician or Viewer" }, { status: 400 });
  const person = getPerson(session.personId);
  if (!person) return NextResponse.json({ error: "not found" }, { status: 404 });
  if (person.roles?.includes(role)) return NextResponse.json({ error: "you already have that role" }, { status: 409 });
  const request = addAccessRequest(person, role, body.note ?? "");
  audit(session.name, "access.request", { role });
  return NextResponse.json(request, { status: 201 });
}
