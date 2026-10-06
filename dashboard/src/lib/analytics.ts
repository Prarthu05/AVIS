import type { FaultResolution, StationEvent } from "./domain";

// Pure functions over station events - no file access, so they're unit
// tested directly (analytics.test.ts). Every number on the dashboard is
// derived here from the raw events; nothing is pre-aggregated on write.

export type UnitOutcome = "confirmed" | "not_confirmed" | "abandoned" | "in_progress";

export interface CheckSummary {
  firstResult?: "OK" | "NG";
  attempts: number;
  failures: number;
  lastResult?: "OK" | "NG";
  lastValue?: string;
}

export interface StepVisit {
  step: number;
  comment?: string;
  at: string;
  durationSec?: number;
}

export interface UnitSummary {
  unitId: string;
  assetId?: string;
  station: string;
  product?: string;
  material?: string;
  ntid?: string;
  wipId?: number;
  program?: string;
  startedAt: string;
  endedAt?: string;
  cycleSec?: number;
  outcome: UnitOutcome;
  escalated: boolean;
  firstPass: boolean;
  reworks: number;
  checks: Record<string, CheckSummary>;
  steps: StepVisit[];
  faults: number;
}

const END_TYPES: Record<string, UnitOutcome> = {
  unit_confirmed: "confirmed",
  unit_not_confirmed: "not_confirmed",
  unit_abandoned: "abandoned",
};

/** One summary per unit (correlation id), built from all of its events. Events must be oldest-first. */
export function summarizeUnits(events: StationEvent[]): UnitSummary[] {
  const units = new Map<string, UnitSummary>();

  for (const e of events) {
    if (!e.unitId) continue;
    let u = units.get(e.unitId);
    if (!u) {
      u = {
        unitId: e.unitId,
        station: e.station,
        startedAt: e.at,
        outcome: "in_progress",
        escalated: false,
        firstPass: true,
        reworks: 0,
        checks: {},
        steps: [],
        faults: 0,
      };
      units.set(e.unitId, u);
    }
    u.assetId ??= e.assetId;
    u.product ??= e.product;
    u.material ??= e.material;
    u.ntid ??= e.ntid;
    u.wipId ??= e.wipId;
    u.program ??= e.program;
    if (e.type === "unit_started") u.startedAt = e.at;

    switch (e.type) {
      case "step_changed":
        if (e.step !== undefined) {
          const prev = u.steps[u.steps.length - 1];
          if (prev && prev.durationSec === undefined) prev.durationSec = secondsBetween(prev.at, e.at);
          if (!prev || prev.step !== e.step) u.steps.push({ step: e.step, comment: e.stepComment, at: e.at });
        }
        if (e.program) u.program = e.program;
        break;
      case "check_result": {
        const name = e.check ?? "Check";
        const c = (u.checks[name] ??= { attempts: 0, failures: 0 });
        c.attempts++;
        c.firstResult ??= e.result;
        c.lastResult = e.result;
        c.lastValue = e.value;
        if (e.result === "NG") {
          c.failures++;
          u.reworks++;
          u.firstPass = false;
        }
        break;
      }
      case "escalated":
        u.escalated = true;
        u.firstPass = false;
        break;
      case "fault":
        u.faults++;
        break;
      default:
        if (END_TYPES[e.type]) {
          u.outcome = END_TYPES[e.type];
          u.endedAt = e.at;
          u.cycleSec = secondsBetween(u.startedAt, e.at);
          const last = u.steps[u.steps.length - 1];
          if (last && last.durationSec === undefined) last.durationSec = secondsBetween(last.at, e.at);
          if (u.outcome !== "confirmed") u.firstPass = false;
        }
    }
  }
  return [...units.values()].sort((a, b) => b.startedAt.localeCompare(a.startedAt));
}

function secondsBetween(a: string, b: string): number {
  return Math.max(0, Math.round((Date.parse(b) - Date.parse(a)) / 1000));
}

export interface Kpis {
  started: number;
  confirmed: number;
  notConfirmed: number;
  abandoned: number;
  inProgress: number;
  finished: number;
  /** % of finished units confirmed with no NG and no escalation. */
  fpy: number | null;
  /** % of finished units confirmed OK (after rework). */
  yieldPct: number | null;
  reworks: number;
  escalations: number;
  avgCycleSec: number | null;
  faults: number;
  openFaults: number;
  criticalFaults: number;
}

