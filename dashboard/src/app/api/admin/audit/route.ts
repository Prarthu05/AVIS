import { NextResponse } from "next/server";
import { readAudit } from "@/lib/jsonStore";

export async function GET() {
  return NextResponse.json(readAudit());
}
