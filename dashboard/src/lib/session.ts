import type { Role } from "./domain";

// Signed session cookie, verified in both middleware.ts (Edge runtime) and
// ordinary API routes (Node) -- uses only the Web Crypto API (crypto.subtle),
// a global in both, so this file needs zero npm dependencies and no runtime
// pragma. Node's crypto.scrypt (for password hashing, auth.ts) is kept
// deliberately separate since it is NOT Edge-safe.

export interface SessionPayload {
  personId: string;
  name: string;
  roles: Role[];
  iat: number; // epoch seconds
  exp: number; // epoch seconds
}

const SESSION_MAX_AGE_SECONDS = 12 * 3600; // one shift (same as DowntimeApp)

export function sessionSecret(): string {
  const secret = process.env.SESSION_SECRET;
  if (!secret) throw new Error("SESSION_SECRET is not set -- see .env.local.example");
  return secret;
}

export function sessionCookieOptions(): { name: string; httpOnly: true; secure: boolean; sameSite: "lax"; path: string; maxAge: number } {
  return {
    name: "avis_session",
    httpOnly: true,
    // App is plain HTTP on the LAN today -- flip to true if/when TLS is
    // added in front of it, or a cookie set with secure:true over plain
    // HTTP is silently dropped by the browser.
    secure: false,
    sameSite: "lax",
    path: "/",
    maxAge: SESSION_MAX_AGE_SECONDS,
  };
}

function base64UrlEncode(bytes: Uint8Array): string {
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function base64UrlDecode(str: string): Uint8Array {
  const padded = str.replace(/-/g, "+").replace(/_/g, "/") + "===".slice((str.length + 3) % 4);
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

function hmacKey(secret: string): Promise<CryptoKey> {
  return crypto.subtle.importKey("raw", new TextEncoder().encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign", "verify"]);
}

export async function signSession(payload: SessionPayload, secret: string): Promise<string> {
  const payloadB64 = base64UrlEncode(new TextEncoder().encode(JSON.stringify(payload)));
  const key = await hmacKey(secret);
  const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(payloadB64));
  return `${payloadB64}.${base64UrlEncode(new Uint8Array(sig))}`;
}

export function newSessionPayload(personId: string, name: string, roles: Role[]): SessionPayload {
  const iat = Math.floor(Date.now() / 1000);
  return { personId, name, roles, iat, exp: iat + SESSION_MAX_AGE_SECONDS };
}

// Verifies the signature (crypto.subtle.verify does the constant-time
// comparison internally) and the expiry. Returns null rather than throwing
// on anything malformed -- callers treat that identically to "not logged
// in", never a hard error.
export async function verifySession(token: string, secret: string): Promise<SessionPayload | null> {
  const parts = token.split(".");
  if (parts.length !== 2) return null;
  const [payloadB64, sigB64] = parts;

  try {
    const key = await hmacKey(secret);
    const valid = await crypto.subtle.verify("HMAC", key, base64UrlDecode(sigB64) as BufferSource, new TextEncoder().encode(payloadB64));
    if (!valid) return null;

    const payload = JSON.parse(new TextDecoder().decode(base64UrlDecode(payloadB64))) as SessionPayload;
    if (typeof payload.exp !== "number" || payload.exp < Math.floor(Date.now() / 1000)) return null;
    return payload;
  } catch {
    return null;
  }
}
