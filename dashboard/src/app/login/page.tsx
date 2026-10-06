import Image from "next/image";
import LoginForm from "@/components/LoginForm";

export const metadata = { title: "Sign in — AVIS" };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const { next } = await searchParams;
  // Only ever redirect within the app -- an absolute/external `next` value
  // would make this an open redirect.
  const safeNext = next && next.startsWith("/") && !next.startsWith("//") ? next : "/";

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-6 bg-neutral-950 px-5 py-8 text-neutral-50">
      <Image src="/jabil-logo-dark.png" alt="Jabil" width={96} height={18} className="theme-logo-dark h-4 w-auto" priority />
      <Image src="/jabil-logo.png" alt="Jabil" width={96} height={18} className="theme-logo-light h-4 w-auto" priority />
      <div className="text-center">
        <div className="mb-1 font-mono text-[11px] uppercase tracking-wider text-neutral-500">Sign in</div>
        <h1 className="text-xl font-bold">
          AV<span className="text-indigo-400">IS</span>
        </h1>
      </div>
      <LoginForm next={safeNext} />
    </div>
  );
}
