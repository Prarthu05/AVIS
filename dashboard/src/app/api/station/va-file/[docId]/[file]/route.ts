import { NextResponse } from "next/server";
import { requireStation } from "@/lib/stationAuth";
import { serveVaFile } from "@/lib/vaFiles";
import { getDocument } from "@/lib/vaStore";

// Stations may only download files of an approved version - never a draft.
export async function GET(req: Request, { params }: { params: Promise<{ docId: string; file: string }> }) {
  const auth = requireStation(req);
  if (auth instanceof NextResponse) return auth;
  const { docId, file } = await params;
  if (getDocument(docId)?.status !== "approved") return NextResponse.json({ error: "not an approved VA" }, { status: 404 });
  return serveVaFile(docId, file);
}
