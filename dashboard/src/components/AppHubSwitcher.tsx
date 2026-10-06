"use client";

import { useEffect, useRef, useState } from "react";
import {
  LayoutGrid,
  Send,
  AppWindow,
  Radio,
  TicketCheck,
  Gauge,
  FolderKanban,
  Ruler,
  Wrench,
  Boxes,
  ClipboardList,
  type LucideIcon,
} from "lucide-react";

const APP_HUB_URL = process.env.NEXT_PUBLIC_APP_HUB_URL || "http://10.77.193.155:4001";

// The name THIS app is registered under on App Hub's own catalog (Super
// Admin console) -- a literal on purpose, same convention as
// teamsNotify.ts's APP_NAME: names this app, doesn't get inherited from
// anywhere else's copy of this component.
const CURRENT_APP_NAME = "AVIS";

// Same fixed icon set App Hub's own icons.jsx maps `app.icon` against --
// an app registered with a name outside this set (or none) falls back to
// AppWindow, same as App Hub's own switcher does.
const ICONS: Record<string, LucideIcon> = { AppWindow, Radio, TicketCheck, Gauge, FolderKanban, Ruler, Wrench, Boxes, ClipboardList };
function AppIcon({ name }: { name?: string }) {
  const Icon = (name && ICONS[name]) || AppWindow;
  return <Icon size={16} />;
}

interface HubApp {
  id: string;
  name: string;
  url: string;
  icon?: string;
  health?: { ok: boolean } | null;
}

// Cross-app switcher, matching Shift Passdown's own AppHubBar -- data
// comes from App Hub's own /api/my-apps via a same-origin proxy
// (/api/apphub-apps, see that route for why it's a proxy and not a
// direct browser call), the exact same access-aware list App Hub's own
// landing page renders, so a newly granted app shows up here the moment
// it shows up there.
export default function AppHubSwitcher() {
  const [apps, setApps] = useState<HubApp[]>([]);
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    fetch("/api/apphub-apps")
      .then((r) => r.json())
      .then((data) => setApps(data?.apps ?? []))
      .catch(() => setApps([]));
  }, []);

  useEffect(() => {
    if (!open) return;
    function onDocClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, [open]);

  if (apps.length === 0) return null;

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen((o) => !o)}
        title="Switch apps"
        className={`flex items-center justify-center rounded-lg p-1.5 transition-colors ${open ? "bg-neutral-800 text-neutral-100" : "text-neutral-500 hover:bg-neutral-900 hover:text-neutral-200"}`}
      >
        <LayoutGrid size={16} />
      </button>
      {open && (
        <div className="absolute left-0 top-full z-50 mt-1.5 w-72 rounded-xl border border-neutral-800 bg-neutral-900 shadow-2xl">
          <div className="flex items-center justify-between border-b border-neutral-800 px-3.5 py-2.5">
            <span className="text-xs font-bold text-neutral-200">Your apps</span>
            <a href={APP_HUB_URL} className="text-[10.5px] font-semibold text-indigo-400 hover:text-indigo-300">
              Manage in App Hub →
            </a>
          </div>
          <div className="grid max-h-80 grid-cols-3 gap-0.5 overflow-y-auto p-2">
            {apps.map((a) => {
              const current = a.name === CURRENT_APP_NAME;
              return (
                <a
                  key={a.id}
                  href={a.url}
                  className={`relative flex flex-col items-center gap-1.5 rounded-lg border px-1.5 py-2.5 text-center ${
                    current ? "border-indigo-500/50 bg-indigo-500/10" : "border-transparent hover:bg-neutral-800"
                  }`}
                >
                  <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-neutral-800 text-indigo-300">
                    <AppIcon name={a.icon} />
                  </span>
                  <span className="text-[10.5px] font-semibold leading-tight text-neutral-200">{a.name}</span>
                  {current && <span className="text-[7.5px] font-bold uppercase tracking-wide text-indigo-400">Current</span>}
                  {a.health && (
                    <span
                      className="absolute right-1.5 top-1.5 h-1.5 w-1.5 rounded-full"
                      style={{ background: a.health.ok ? "#34d399" : "#f87171" }}
                    />
                  )}
                </a>
              );
            })}
          </div>
          <div className="border-t border-neutral-800 px-3.5 py-2.5">
            <a href={APP_HUB_URL} className="flex items-center gap-1.5 text-[10.5px] font-semibold text-neutral-400 hover:text-indigo-400">
              <Send size={11} /> Request access to another app
            </a>
          </div>
        </div>
      )}
    </div>
  );
}
