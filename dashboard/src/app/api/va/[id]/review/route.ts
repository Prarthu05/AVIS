import { NextResponse } from "next/server";
import { getDocument, reviewDocument } from "@/lib/vaStore";
import { requireRole } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

// body: { approve: boolean, comment?: string }. Approvers (or Admin) only.
// Nobody approves their own upload - a second pair of eyes is the point.
export async function POST(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await requireRole(["approver"]);
  if (session instanceof NextResponse) return session;
  const body = (await req.json()) as { approve?: boolean; comment?: string };
  const doc = getDocument(id);
  if (!doc) return NextResponse.json({ error: "not found" }, { status: 404 });
  if (body.approve && doc.uploadedBy === session.name && !process.env.AVIS_ALLOW_SELF_APPROVAL) {
    return NextResponse.json({ error: "you uploaded this version - another Approver has to approve it" }, { status: 403 });
  }
  if (!body.approve && !body.comment?.trim()) return NextResponse.json({ error: "say why it's rejected" }, { status: 400 });
  const result = reviewDocument(id, !!body.approve, session.name, body.comment ?? "");
  if ("error" in result) return NextResponse.json(result, { status: 400 });
  audit(session.name, body.approve ? "va.approve" : "va.reject", { docId: id, version: result.version, comment: body.comment ?? "" });
  return NextResponse.json(result);
}
