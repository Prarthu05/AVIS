import fs from "fs";
import path from "path";
import { NextResponse } from "next/server";
import { documentFilePath } from "./vaStore";

const TYPES: Record<string, string> = { ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".webp": "image/webp", ".gif": "image/gif", ".pdf": "application/pdf" };

export function serveVaFile(id: string, file: string): Response {
  const full = documentFilePath(id, file);
  if (!full) return NextResponse.json({ error: "not found" }, { status: 404 });
  return new Response(new Uint8Array(fs.readFileSync(full)), {
    headers: {
      "Content-Type": TYPES[path.extname(full).toLowerCase()] ?? "application/octet-stream",
      // A document version's files never change once uploaded.
      "Cache-Control": "private, max-age=31536000, immutable",
    },
  });
}
