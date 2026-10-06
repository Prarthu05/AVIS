"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Gauge,
  ChartColumn,
  MonitorCog,
  ScanSearch,
  TriangleAlert,
  FileImage,
  ClipboardCheck,
  KeyRound,
  Server,
  ScrollText,
  Hand,
  LogOut,
  Menu,
  X,
  Sun,
  Moon,
  type LucideIcon,
} from "lucide-react";
import { getTheme, setTheme, type Theme } from "@/lib/theme";
import { pageAccessAllowed } from "@/lib/accessControl";
import { roleLabels, type Role } from "@/lib/domain";
import type { SessionPayload } from "@/lib/session";
import AppHubSwitcher from "./AppHubSwitcher";

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
}

const primaryItems: NavItem[] = [
  { href: "/", label: "Overview", icon: Gauge },
  { href: "/stations", label: "Stations", icon: MonitorCog },
  { href: "/analytics", label: "Analytics", icon: ChartColumn },
  { href: "/units", label: "Unit Traceability", icon: ScanSearch },
  { href: "/faults", label: "Fault Feed", icon: TriangleAlert },
];

const vaItems: NavItem[] = [
  { href: "/va", label: "VA Library", icon: FileImage },
  { href: "/approvals", label: "Approvals", icon: ClipboardCheck },
];

const adminItems: NavItem[] = [
  { href: "/admin/access", label: "People & Access", icon: KeyRound },
  { href: "/admin/stations", label: "Station Setup", icon: Server },
  { href: "/admin/audit", label: "Audit Log", icon: ScrollText },
];

const selfItems: NavItem[] = [{ href: "/request-access", label: "Request Access", icon: Hand }];

function noopSubscribe(): () => void {
  return () => {};
}

function isActive(pathname: string, href: string): boolean {
  return href === "/" ? pathname === "/" : pathname.startsWith(href);
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return <div className="px-3 pb-1 pt-4 font-mono text-[10px] font-semibold uppercase tracking-wider text-neutral-500">{children}</div>;
}

function Row({ item, active, onClick }: { item: NavItem; active: boolean; onClick?: () => void }) {
  const Icon = item.icon;
  return (
    <Link
      href={item.href}
      onClick={onClick}
      className={`flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
        active ? "bg-indigo-500 text-white" : "text-neutral-400 hover:bg-neutral-900 hover:text-neutral-100"
      }`}
    >
      <Icon size={16} className="shrink-0" />
      <span className="truncate">{item.label}</span>
    </Link>
  );
}

function NavContent({ pathname, roles, onNavigate }: { pathname: string; roles: Role[]; onNavigate?: () => void }) {
  // Nav visibility mirrors accessControl's page-access table -- a role
  // that can't reach a page never gets shown the link to it. This is UX
  // only; middleware is what actually enforces it if someone navigates
  // there directly. Each section is filtered independently by its own
  // items' real access rules (not one hardcoded role check gating the
  // whole section) so a role that can reach exactly one item in a
  // section still sees that item -- e.g. IE keeps seeing Escalation under
  // Admin without needing the full Admin role.
  const visiblePrimary = primaryItems.filter((item) => pageAccessAllowed(item.href, roles));
  const visibleVa = vaItems.filter((item) => pageAccessAllowed(item.href, roles));
  const visibleAdmin = adminItems.filter((item) => pageAccessAllowed(item.href, roles));
  const visibleSelf = roles.includes("admin") ? [] : selfItems;

  return (
    <nav className="flex-1 space-y-0.5 overflow-y-auto px-2 pb-3">
      <div className="space-y-0.5 pt-2">
        {visiblePrimary.map((item) => (
          <Row key={item.href} item={item} active={isActive(pathname, item.href)} onClick={onNavigate} />
        ))}
      </div>
      {visibleVa.length > 0 && (
        <>
          <SectionLabel>Visual Aids</SectionLabel>
          <div className="space-y-0.5">
            {visibleVa.map((item) => (
              <Row key={item.href} item={item} active={isActive(pathname, item.href)} onClick={onNavigate} />
            ))}
          </div>
        </>
      )}
      {visibleAdmin.length > 0 && (
        <>
          <SectionLabel>Admin</SectionLabel>
          <div className="space-y-0.5">
            {visibleAdmin.map((item) => (
              <Row key={item.href} item={item} active={isActive(pathname, item.href)} onClick={onNavigate} />
            ))}
          </div>
        </>
      )}
      {visibleSelf.length > 0 && (
        <div className="space-y-0.5 pt-4">
          {visibleSelf.map((item) => (
            <Row key={item.href} item={item} active={isActive(pathname, item.href)} onClick={onNavigate} />
          ))}
        </div>
      )}
    </nav>
  );
}

