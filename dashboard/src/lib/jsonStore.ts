import fs from "fs";
import path from "path";

// Same "JSON file per collection under data/" storage every app on the Pi
// uses (DowntimeApp, App Hub), with one hardening: writes go to a temp file
// and are renamed into place, so a crash or power cut mid-write can never
// leave a half-written (unparseable) file behind.
export const DATA_DIR = process.env.AVIS_DATA_DIR || path.join(process.cwd(), "data");

export function dataPath(...parts: string[]): string {
  return path.join(DATA_DIR, ...parts);
}

export function readJson<T>(file: string, fallback: T): T {
  try {
    return JSON.parse(fs.readFileSync(dataPath(file), "utf-8")) as T;
  } catch {
    return fallback;
  }
}

export function writeJson(file: string, data: unknown): void {
  const target = dataPath(file);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  const tmp = `${target}.${process.pid}.tmp`;
  fs.writeFileSync(tmp, JSON.stringify(data, null, 2) + "\n", "utf-8");
  fs.renameSync(tmp, target);
}

export function appendLine(file: string, data: unknown): void {
  const target = dataPath(file);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.appendFileSync(target, JSON.stringify(data) + "\n", "utf-8");
}

/** Append-only audit trail, like App Hub's audit.log - every admin/approval mutation gets one line. */
export function audit(actor: string, action: string, details: Record<string, unknown> = {}): void {
  appendLine("audit.log", { ts: new Date().toISOString(), actor, action, details });
}

export function readAudit(limit = 300): { ts: string; actor: string; action: string; details: Record<string, unknown> }[] {
  try {
    const lines = fs.readFileSync(dataPath("audit.log"), "utf-8").split("\n").filter(Boolean);
    return lines
      .slice(-limit)
      .reverse()
      .map((l) => {
        try {
          return JSON.parse(l);
        } catch {
          return null;
        }
      })
      .filter(Boolean);
  } catch {
    return [];
  }
}