export function computeKpis(units: UnitSummary[], faults: StationEvent[], resolutions: Record<string, FaultResolution>): Kpis {
  const finishedUnits = units.filter((u) => u.outcome !== "in_progress");
  const confirmed = finishedUnits.filter((u) => u.outcome === "confirmed");
  const cycles = confirmed.map((u) => u.cycleSec).filter((s): s is number => s !== undefined);
  const pct = (n: number) => (finishedUnits.length === 0 ? null : Math.round((1000 * n) / finishedUnits.length) / 10);
  return {
    started: units.length,
    confirmed: confirmed.length,
    notConfirmed: finishedUnits.filter((u) => u.outcome === "not_confirmed").length,
    abandoned: finishedUnits.filter((u) => u.outcome === "abandoned").length,
    inProgress: units.length - finishedUnits.length,
    finished: finishedUnits.length,
    fpy: pct(confirmed.filter((u) => u.firstPass).length),
    yieldPct: pct(confirmed.length),
    reworks: units.reduce((n, u) => n + u.reworks, 0),
    escalations: units.filter((u) => u.escalated).length,
    avgCycleSec: cycles.length ? Math.round(cycles.reduce((a, b) => a + b, 0) / cycles.length) : null,
    faults: faults.length,
    openFaults: faults.filter((f) => isOpenFault(f, resolutions)).length,
    criticalFaults: faults.filter((f) => f.severity === "Critical").length,
  };
}

/** Info-level faults are notices, not problems - they never sit in the open queue. */
export function isOpenFault(f: StationEvent, resolutions: Record<string, FaultResolution>): boolean {
  return f.severity !== "Info" && !resolutions[f.id];
}

export interface Bucket {
  key: string;
  label: string;
  confirmed: number;
  notConfirmed: number;
  reworks: number;
}

/** Finished units per hour (local hours of the day) or per day. */
export function timeSeries(units: UnitSummary[], from: Date, to: Date, by: "hour" | "day", tzOffsetMinutes = 0): Bucket[] {
  const buckets = new Map<string, Bucket>();
  const keyOf = (iso: string) => {
    const local = new Date(Date.parse(iso) - tzOffsetMinutes * 60_000);
    return by === "hour" ? local.toISOString().slice(0, 13) : local.toISOString().slice(0, 10);
  };
  const step = by === "hour" ? 3600_000 : 86_400_000;
  for (let t = from.getTime(); t <= to.getTime() && buckets.size < 800; t += step) {
    const key = keyOf(new Date(t).toISOString());
    if (!buckets.has(key)) buckets.set(key, { key, label: by === "hour" ? key.slice(11, 13) + ":00" : key.slice(5), confirmed: 0, notConfirmed: 0, reworks: 0 });
  }
  for (const u of units) {
    if (!u.endedAt) continue;
    const b = buckets.get(keyOf(u.endedAt));
    if (!b) continue;
    if (u.outcome === "confirmed") b.confirmed++;
    else b.notConfirmed++;
    b.reworks += u.reworks;
  }
  return [...buckets.values()];
}

export interface ParetoRow {
  key: string;
  label: string;
  count: number;
  open: number;
  share: number;
}

export function faultPareto(faults: StationEvent[], resolutions: Record<string, FaultResolution>, by: "code" | "point" | "station"): ParetoRow[] {
  const rows = new Map<string, ParetoRow>();
  for (const f of faults) {
    const key = by === "code" ? f.faultCode ?? "?" : by === "point" ? f.integrationPoint ?? "?" : f.station;
    const label = by === "code" ? `${f.faultCode ?? "?"} ${f.faultTitle ?? ""}`.trim() : key;
    const r = rows.get(key) ?? { key, label, count: 0, open: 0, share: 0 };
    r.count++;
    if (isOpenFault(f, resolutions)) r.open++;
    rows.set(key, r);
  }
  const total = faults.length || 1;
  return [...rows.values()].map((r) => ({ ...r, share: Math.round((1000 * r.count) / total) / 10 })).sort((a, b) => b.count - a.count);
}

