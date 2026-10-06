import { test } from "node:test";
import assert from "node:assert/strict";
import { checkStats, computeKpis, faultPareto, groupBy, stepTimes, summarizeUnits, timeSeries, formatDuration } from "./analytics.ts";
import type { StationEvent } from "./domain.ts";

let n = 0;
function ev(type: StationEvent["type"], at: string, extra: Partial<StationEvent> = {}): StationEvent {
  return { id: `e${++n}`, type, at: `2026-10-05T${at}Z`, station: "ST-1", ...extra };
}

// Unit A: LJ NG twice then OK, confirmed. Unit B: first pass. Unit C: 5x IV4 NG, escalated, not confirmed. Unit D: still running.
const events: StationEvent[] = [
  ev("unit_started", "08:00:00", { unitId: "A", assetId: "HN1", product: "CVG300", ntid: "111" }),
  ev("step_changed", "08:00:10", { unitId: "A", step: 1 }),
  ev("step_changed", "08:01:10", { unitId: "A", step: 2 }),
  ev("check_result", "08:01:20", { unitId: "A", check: "LJ", result: "NG" }),
  ev("check_result", "08:02:00", { unitId: "A", check: "LJ", result: "NG" }),
  ev("check_result", "08:03:00", { unitId: "A", check: "LJ", result: "OK" }),
  ev("unit_confirmed", "08:04:00", { unitId: "A" }),
  ev("unit_started", "09:00:00", { unitId: "B", assetId: "HN2", product: "CVG300", ntid: "111" }),
  ev("step_changed", "09:00:10", { unitId: "B", step: 1 }),
  ev("step_changed", "09:00:40", { unitId: "B", step: 2 }),
  ev("check_result", "09:01:00", { unitId: "B", check: "LJ", result: "OK" }),
  ev("unit_confirmed", "09:02:00", { unitId: "B" }),
  ev("unit_started", "09:10:00", { unitId: "C", assetId: "HN3", product: "CVG400", ntid: "222", station: "ST-2" }),
  ...[1, 2, 3, 4, 5].map((i) => ev("check_result", `09:1${i}:00`, { unitId: "C", check: "IV4", result: "NG", station: "ST-2" })),
  ev("escalated", "09:15:30", { unitId: "C", station: "ST-2" }),
  ev("unit_not_confirmed", "09:20:00", { unitId: "C", station: "ST-2" }),
  ev("unit_started", "10:00:00", { unitId: "D", assetId: "HN4", product: "CVG300" }),
];

const faults: StationEvent[] = [
  ev("fault", "08:01:20", { faultCode: "LJ-01", faultTitle: "LJ Check Failed", integrationPoint: "LJ", severity: "Warning" }),
  ev("fault", "09:15:30", { faultCode: "ST-01", faultTitle: "Rework Limit Reached", integrationPoint: "Station", severity: "Critical" }),
  ev("fault", "09:30:00", { faultCode: "LJ-01", faultTitle: "LJ Check Failed", integrationPoint: "LJ", severity: "Warning" }),
  ev("fault", "09:31:00", { faultCode: "SCN-01", integrationPoint: "Badge Scanner", severity: "Info" }),
];

test("summarizeUnits builds one journey per correlation id", () => {
  const units = summarizeUnits(events);
  assert.equal(units.length, 4);
  const a = units.find((u) => u.unitId === "A")!;
  assert.equal(a.outcome, "confirmed");
  assert.equal(a.firstPass, false);
  assert.equal(a.reworks, 2);
  assert.equal(a.cycleSec, 240);
  assert.deepEqual(a.checks.LJ, { firstResult: "NG", attempts: 3, failures: 2, lastResult: "OK", lastValue: undefined });
  assert.deepEqual(a.steps.map((s) => [s.step, s.durationSec]), [[1, 60], [2, 170]]);
  const c = units.find((u) => u.unitId === "C")!;
  assert.equal(c.escalated, true);
  assert.equal(c.outcome, "not_confirmed");
  assert.equal(units.find((u) => u.unitId === "D")!.outcome, "in_progress");
});

test("computeKpis: FPY counts only first-pass confirmations, yield counts all confirmations", () => {
  const k = computeKpis(summarizeUnits(events), faults, { [faults[0].id]: { eventId: faults[0].id, resolvedBy: "x", resolvedAt: "", note: "" } });
  assert.equal(k.started, 4);
  assert.equal(k.finished, 3);
  assert.equal(k.confirmed, 2);
  assert.equal(k.notConfirmed, 1);
  assert.equal(k.inProgress, 1);
  assert.equal(k.fpy, 33.3);
  assert.equal(k.yieldPct, 66.7);
  assert.equal(k.reworks, 7);
  assert.equal(k.escalations, 1);
  assert.equal(k.avgCycleSec, 180);
  assert.equal(k.faults, 4);
  assert.equal(k.openFaults, 2, "resolved and Info faults are not open");
  assert.equal(k.criticalFaults, 1);
});

test("computeKpis with no finished units reports null rates, not 0%", () => {
  const k = computeKpis(summarizeUnits([events[events.length - 1]]), [], {});
  assert.equal(k.fpy, null);
  assert.equal(k.yieldPct, null);
});

test("faultPareto groups and sorts by count", () => {
  const rows = faultPareto(faults, {}, "code");
  assert.equal(rows[0].key, "LJ-01");
  assert.equal(rows[0].count, 2);
  assert.equal(rows[0].share, 50);
  assert.equal(faultPareto(faults, {}, "point")[0].key, "LJ");
});

test("checkStats reports first-attempt NG rate per check", () => {
  const stats = checkStats(summarizeUnits(events));
  const iv4 = stats.find((s) => s.check === "IV4")!;
  assert.equal(iv4.failures, 5);
  assert.equal(iv4.escalatedUnits, 1);
  const lj = stats.find((s) => s.check === "LJ")!;
  assert.equal(lj.units, 2);
  assert.equal(lj.firstAttemptNgPct, 50);
  assert.equal(lj.ngRatePct, 50);
});

test("stepTimes averages time on each step per product", () => {
  const rows = stepTimes(summarizeUnits(events));
  const step1 = rows.find((r) => r.product === "CVG300" && r.step === 1)!;
  assert.equal(step1.visits, 2);
  assert.equal(step1.avgSec, 45);
  assert.equal(step1.maxSec, 60);
});

test("timeSeries buckets finished units by hour", () => {
  const series = timeSeries(summarizeUnits(events), new Date("2026-10-05T08:00:00Z"), new Date("2026-10-05T10:59:00Z"), "hour");
  assert.deepEqual(series.map((b) => [b.label, b.confirmed, b.notConfirmed]), [["08:00", 1, 0], ["09:00", 1, 1], ["10:00", 0, 0]]);
});

test("groupBy station / operator", () => {
  const byStation = groupBy(summarizeUnits(events), "station");
  assert.deepEqual(byStation.map((r) => [r.key, r.units, r.confirmed]), [["ST-1", 3, 2], ["ST-2", 1, 0]]);
  assert.equal(groupBy(summarizeUnits(events), "ntid")[0].key, "111");
});

test("formatDuration", () => {
  assert.equal(formatDuration(45), "45s");
  assert.equal(formatDuration(185), "3m 05s");
  assert.equal(formatDuration(3720), "1h 02m");
  assert.equal(formatDuration(null), "–");
});
