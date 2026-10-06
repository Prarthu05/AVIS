"use client";

import { useState } from "react";

export default function LoginForm({ next }: { next: string }) {
  const [ntid, setNtid] = useState("");
  const [email, setEmail] = useState("");
  const [name, setName] = useState("");
  const [needsRegistration, setNeedsRegistration] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    // A network hiccup (the request never completing, a dropped
    // connection mid-deploy-restart, etc.) makes fetch() itself reject --
    // without this try/catch that left the button stuck on "Signing in..."
    // forever with no way to retry short of reloading the page.
    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(needsRegistration ? { ntid, email, name } : { ntid }),
      });
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        // An NTID nobody's seen before isn't a hard failure -- it's an
        // invitation to register on the spot (see /api/auth/login's POST).
        if (body.needsRegistration) {
          setNeedsRegistration(true);
          setSubmitting(false);
          return;
        }
        setError(body.error ?? "Something went wrong");
        setSubmitting(false);
        return;
      }
      // Full navigation (not router.push) so middleware re-evaluates the
      // freshly-set cookie on the way in, rather than trusting client-side
      // router state that never saw the redirect.
      window.location.href = next;
    } catch {
      setError("Couldn't reach the server -- check your connection and try again.");
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex w-full max-w-sm flex-col gap-4 rounded-xl border border-neutral-800 bg-neutral-900 p-6 shadow-sm">
      <div>
        <label className="mb-1.5 block font-mono text-[11px] uppercase tracking-wider text-neutral-500">NTID</label>
        <input
          type="text"
          autoComplete="username"
          value={ntid}
          onChange={(e) => setNtid(e.target.value)}
          required
          readOnly={needsRegistration}
          className={`w-full rounded-lg border border-neutral-700 bg-neutral-800 px-3 py-2 font-mono text-sm text-neutral-100 focus:border-indigo-400 focus:outline-none ${
            needsRegistration ? "opacity-60" : ""
          }`}
        />
      </div>

      {needsRegistration && (
        <>
          <p className="text-xs text-neutral-500">
            NTID <span className="font-mono text-neutral-300">{ntid}</span> isn&apos;t set up yet. Enter your email and name to
            get started -- you&apos;ll have Viewer access right away, and an Admin can give you your real access
            afterward.
          </p>
          <div>
            <label className="mb-1.5 block font-mono text-[11px] uppercase tracking-wider text-neutral-500">Email</label>
            <input
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="w-full rounded-lg border border-neutral-700 bg-neutral-800 px-3 py-2 text-sm text-neutral-100 focus:border-indigo-400 focus:outline-none"
            />
          </div>
          <div>
            <label className="mb-1.5 block font-mono text-[11px] uppercase tracking-wider text-neutral-500">Name</label>
            <input
              type="text"
              autoComplete="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              className="w-full rounded-lg border border-neutral-700 bg-neutral-800 px-3 py-2 text-sm text-neutral-100 focus:border-indigo-400 focus:outline-none"
            />
          </div>
        </>
      )}

      {error && <p className="text-sm text-red-400">{error}</p>}

      <button
        type="submit"
        disabled={submitting}
        className="rounded-lg bg-indigo-500 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-400 disabled:opacity-50"
      >
        {submitting ? "Signing in…" : needsRegistration ? "Create account & sign in" : "Sign in"}
      </button>

      {needsRegistration ? (
        <button
          type="button"
          onClick={() => {
            setNeedsRegistration(false);
            setError(null);
          }}
          className="text-center font-mono text-[11px] text-neutral-600 hover:text-neutral-300"
        >
          ← use a different NTID
        </button>
      ) : (
        <p className="text-center text-[11px] text-neutral-600">
          No account yet? Just sign in with your NTID -- you&apos;ll be set up automatically.
        </p>
      )}
    </form>
  );
}
