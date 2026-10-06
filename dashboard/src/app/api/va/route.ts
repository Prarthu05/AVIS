import { NextResponse } from "next/server";
import { createDraft, getProduct, listDocuments, type UploadFile } from "@/lib/vaStore";
import { getSession, requireRole } from "@/lib/requireSession";
import { audit } from "@/lib/jsonStore";

const MAX_UPLOAD_BYTES = 60 * 1024 * 1024;

// GET ?status=pending - the approval queue; otherwise every version.
export async function GET(req: Request) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  const status = new URL(req.url).searchParams.get("status");
  const docs = listDocuments()
    .filter((d) => !status || d.status === status)
    .map((d) => ({ ...d, productName: getProduct(d.productId)?.name ?? "?" }))
    .sort((a, b) => (b.submittedAt ?? b.uploadedAt).localeCompare(a.submittedAt ?? a.uploadedAt));
  return NextResponse.json(docs);
}

// multipart/form-data: productId, title, file (one PDF, or several images)
export async function POST(req: Request) {
  const session = await requireRole(["engineer"]);
  if (session instanceof NextResponse) return session;
  const form = await req.formData();
  const productId = String(form.get("productId") ?? "");
  const title = String(form.get("title") ?? "");
  const files: UploadFile[] = [];
  let total = 0;
  for (const entry of form.getAll("file")) {
    if (typeof entry === "string") continue;
    total += entry.size;
    if (total > MAX_UPLOAD_BYTES) return NextResponse.json({ error: "upload is larger than 60 MB" }, { status: 413 });
    files.push({ name: entry.name.replace(/[^A-Za-z0-9._ -]/g, "_"), data: Buffer.from(await entry.arrayBuffer()) });
  }
  try {
    const doc = await createDraft(productId, files, title, session.name);
    audit(session.name, "va.upload", { docId: doc.id, productId, version: doc.version, pages: doc.pages.length });
    return NextResponse.json(doc, { status: 201 });
  } catch (e) {
    return NextResponse.json({ error: (e as Error).message }, { status: 400 });
  }
}
