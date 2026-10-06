import { NextResponse } from "next/server";
import { deleteDraft, getDocument, getProduct, updateDraft } from "@/lib/vaStore";
import type { StepMapping } from "@/lib/domain";
import { getSession, requireRole } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

export async function GET(_req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const doc = getDocument(id);
  if (!doc) return NextResponse.json({ error: "not found" }, { status: 404 });
  return NextResponse.json({ ...doc, product: getProduct(doc.productId) ?? null });
}

export async function PUT(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await requireRole(["engineer"]);
  if (session instanceof NextResponse) return session;
  const body = (await req.json()) as { title?: string; notes?: string; mappings?: StepMapping[]; defaultPages?: number[] };
  const result = updateDraft(id, body);
  if ("error" in result) return NextResponse.json(result, { status: 400 });
  audit(session.name, "va.edit", { docId: id, steps: result.mappings.length });
  return NextResponse.json(result);
}

export async function DELETE(_req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const session = await requireRole(["engineer"]);
  if (session instanceof NextResponse) return session;
  const result = deleteDraft(id);
  if (typeof result === "object") return NextResponse.json(result, { status: 400 });
  if (!result) return NextResponse.json({ error: "not found" }, { status: 404 });
  audit(session.name, "va.delete-draft", { docId: id });
  return NextResponse.json({ ok: true });
}