function ThemeToggleButton({ theme, onToggle, className = "" }: { theme: Theme; onToggle: () => void; className?: string }) {
  return (
    <button
      onClick={onToggle}
      title={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
      className={`flex items-center justify-center rounded-lg text-neutral-400 hover:bg-neutral-900 hover:text-neutral-100 ${className}`}
    >
      {theme === "dark" ? <Sun size={16} /> : <Moon size={16} />}
    </button>
  );
}

function LogoutButton({ className = "" }: { className?: string }) {
  async function logout() {
    await fetch("/api/auth/logout", { method: "POST" });
    // Full navigation, not router.push -- guarantees middleware
    // re-evaluates with the now-cleared cookie on the way to /login.
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination
    window.location.href = "/login";
  }
  return (
    <button onClick={logout} title="Log out" className={`flex items-center justify-center rounded-lg text-neutral-400 hover:bg-neutral-900 hover:text-red-400 ${className}`}>
      <LogOut size={16} />
    </button>
  );
}

export default function GlobalNav({ session }: { session: SessionPayload | null }) {
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);
  // "dark" here (not getTheme()) matches what the server renders --
  // localStorage doesn't exist during SSR, so getTheme()'s own fallback
  // would already be "dark" there, but calling it directly as the initial
  // client value risks a real mismatch the moment someone actually has
  // "light" stored: server says dark, client's first render says light,
  // React has to reconcile a mismatch on every hydration. Synced to the
  // real stored value in the effect below instead, after mount -- the
  // data-theme attribute itself (the thing that actually themes the page)
  // is already correct by then regardless, set synchronously pre-paint by
  // the blocking script in layout.tsx; this state is only for which
  // icon/logo the toggle itself shows.
  // The stored theme is read with useSyncExternalStore: "dark" on the server
  // (no localStorage there) and the real stored value on the client, without
  // a hydration mismatch or a setState-in-effect re-render. The data-theme
  // attribute itself is already set pre-paint by layout.tsx's script; this is
  // only for which icon/logo the toggle shows.
  const storedTheme = useSyncExternalStore(noopSubscribe, getTheme, () => "dark" as Theme);
  const [themeOverride, setThemeOverride] = useState<Theme | null>(null);
  const theme = themeOverride ?? storedTheme;
  const ref = useRef<HTMLDivElement>(null);
  const menuButtonRef = useRef<HTMLButtonElement>(null);

  // Close the mobile menu on navigation - adjusted during render (React's
  // recommended pattern) rather than in an effect.
  const [menuPath, setMenuPath] = useState(pathname);
  if (menuPath !== pathname) {
    setMenuPath(pathname);
    setMobileOpen(false);
  }
  // The toggle button itself lives in the mobile header, outside the
  // dropdown panel `ref` tracks -- without excluding it here too, a
  // click on the button while the menu is open fires both this
  // mousedown listener (which closes it) AND the button's own onClick
  // (which re-toggles it back open) for the same click, so the button
  // appeared to do nothing except when the menu was already closed.
  useEffect(() => {
    function onClick(e: MouseEvent) {
      const target = e.target as Node;
      if (ref.current?.contains(target)) return;
      if (menuButtonRef.current?.contains(target)) return;
      setMobileOpen(false);
    }
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, []);

  function toggleTheme() {
    const next: Theme = theme === "dark" ? "light" : "dark";
    setTheme(next);
    setThemeOverride(next);
  }

  // jabil-logo.png is the dark-navy wordmark for light backgrounds;
  // jabil-logo-dark.png is the light/white one for dark backgrounds --
  // matching Relay's own two assets, swapped with the sidebar itself.
  const logoSrc = theme === "dark" ? "/jabil-logo-dark.png" : "/jabil-logo.png";

  // /login carries its own complete, larger branded header (logo, "Sign
  // in" eyebrow, AVIS wordmark) inside its centered card -- this
  // generic bar rendering there too was a second, smaller Jabil logo
  // orphaned in the top-left corner of the same screen, redundant at
  // best and confusing at worst (a real screenshot caught it: "what's
  // the logo on the side for?"). Every other no-session case (a
  // mid-navigation redirect, a stale tab) still gets this bar so the
  // page isn't blank while middleware sends it to /login.
  if (pathname === "/login") return null;

  // /login is the one page middleware lets through with no session --
  // logo-only, no nav (there's nothing to link to without a role yet).
  if (!session) {
    return (
      <header className="sticky top-0 z-40 flex items-center gap-2.5 border-b border-neutral-800 bg-neutral-950 px-3 py-2.5">
        <Image src={logoSrc} alt="Jabil" width={64} height={12} className="h-3 w-auto shrink-0" priority />
        <span className="text-sm font-bold uppercase tracking-tight text-neutral-100">
          AV<span className="text-indigo-400">IS</span>
        </span>
      </header>
    );
  }

  return (
    <>
      {/* Desktop sidebar */}
      <aside className="sticky top-0 hidden h-screen w-56 shrink-0 flex-col border-r border-neutral-800 bg-neutral-950 md:flex">
        <div className="flex items-center justify-between gap-2 border-b border-neutral-800 px-3 py-3.5">
          <Link href="/" className="flex min-w-0 items-center gap-2.5">
            <Image src={logoSrc} alt="Jabil" width={64} height={12} className="h-3 w-auto shrink-0" priority />
            <span className="truncate text-sm font-bold uppercase tracking-tight text-neutral-100">
              AV<span className="text-indigo-400">IS</span>
            </span>
          </Link>
          <AppHubSwitcher />
        </div>
        <NavContent pathname={pathname} roles={session.roles} />
        <div className="flex items-center justify-between gap-2 border-t border-neutral-800 px-3 py-2.5">
          <div className="min-w-0">
            <div className="truncate text-xs font-medium text-neutral-200">{session.name}</div>
            <div className="truncate font-mono text-[10px] text-neutral-600">{roleLabels(session.roles)}</div>
          </div>
          <LogoutButton className="shrink-0 p-1.5" />
        </div>
        <div className="flex items-center justify-between border-t border-neutral-800 px-3 py-2.5">
          <span className="font-mono text-[10px] uppercase tracking-wider text-neutral-600">{theme} mode</span>
          <ThemeToggleButton theme={theme} onToggle={toggleTheme} className="p-1.5" />
        </div>
      </aside>

      {/* Mobile top bar */}
      <header className="sticky top-0 z-40 flex items-center justify-between border-b border-neutral-800 bg-neutral-950 px-3 py-2.5 md:hidden">
        <Link href="/" className="flex items-center gap-2">
          <Image src={logoSrc} alt="Jabil" width={56} height={11} className="h-2.5 w-auto shrink-0" priority />
          <span className="text-sm font-bold uppercase tracking-tight text-neutral-100">
            AV<span className="text-indigo-400">IS</span>
          </span>
        </Link>
        <div className="flex items-center gap-1">
          <AppHubSwitcher />
          <ThemeToggleButton theme={theme} onToggle={toggleTheme} className="p-1.5" />
          <LogoutButton className="p-1.5" />
          <button ref={menuButtonRef} onClick={() => setMobileOpen((o) => !o)} className="p-1.5 text-neutral-400 hover:text-neutral-100" aria-label="Menu">
            {mobileOpen ? <X size={20} /> : <Menu size={20} />}
          </button>
        </div>
      </header>
      {mobileOpen && (
        <div ref={ref} className="fixed inset-x-0 top-[49px] z-40 border-b border-neutral-800 bg-neutral-950 shadow-xl md:hidden">
          <div className="border-b border-neutral-800 px-3 py-2.5">
            <div className="truncate text-xs font-medium text-neutral-200">{session.name}</div>
            <div className="truncate font-mono text-[10px] text-neutral-600">{roleLabels(session.roles)}</div>
          </div>
          <NavContent pathname={pathname} roles={session.roles} onNavigate={() => setMobileOpen(false)} />
        </div>
      )}
    </>
  );
}
