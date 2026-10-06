"use client";

import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";

// Building blocks in DowntimeApp's visual language: mono uppercase
// eyebrows, neutral-900 cards with neutral-800 borders, indigo primary.

export function PageHeader({ eyebrow, title, children }: { eyebrow: string; title: string; children?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <div className="mb-1 font-mono text-[11px] uppercase tracking-wider text-neutral-500">{eyebrow}</div>
        <h1 className="text-2xl font-bold">{title}</h1>
      </div>
      {children && <div className="flex flex-wrap items-center gap-2">{children}</div>}
    </div>
  );
}

export function Page({ children, wide = true }: { children: ReactNode; wide?: boolean }) {
  return <div className={`mx-auto ${wide ? "max-w-7xl" : "max-w-4xl"} px-5 py-8 sm:px-8`}>{children}</div>;
}

export function Card({ title, children, className = "", action }: { title?: string; children: ReactNode; className?: string; action?: ReactNode }) {
  return (
    <section className={`rounded-xl border border-neutral-800 bg-neutral-900 p-4 ${className}`}>
      {(title || action) && (
        <div className="mb-3 flex items-center justify-between gap-2">
          {title && <h2 className="font-mono text-[11px] font-semibold uppercase tracking-wider text-neutral-400">{title}</h2>}
          {action}
        </div>
      )}
      {children}
    </section>
  );
}

type Tone = "default" | "good" | "warn" | "bad";
const TONE: Record<Tone, string> = {
  default: "text-neutral-50",
  good: "text-emerald-400",
  warn: "text-amber-400",
  bad: "text-red-400",
};

export function KpiTile({ label, value, sub, icon: Icon, tone = "default" }: { label: string; value: string; sub?: string; icon?: LucideIcon; tone?: Tone }) {
  return (
    <div className="rounded-xl border border-neutral-800 bg-neutral-900 px-4 py-3">
      <div className="flex items-center justify-between gap-2">
        <span className="font-mono text-[10.5px] uppercase tracking-wider text-neutral-500">{label}</span>
        {Icon && <Icon size={15} className="text-neutral-600" />}
      </div>
      <div className={`mt-1 font-mono text-2xl font-bold ${TONE[tone]}`}>{value}</div>
      {sub && <div className="mt-0.5 text-[11px] text-neutral-500">{sub}</div>}
    </div>
  );
}

export const RANGES = [
  { key: "today", label: "Today" },
  { key: "24h", label: "24 h" },
  { key: "7d", label: "7 days" },
  { key: "30d", label: "30 days" },
] as const;

export function RangePicker({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <div className="flex rounded-lg border border-neutral-800 bg-neutral-900 p-0.5">
      {RANGES.map((r) => (
        <button
          key={r.key}
          onClick={() => onChange(r.key)}
          className={`rounded-md px-3 py-1.5 text-xs font-semibold ${value === r.key ? "bg-indigo-500 text-white" : "text-neutral-400 hover:text-neutral-100"}`}
        >
          {r.label}
        </button>
      ))}
    </div>
  );
}

export function Select({ value, onChange, options, label }: { value: string; onChange: (v: string) => void; options: { value: string; label: string }[]; label: string }) {
  return (
    <label className="flex items-center gap-2 text-xs text-neutral-500">
      <span className="font-mono uppercase tracking-wider">{label}</span>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="rounded-lg border border-neutral-700 bg-neutral-800 px-2.5 py-1.5 text-sm text-neutral-100 focus:border-indigo-400 focus:outline-none"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}

const PILL: Record<string, string> = {
  confirmed: "bg-emerald-500/20 text-emerald-300",
  approved: "bg-emerald-500/20 text-emerald-300",
  online: "bg-emerald-500/20 text-emerald-300",
  OK: "bg-emerald-500/20 text-emerald-300",
  in_progress: "bg-sky-500/20 text-sky-300",
  pending: "bg-amber-500/20 text-amber-300",
  draft: "bg-neutral-700 text-neutral-300",
  superseded: "bg-neutral-800 text-neutral-500",
  offline: "bg-neutral-800 text-neutral-500",
  Info: "bg-neutral-800 text-neutral-400",
  Warning: "bg-amber-500/20 text-amber-300",
  NG: "bg-red-500/20 text-red-300",
  Error: "bg-red-500/20 text-red-300",
  rejected: "bg-red-500/20 text-red-300",
  not_confirmed: "bg-red-500/20 text-red-300",
  abandoned: "bg-red-500/20 text-red-300",
  Critical: "bg-red-500/40 text-red-200",
  escalated: "bg-red-500/40 text-red-200",
};

const PILL_LABEL: Record<string, string> = { not_confirmed: "not confirmed", in_progress: "in progress" };

export function Pill({ value }: { value: string }) {
  return (
    <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 font-mono text-[10.5px] font-semibold ${PILL[value] ?? "bg-neutral-800 text-neutral-300"}`}>
      {PILL_LABEL[value] ?? value}
    </span>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="py-6 text-center text-sm text-neutral-500">{children}</p>;
}

export function Button({
  children,
  onClick,
  variant = "primary",
  disabled,
  type = "button",
}: {
  children: ReactNode;
  onClick?: () => void;
  variant?: "primary" | "ghost" | "danger";
  disabled?: boolean;
  type?: "button" | "submit";
}) {
  const cls =
    variant === "primary"
      ? "bg-indigo-500 text-white hover:bg-indigo-400"
      : variant === "danger"
        ? "border border-red-500/40 text-red-400 hover:bg-red-500/10"
        : "border border-neutral-700 text-neutral-300 hover:bg-neutral-800";
  return (
    <button type={type} onClick={onClick} disabled={disabled} className={`rounded-lg px-3.5 py-2 text-sm font-semibold disabled:opacity-50 ${cls}`}>
      {children}
    </button>
  );
}

export const inputCls = "rounded-lg border border-neutral-700 bg-neutral-800 px-3 py-2 text-sm text-neutral-100 focus:border-indigo-400 focus:outline-none";

export function fmtTime(iso?: string | null): string {
  if (!iso) return "–";
  const d = new Date(iso);
  const today = new Date().toDateString() === d.toDateString();
  return today ? d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" }) : d.toLocaleString([], { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

export function pct(v: number | null | undefined): string {
  return v === null || v === undefined ? "–" : `${v}%`;
}

/** fetch() JSON helper that surfaces the API's { error } message. */
export async function api<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error((body as { error?: string }).error ?? `Request failed (${res.status})`);
  return body as T;
}

export function postJson<T>(url: string, body: unknown, method = "POST"): Promise<T> {
  return api<T>(url, { method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
}
