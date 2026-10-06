import { NextResponse } from "next/server";
import { requireRole } from "@/lib/requireSession";
import { reopenFault, resolveFault } from "@/lib/eventStore";
import { audit } from "@/lib/jsonStore";

// Technicians (and engineers/admins) mark a fault resolved with a note, or reopen it.
export async function POST(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await requireRole(["technician", "engineer"]);
  if (session instanceof NextResponse) return session;
  const body = (await req.json().catch(() => ({}))) as { note?: string; reopen?: boolean };
  if (body.reopen) {
    reopenFault(id);
    audit(session.name, "fault.reopen", { eventId: id });
    return NextResponse.json({ ok: true });
  }
  const r = resolveFault(id, session.name, body.note ?? "");
  audit(session.name, "fault.resolve", { eventId: id, note: r.note });
  return NextResponse.json(r);
}
