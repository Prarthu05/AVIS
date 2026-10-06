import { NextResponse } from "next/server";
import { listPeople, setPersonRoles } from "@/lib/peopleStore";
import { ALL_ROLES, type Role } from "@/lib/domain";
import { getSession } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

// Admin-only via the /api/admin prefix (accessControl.ts). Takes the
// person's whole role set at once, like DowntimeApp's set-role route.
export async function POST(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await getSession();
  const body = (await req.json()) as { roles?: string[] };
  if (!Array.isArray(body.roles) || body.roles.some((r) => !ALL_ROLES.includes(r as Role))) {
    return NextResponse.json({ error: `roles must be drawn from: ${ALL_ROLES.join(", ")}` }, { status: 400 });
  }
  const roles = body.roles as Role[];
  // Never let the last Admin remove their own admin role - the app would have no-one able to manage access.
  const admins = listPeople().filter((p) => p.roles?.includes("admin"));
  if (!roles.includes("admin") && admins.length === 1 && admins[0].id === id) {
    return NextResponse.json({ error: "this is the last Admin - give someone else Admin first" }, { status: 400 });
  }
  const person = setPersonRoles(id, roles);
  if (!person) return NextResponse.json({ error: "not found" }, { status: 404 });
  audit(session?.name ?? "?", "person.set-roles", { personId: id, name: person.name, roles });
  return NextResponse.json(person);
}
