"use client";

import { useState } from "react";

// Hand-built SVG charts, same approach as DowntimeApp's charts.tsx (no chart
// library), themed through CSS variables so they follow the light/dark toggle.
// Series colours are --chart-ok / --chart-ng (CVD-validated, see globals.css);
// every chart has a legend or title naming its series and a hover tooltip.
const GRID = "var(--color-neutral-800)";
const AXIS = "var(--color-neutral-500)";
const OK = "var(--chart-ok)";
const NG = "var(--chart-ng)";
const MONO = "var(--font-mono), ui-monospace, monospace";

function niceMax(value: number): number {
  if (value <= 0) return 4;
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const step = value / magnitude <= 5 ? magnitude / 2 : magnitude;
  return Math.max(4, Math.ceil(value / step) * step);
}

export interface StackPoint {
  label: string;
  confirmed: number;
  notConfirmed: number;
  reworks: number;
}

/** Finished units per hour/day: confirmed stacked under not-confirmed, 2px gap between segments. */
export function ThroughputChart({ points }: { points: StackPoint[] }) {
  const [hover, setHover] = useState<number | null>(null);
  const W = 720;
  const H = 220;
  const padL = 34;
  const padB = 22;
  const padT = 10;
  const padR = 8;
  const chartW = W - padL - padR;
  const chartH = H - padT - padB;
  const max = niceMax(Math.max(0, ...points.map((p) => p.confirmed + p.notConfirmed)));
  const n = Math.max(1, points.length);
  const slot = chartW / n;
  const barW = Math.max(3, Math.min(28, slot * 0.6));
  const labelEvery = Math.max(1, Math.ceil(n / 12));
  const y = (v: number) => padT + chartH * (1 - v / max);
  const h = hover !== null ? points[hover] : null;

  return (
    <div>
      <Legend items={[{ color: OK, label: "Confirmed OK" }, { color: NG, label: "Not confirmed" }]} />
      <div className="relative">
        <svg viewBox={`0 0 ${W} ${H}`} className="w-full" role="img" aria-label="Finished units per period, confirmed vs not confirmed" onMouseLeave={() => setHover(null)}>
          {[0, 0.5, 1].map((t) => (
            <g key={t}>
              <line x1={padL} x2={W - padR} y1={y(max * t)} y2={y(max * t)} stroke={GRID} strokeOpacity={0.6} />
              <text x={padL - 6} y={y(max * t)} textAnchor="end" dominantBaseline="middle" fontSize={9} fill={AXIS} fontFamily={MONO}>
                {Math.round(max * t)}
              </text>
            </g>
          ))}
          {points.map((p, i) => {
            const x = padL + slot * i + (slot - barW) / 2;
            const okTop = y(p.confirmed);
            const ngTop = y(p.confirmed + p.notConfirmed);
            return (
              <g key={p.label + i} onMouseEnter={() => setHover(i)}>
                <rect x={padL + slot * i} y={padT} width={slot} height={chartH} fill="transparent" />
                {hover === i && <rect x={padL + slot * i} y={padT} width={slot} height={chartH} fill="var(--color-neutral-800)" opacity={0.4} />}
                {p.confirmed > 0 && <rect x={x} y={okTop} width={barW} height={Math.max(1, padT + chartH - okTop)} rx={2} fill={OK} />}
                {p.notConfirmed > 0 && <rect x={x} y={ngTop} width={barW} height={Math.max(1, okTop - ngTop - (p.confirmed > 0 ? 2 : 0))} rx={2} fill={NG} />}
                {i % labelEvery === 0 && (
                  <text x={padL + slot * i + slot / 2} y={H - 6} textAnchor="middle" fontSize={9} fill={AXIS} fontFamily={MONO}>
                    {p.label}
                  </text>
                )}
              </g>
            );
          })}
          <line x1={padL} x2={W - padR} y1={padT + chartH} y2={padT + chartH} stroke={AXIS} strokeOpacity={0.5} />
        </svg>
        {h && (
          <div className="pointer-events-none absolute right-2 top-0 rounded-lg border border-neutral-700 bg-neutral-900 px-3 py-2 text-xs shadow-xl">
            <div className="mb-1 font-mono font-semibold text-neutral-200">{h.label}</div>
            <div className="text-neutral-300">Confirmed OK: <b>{h.confirmed}</b></div>
            <div className="text-neutral-300">Not confirmed: <b>{h.notConfirmed}</b></div>
            <div className="text-neutral-500">Reworks: {h.reworks}</div>
          </div>
        )}
      </div>
    </div>
  );
}

export function Legend({ items }: { items: { color: string; label: string }[] }) {
  return (
    <div className="mb-2 flex flex-wrap gap-4 text-xs text-neutral-400">
      {items.map((i) => (
        <span key={i.label} className="flex items-center gap-1.5">
          <span className="inline-block h-2.5 w-2.5 rounded-sm" style={{ background: i.color }} />
          {i.label}
        </span>
      ))}
    </div>
  );
}

/** Ranked horizontal bars (Pareto) - one hue, value + share labelled at the bar end. */
export function HBars({ rows, unit = "" }: { rows: { label: string; value: number; sub?: string }[]; unit?: string }) {
  const max = Math.max(1, ...rows.map((r) => r.value));
  if (rows.length === 0) return <p className="py-4 text-center text-sm text-neutral-500">Nothing recorded in this range.</p>;
  return (
    <div className="flex flex-col gap-1.5">
      {rows.map((r) => (
        <div key={r.label} className="group grid grid-cols-[minmax(0,11rem)_1fr_auto] items-center gap-2 text-xs" title={`${r.label}: ${r.value}${unit}${r.sub ? ` (${r.sub})` : ""}`}>
          <span className="truncate text-neutral-300">{r.label}</span>
          <span className="h-3 rounded-r-sm bg-neutral-800">
            <span className="block h-3 rounded-r-sm group-hover:opacity-80" style={{ width: `${(100 * r.value) / max}%`, background: "var(--chart-neutral)" }} />
          </span>
          <span className="min-w-16 whitespace-nowrap text-right font-mono text-neutral-400">
            {r.value}
            {unit} {r.sub && <span className="text-neutral-600">{r.sub}</span>}
          </span>
        </div>
      ))}
    </div>
  );
}
