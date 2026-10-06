import type { Metadata } from "next";
import { Roboto } from "next/font/google";
import Script from "next/script";
import { cookies } from "next/headers";
import GlobalNav from "@/components/GlobalNav";
import { sessionCookieOptions, sessionSecret, verifySession } from "@/lib/session";
import "./globals.css";

// Jabil Brand Guidelines specify Roboto -- the same face Relay/App Hub/
// Ticket System already load from Google Fonts, at the same weights.
const roboto = Roboto({
  variable: "--font-roboto",
  subsets: ["latin"],
  weight: ["300", "400", "500", "700", "900"],
  style: ["normal", "italic"],
});

export const metadata: Metadata = {
  title: "AVIS Dashboard",
  description: "AVIS station analytics, fault feed and visual-aid management.",
};

// Applied synchronously, before first paint, so a returning visitor who
// chose light mode doesn't see a flash of dark while the page loads. The
// bare (un-attributed) state is already dark -- see theme.ts -- so there's
// nothing to do when the stored choice is dark or unset, only when it's
// light.
const THEME_INIT_SCRIPT = `(function(){try{if(localStorage.getItem("avis-theme")==="light"){document.documentElement.setAttribute("data-theme","light");}}catch(e){}})();`;

export default async function RootLayout({ children }: LayoutProps<"/">) {
  // Middleware already redirected any unauthenticated request to /login
  // before it reaches a protected page, so `session` is non-null here on
  // every page GlobalNav actually needs it for -- except /login itself,
  // where GlobalNav renders logo-only (see its own null-session handling).
  const cookieStore = await cookies();
  const token = cookieStore.get(sessionCookieOptions().name)?.value;
  const session = token ? await verifySession(token, sessionSecret()) : null;

  return (
    <html lang="en" className={`${roboto.variable} h-full antialiased`}>
      {/* A plain <script> tag here doesn't reliably execute -- React's
          client render path treats it as inert markup, not something the
          browser should run (Next.js's dev overlay flags this directly:
          "Scripts inside React components are never executed when
          rendering on the client"). next/script's beforeInteractive
          strategy is the documented fix: a script that must run before
          hydration, like setting a theme before paint. It goes as a
          direct child of <html> here, NOT inside a manual <head> --
          this app also uses the Metadata API (the `metadata` export
          above), and Next.js already generates its own <head> from
          that. Wrapping the script in a second, manually-written <head>
          fought that auto-generated one and was the actual cause of the
          theme "resetting" on every click/refresh: the mismatch meant
          the data-theme attribute the script set never survived
          hydration correctly. */}
      <Script id="theme-init" strategy="beforeInteractive" dangerouslySetInnerHTML={{ __html: THEME_INIT_SCRIPT }} />
      <body className="min-h-full bg-neutral-950 text-neutral-50">
        <div className="flex min-h-full">
          <GlobalNav session={session} />
          <main className="min-w-0 flex-1">{children}</main>
        </div>
      </body>
    </html>
  );
}
