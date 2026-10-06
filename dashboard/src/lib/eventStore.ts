import fs from "fs";
import { randomUUID } from "crypto";
import type { FaultResolution, Station, StationEvent, StationEventType } from "./domain";
import { appendLine, dataPath, readJson, writeJson } from "./jsonStore";

// Station events land in one append-only JSONL file per day
// (data/events/2026-10-05.jsonl): cheap to write from many stations,
// never rewritten, and a date range only reads the days it needs.
const EVENTS_DIR = "events";
const STATIONS = "stations.json";
const RESOLUTIONS = "fault-resolutions.json";

const TYPES: StationEventType[] = [
  "unit_started",
  "step_changed",
  "check_result",
  "unit_confirmed",
  "unit_not_confirmed",
  "unit_abandoned",
  "escalated",
  "fault",
  "heartbeat",
];

function dayOf(iso: string): string {
  return iso.slice(0, 10);
}

/** Validates and stores a batch from one station. Returns how many were accepted. */
export function ingestEvents(station: string, raw: unknown[]): { accepted: number; rejected: number } {
  const receivedAt = new Date().toISOString();
  let accepted = 0;
  let rejected = 0;
  const seen = recentIds();
  let latestHeartbeat: StationEvent | undefined;

  for (const item of raw.slice(0, 1000)) {
    const e = sanitize(item, station, receivedAt);
    if (!e) {
      rejected++;
      continue;
    }
    // Stations retry a batch until it's acknowledged - never store the same event twice.
    if (seen.has(e.id)) {
      accepted++;
      continue;
    }
    seen.add(e.id);
    if (e.type === "heartbeat") {
      // Heartbeats only update station status - they'd swamp the event log.
      latestHeartbeat = e;
    } else {
      appendLine(`${EVENTS_DIR}/${dayOf(e.at)}.jsonl`, e);
      rememberId(e.id);
    }
    accepted++;
  }
  touchStation(station, receivedAt, latestHeartbeat);
  return { accepted, rejected };
}

function sanitize(item: unknown, station: string, receivedAt: string): StationEvent | null {
  if (!item || typeof item !== "object") return null;
  const e = item as Record<string, unknown>;
  const type = e.type as StationEventType;
  if (!TYPES.includes(type)) return null;
  const at = typeof e.at === "string" && !Number.isNaN(Date.parse(e.at)) ? new Date(e.at).toISOString() : receivedAt;
  const str = (v: unknown, max = 200) => (typeof v === "string" && v.length > 0 ? v.slice(0, max) : undefined);
  const num = (v: unknown) => (typeof v === "number" && Number.isFinite(v) ? v : undefined);
  const measurements =
    e.measurements && typeof e.measurements === "object"
      ? Object.fromEntries(Object.entries(e.measurements as Record<string, unknown>).slice(0, 30).map(([k, v]) => [k.slice(0, 60), typeof v === "string" ? v.slice(0, 100) : null]))
      : undefined;
  const links =
    e.links && typeof e.links === "object"
      ? Object.fromEntries(Object.entries(e.links as Record<string, unknown>).slice(0, 10).map(([k, v]) => [k.slice(0, 30), String(v).slice(0, 20)]))
      : undefined;
  const result = e.result === "OK" || e.result === "NG" ? e.result : undefined;
  const severity = ["Info", "Warning", "Error", "Critical"].includes(e.severity as string) ? (e.severity as StationEvent["severity"]) : undefined;

  return {
    id: str(e.id, 64) ?? randomUUID(),
    type,
    at,
    station,
    receivedAt,
    unitId: str(e.unitId, 64),
    assetId: str(e.assetId, 80),
    ntid: str(e.ntid, 40),
    wipId: num(e.wipId),
    material: str(e.material, 80),
    product: str(e.product, 80),
    program: str(e.program, 120),
    step: num(e.step),
    stepComment: str(e.stepComment, 200),
    check: str(e.check, 40),
    result,
    value: str(e.value, 100),
    attempt: num(e.attempt),
    failures: num(e.failures),
    measurements,
    imagePath: str(e.imagePath, 300),
    faultCode: str(e.faultCode, 20),
    faultTitle: str(e.faultTitle, 80),
    integrationPoint: str(e.integrationPoint, 40),
    severity,
    message: str(e.message, 1000),
    phase: str(e.phase, 40),
    links,
    appVersion: str(e.appVersion, 40),
  };
}

