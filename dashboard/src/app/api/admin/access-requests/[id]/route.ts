import { NextResponse } from "next/server";
import { resolveAccessRequest } from "@/lib/peopleStore";
import { getSession } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

// Admin-only (/api/admin). body: { approve: boolean }
export async function POST(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await getSession();
  const body = (await req.json()) as { approve?: boolean };
  const request = resolveAccessRequest(id, !!body.approve);
  if (!request) return NextResponse.json({ error: "not found" }, { status: 404 });
  audit(session?.name ?? "?", body.approve ? "access.approve" : "access.dismiss", { person: request.personName, role: request.role });
  return NextResponse.json({ ok: true });
}
