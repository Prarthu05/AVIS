import { NextResponse } from "next/server";
import { deletePerson, findPersonByNtid, updatePerson } from "@/lib/peopleStore";
import { normalizeNtid } from "@/lib/auth";
import { getSession } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

export async function PUT(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await getSession();
  const body = (await req.json()) as { name?: string; email?: string; ntid?: string };
  const ntid = body.ntid ? normalizeNtid(body.ntid) : body.ntid === "" ? undefined : null;
  if (ntid === null && body.ntid !== undefined) return NextResponse.json({ error: "invalid NTID" }, { status: 400 });
  const clash = ntid ? findPersonByNtid(ntid) : undefined;
  if (clash && clash.id !== id) return NextResponse.json({ error: "someone else already has that NTID" }, { status: 409 });
  const person = updatePerson(id, {
    ...(body.name !== undefined ? { name: body.name.trim() } : {}),
    ...(body.email !== undefined ? { email: body.email.trim() } : {}),
    ...(body.ntid !== undefined ? { ntid: ntid ?? undefined } : {}),
  });
  if (!person) return NextResponse.json({ error: "not found" }, { status: 404 });
  audit(session?.name ?? "?", "person.update", { personId: id });
  return NextResponse.json(person);
}

export async function DELETE(_req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await getSession();
  if (session?.personId === id) return NextResponse.json({ error: "you can't remove yourself" }, { status: 400 });
  deletePerson(id);
  audit(session?.name ?? "?", "person.delete", { personId: id });
  return NextResponse.json({ ok: true });
}