export interface CheckStat {
  check: string;
  units: number;
  attempts: number;
  failures: number;
  /** % of units whose first attempt at this check was NG. */
  firstAttemptNgPct: number;
  /** % of attempts that were NG. */
  ngRatePct: number;
  escalatedUnits: number;
}

export function checkStats(units: UnitSummary[]): CheckStat[] {
  const stats = new Map<string, CheckStat & { firstNg: number }>();
  for (const u of units) {
    for (const [name, c] of Object.entries(u.checks)) {
      const s = stats.get(name) ?? { check: name, units: 0, attempts: 0, failures: 0, firstAttemptNgPct: 0, ngRatePct: 0, escalatedUnits: 0, firstNg: 0 };
      s.units++;
      s.attempts += c.attempts;
      s.failures += c.failures;
      if (c.firstResult === "NG") s.firstNg++;
      if (u.escalated && c.failures > 0) s.escalatedUnits++;
      stats.set(name, s);
    }
  }
  return [...stats.values()]
    .map(({ firstNg, ...s }) => ({
      ...s,
      firstAttemptNgPct: s.units ? Math.round((1000 * firstNg) / s.units) / 10 : 0,
      ngRatePct: s.attempts ? Math.round((1000 * s.failures) / s.attempts) / 10 : 0,
    }))
    .sort((a, b) => b.failures - a.failures);
}

export interface StepTimeRow {
  product: string;
  step: number;
  comment?: string;
  visits: number;
  avgSec: number;
  medianSec: number;
  maxSec: number;
}

/** How long operators spend on each LightGuide step, per product - the bottleneck view. */
export function stepTimes(units: UnitSummary[]): StepTimeRow[] {
  const groups = new Map<string, { product: string; step: number; comment?: string; secs: number[] }>();
  for (const u of units) {
    for (const s of u.steps) {
      if (s.durationSec === undefined) continue;
      const product = u.product ?? u.program ?? "?";
      const key = `${product}::${s.step}`;
      const g = groups.get(key) ?? { product, step: s.step, comment: s.comment, secs: [] };
      g.secs.push(s.durationSec);
      g.comment ??= s.comment;
      groups.set(key, g);
    }
  }
  return [...groups.values()]
    .map((g) => {
      const sorted = [...g.secs].sort((a, b) => a - b);
      return {
        product: g.product,
        step: g.step,
        comment: g.comment,
        visits: sorted.length,
        avgSec: Math.round(sorted.reduce((a, b) => a + b, 0) / sorted.length),
        medianSec: sorted[Math.floor(sorted.length / 2)],
        maxSec: sorted[sorted.length - 1],
      };
    })
    .sort((a, b) => a.product.localeCompare(b.product) || a.step - b.step);
}

export interface GroupRow {
  key: string;
  units: number;
  confirmed: number;
  finished: number;
  fpy: number | null;
  yieldPct: number | null;
  reworks: number;
  escalations: number;
  avgCycleSec: number | null;
}

/** KPIs split by station, product or operator. */
export function groupBy(units: UnitSummary[], by: "station" | "product" | "ntid"): GroupRow[] {
  const groups = new Map<string, UnitSummary[]>();
  for (const u of units) {
    const key = (by === "station" ? u.station : by === "product" ? u.product ?? u.material : u.ntid) ?? "?";
    groups.set(key, [...(groups.get(key) ?? []), u]);
  }
  return [...groups.entries()]
    .map(([key, list]) => {
      const k = computeKpis(list, [], {});
      return { key, units: list.length, confirmed: k.confirmed, finished: k.finished, fpy: k.fpy, yieldPct: k.yieldPct, reworks: k.reworks, escalations: k.escalations, avgCycleSec: k.avgCycleSec };
    })
    .sort((a, b) => b.units - a.units);
}

export function formatDuration(sec: number | null | undefined): string {
  if (sec === null || sec === undefined) return "–";
  if (sec < 60) return `${sec}s`;
  const m = Math.floor(sec / 60);
  const s = sec % 60;
  if (m < 60) return `${m}m ${String(s).padStart(2, "0")}s`;
  return `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, "0")}m`;
}
