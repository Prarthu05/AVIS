import { NextResponse } from "next/server";
import { submitForApproval } from "@/lib/vaStore";
import { requireRole } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

export async function POST(_req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await requireRole(["engineer"]);
  if (session instanceof NextResponse) return session;
  const result = submitForApproval(id, session.name);
  if ("error" in result) return NextResponse.json(result, { status: 400 });
  audit(session.name, "va.submit", { docId: id, version: result.version });
  return NextResponse.json(result);
}
