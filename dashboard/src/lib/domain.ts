// ---------- People & roles ----------
// Same model as DowntimeApp / App Hub: NTID-only sign-in (shared
// app_hub_id cookie), access entirely by role, a person may hold several.
//   admin      - everything, incl. People & Access and granting Approver
//   approver   - approves/rejects VA versions before stations use them
//   engineer   - uploads VAs, edits product + step -> page mapping
//   technician - works the fault feed (mark faults resolved)
//   viewer     - read-only dashboards (default for a new NTID)
export type Role = "admin" | "approver" | "engineer" | "technician" | "viewer";

export const ALL_ROLES: Role[] = ["admin", "approver", "engineer", "technician", "viewer"];

const ROLE_LABELS: Record<Role, string> = {
  admin: "Admin",
  approver: "Approver",
  engineer: "Engineer",
  technician: "Technician",
  viewer: "Viewer",
};

export function roleLabel(role: Role): string {
  return ROLE_LABELS[role];
}

export function roleLabels(roles: Role[]): string {
  return roles.length > 0 ? roles.map(roleLabel).join(", ") : "No access yet";
}

export interface Person {
  id: string;
  name: string;
  email: string;
  ntid?: string;
  roles?: Role[];
}

export interface AccessRequest {
  id: string;
  personId: string;
  personName: string;
  ntid?: string;
  role: Role;
  note: string;
  createdAt: string;
}

// ---------- Visual aids ----------
export interface Product {
  id: string;
  /** Model name as the station knows it (INI [RECIPE] model / LightGuide program). */
  name: string;
  /** iFactory WIP material / part numbers that run as this product. */
  partNumbers: string[];
  description: string;
  createdAt: string;
}

export type VaStatus = "draft" | "pending" | "approved" | "rejected" | "superseded";

export interface VaPage {
  n: number; // 1-based page number
  file: string; // file name inside the document's folder
}

/** Which pages the station shows while LightGuide is on a step. Several rows may share a step. */
export interface StepMapping {
  step: number;
  pages: number[];
}

export interface VaDocument {
  id: string;
  productId: string;
  version: number;
  title: string;
  originalFile: string;
  kind: "pdf" | "images";
  pages: VaPage[];
  /** True when the PDF could not be rendered to page images (pdftoppm missing) - stations open the PDF at the page. */
  pdfOnly: boolean;
  mappings: StepMapping[];
  /** Pages shown for a step that has no mapping (e.g. an overview). Empty = keep the last page shown. */
  defaultPages: number[];
  status: VaStatus;
  notes: string;
  uploadedBy: string;
  uploadedAt: string;
  submittedBy?: string;
  submittedAt?: string;
  reviewedBy?: string;
  reviewedAt?: string;
  reviewComment?: string;
}

// ---------- Station events (pushed by the AVIS middleware) ----------
export type StationEventType =
  | "unit_started"
  | "step_changed"
  | "check_result"
  | "unit_confirmed"
  | "unit_not_confirmed"
  | "unit_abandoned"
  | "escalated"
  | "fault"
  | "heartbeat";

export interface StationEvent {
  id: string;
  type: StationEventType;
  at: string; // ISO time on the station
  station: string;
  receivedAt?: string;
  /** Correlation id - one per unit (NTID + AssetID joined), on every event for that unit. */
  unitId?: string;
  assetId?: string;
  ntid?: string;
  wipId?: number;
  material?: string;
  product?: string;
  program?: string;
  step?: number;
  stepComment?: string;
  check?: string;
  result?: "OK" | "NG";
  value?: string;
  attempt?: number;
  failures?: number;
  measurements?: Record<string, string | null>;
  imagePath?: string;
  // faults
  faultCode?: string;
  faultTitle?: string;
  integrationPoint?: string;
  severity?: "Info" | "Warning" | "Error" | "Critical";
  message?: string;
  // heartbeat
  phase?: string;
  links?: Record<string, string>;
  appVersion?: string;
}

export interface FaultResolution {
  eventId: string;
  resolvedBy: string;
  resolvedAt: string;
  note: string;
}

export interface Station {
  name: string;
  firstSeen: string;
  lastSeen: string;
  lastHeartbeat?: StationEvent;
  vaMapVersion?: string;
}
