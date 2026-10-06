import { NextResponse } from "next/server";
import { serveVaFile } from "@/lib/vaFiles";
import { getSession } from "@/lib/requireSession";

// Page images / original PDF for the dashboard's own previews (session required).
export async function GET(_req: Request, { params }: { params: Promise<{ id: string; file: string }> }) {
  const { id, file } = await params;
  if (!(await getSession())) return NextResponse.json({ error: "not logged in" }, { status: 401 });
  return serveVaFile(id, file);
}