// Small rolling set of recent event ids for de-duplication across retries.
const RECENT_IDS = "recent-event-ids.json";
let recentCache: string[] | null = null;
function recentIds(): Set<string> {
  recentCache ??= readJson<string[]>(RECENT_IDS, []);
  return new Set(recentCache);
}
function rememberId(id: string) {
  recentCache ??= readJson<string[]>(RECENT_IDS, []);
  recentCache.push(id);
  if (recentCache.length > 20000) recentCache = recentCache.slice(-10000);
  writeJson(RECENT_IDS, recentCache);
}

/** Every stored event between two dates (inclusive, by day), oldest first. */
export function readEvents(from: Date, to: Date, filter?: (e: StationEvent) => boolean): StationEvent[] {
  const out: StationEvent[] = [];
  const day = new Date(Date.UTC(from.getUTCFullYear(), from.getUTCMonth(), from.getUTCDate()));
  const end = to.getTime();
  for (let guard = 0; day.getTime() <= end && guard < 400; guard++) {
    const file = dataPath(EVENTS_DIR, `${day.toISOString().slice(0, 10)}.jsonl`);
    if (fs.existsSync(file)) {
      for (const line of fs.readFileSync(file, "utf-8").split("\n")) {
        if (!line) continue;
        try {
          const e = JSON.parse(line) as StationEvent;
          const t = Date.parse(e.at);
          if (t >= from.getTime() && t <= end && (!filter || filter(e))) out.push(e);
        } catch {
          // a torn last line after a power cut - skip it
        }
      }
    }
    day.setUTCDate(day.getUTCDate() + 1);
  }
  return out.sort((a, b) => a.at.localeCompare(b.at));
}

// ---------- Stations ----------
export function listStations(): Station[] {
  return readJson<Station[]>(STATIONS, []).sort((a, b) => a.name.localeCompare(b.name));
}

function touchStation(name: string, at: string, heartbeat?: StationEvent, vaMapVersion?: string) {
  const list = readJson<Station[]>(STATIONS, []);
  let s = list.find((x) => x.name === name);
  if (!s) {
    s = { name, firstSeen: at, lastSeen: at };
    list.push(s);
  }
  s.lastSeen = at;
  if (heartbeat) s.lastHeartbeat = heartbeat;
  if (vaMapVersion) s.vaMapVersion = vaMapVersion;
  writeJson(STATIONS, list);
}

export function recordVaMapFetch(station: string, version: string) {
  touchStation(station, new Date().toISOString(), undefined, version);
}

export function removeStation(name: string) {
  writeJson(STATIONS, readJson<Station[]>(STATIONS, []).filter((s) => s.name !== name));
}

/** A station is online if it has called in within the last 2 minutes (heartbeat every 30 s). */
export function isOnline(s: Station, now = Date.now()): boolean {
  return now - Date.parse(s.lastSeen) < 2 * 60_000;
}

// ---------- Fault resolution (technicians) ----------
export function listResolutions(): Record<string, FaultResolution> {
  return readJson<Record<string, FaultResolution>>(RESOLUTIONS, {});
}

export function resolveFault(eventId: string, by: string, note: string): FaultResolution {
  const all = listResolutions();
  const r: FaultResolution = { eventId, resolvedBy: by, resolvedAt: new Date().toISOString(), note: note.slice(0, 500) };
  all[eventId] = r;
  writeJson(RESOLUTIONS, all);
  return r;
}

export function reopenFault(eventId: string) {
  const all = listResolutions();
  delete all[eventId];
  writeJson(RESOLUTIONS, all);
}
