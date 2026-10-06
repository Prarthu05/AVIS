import type { CheckStat, GroupRow, Kpis, ParetoRow, StepTimeRow, UnitSummary, Bucket } from "@/lib/analytics";
import type { Station, StationEvent, VaDocument, Product, FaultResolution } from "@/lib/domain";

export interface AnalyticsResponse {
  range: { key: string; from: string; to: string };
  kpis: Kpis;
  series: Bucket[];
  paretoByCode: ParetoRow[];
  paretoByPoint: ParetoRow[];
  checks: CheckStat[];
  stepTimes: StepTimeRow[];
  byStation: GroupRow[];
  byProduct: GroupRow[];
  byOperator: GroupRow[];
  recentUnits: UnitSummary[];
}

export type StationRow = Station & { online: boolean; vaInSync: boolean };
export type FaultRow = StationEvent & { resolution: FaultResolution | null };
export type ProductRow = Product & { publishedVersion: number | null; publishedDocId: string | null; pending: number; drafts: number; versions: number };
export type DocRow = VaDocument & { productName: string };
export type { UnitSummary, StationEvent, VaDocument, Product };
