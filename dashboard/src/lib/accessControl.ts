import type { Role } from "./domain";

// Same scheme as DowntimeApp's accessControl.ts: longest-matching prefix
// wins, "admin" always passes, an unmapped page is admin-only (fail closed).
// Shared by middleware.ts (enforcement) and GlobalNav.tsx (which links to show).
export interface PageAccessEntry {
  prefix: string;
  roles: Role[];
}

const EVERYONE: Role[] = ["approver", "engineer", "technician", "viewer"];

export const PAGE_ACCESS: PageAccessEntry[] = [
  { prefix: "/admin/access", roles: [] },
  { prefix: "/admin/stations", roles: [] },
  { prefix: "/admin/audit", roles: [] },
  { prefix: "/approvals", roles: ["approver"] },
  { prefix: "/va", roles: ["approver", "engineer", "technician", "viewer"] },
  { prefix: "/faults", roles: EVERYONE },
  { prefix: "/units", roles: EVERYONE },
  { prefix: "/stations", roles: EVERYONE },
  { prefix: "/analytics", roles: EVERYONE },
  { prefix: "/request-access", roles: EVERYONE },
  { prefix: "/", roles: EVERYONE },
];

// API prefixes that need a narrower check than "any signed-in role".
// Finer per-method checks (e.g. only engineers may upload) live in the
// route handlers via requireRole().
export const SENSITIVE_API_ACCESS: PageAccessEntry[] = [
  { prefix: "/api/admin", roles: [] },
  { prefix: "/api/people", roles: [] },
];

function matches(pathname: string, prefix: string): boolean {
  if (prefix === "/") return pathname === "/";
  return pathname === prefix || pathname.startsWith(prefix + "/");
}

function bestMatch(pathname: string, table: PageAccessEntry[]): PageAccessEntry | null {
  const candidates = table.filter((e) => matches(pathname, e.prefix));
  if (candidates.length === 0) return null;
  return candidates.reduce((best, e) => (e.prefix.length > best.prefix.length ? e : best));
}

export function pageAccessAllowed(pathname: string, roles: Role[]): boolean {
  if (roles.includes("admin")) return true;
  const entry = bestMatch(pathname, PAGE_ACCESS);
  if (!entry) return false;
  return entry.roles.some((r) => roles.includes(r));
}

export function sensitiveApiAllowed(pathname: string, roles: Role[]): boolean {
  if (roles.includes("admin")) return true;
  const entry = bestMatch(pathname, SENSITIVE_API_ACCESS);
  if (!entry) return true;
  return entry.roles.some((r) => roles.includes(r));
}

export function hasAnyRole(sessionRoles: Role[], allowed: Role[]): boolean {
  return sessionRoles.includes("admin") || sessionRoles.some((r) => allowed.includes(r));
}
