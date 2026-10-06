// Verifies/signs the `app_hub_id` cookie App Hub itself sets on login (see
// app-hub/backend/src/identity.js) -- same cookie name, same signing
// scheme (HMAC-SHA256 over the raw username, base64url-encoded, no JSON
// envelope and no embedded expiry -- unlike this app's OWN session cookie
// in session.ts, which wraps a JSON payload with iat/exp). Cookies aren't
// port-scoped, only host-scoped, so a cookie App Hub (port 4001) sets is
// already visible to this app (port 3211) on the same box the moment the
// browser sends it -- see [[project-app-hub]]'s SSO section. Deliberately
// Web Crypto only (crypto.subtle), not Node's crypto module, so this can
// be read from middleware.ts's Edge runtime the same way session.ts's own
// HMAC helpers are.
//
// App Hub's own production deployment never overrode its AUTH_COOKIE_SECRET
// env var, so the literal fallback string below is what actually verifies
// there today -- matching Shift Passdown's own copy of this same
// integration line for line. If App Hub's secret is ever rotated for real,
// set APP_HUB_AUTH_COOKIE_SECRET here to match.

export const APP_HUB_COOKIE_NAME = "app_hub_id";
const APP_HUB_COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 30; // matches App Hub's own COOKIE_MAX_AGE_SECONDS

function appHubSecret(): string {
  return process.env.APP_HUB_AUTH_COOKIE_SECRET || "app-hub-local-only";
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

// Cookie VALUE only (no "app_hub_id=" prefix, already decodeURIComponent'd
// -- Next's cookie APIs do that for you on both read and write).
export async function verifyAppHubCookie(cookieValue: string): Promise<string | null> {
  const idx = cookieValue.lastIndexOf(".");
  if (idx < 0) return null;
  const username = cookieValue.slice(0, idx);
  const macB64 = cookieValue.slice(idx + 1);
  try {
    const key = await hmacKey(appHubSecret());
    const valid = await crypto.subtle.verify("HMAC", key, base64UrlDecode(macB64) as BufferSource, new TextEncoder().encode(username));
    return valid ? username : null;
  } catch {
    return null;
  }
}

export async function signAppHubCookie(username: string): Promise<string> {
  const key = await hmacKey(appHubSecret());
  const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(username));
  return `${username}.${base64UrlEncode(new Uint8Array(sig))}`;
}

export function appHubCookieOptions(): { name: string; httpOnly: true; secure: boolean; sameSite: "lax"; path: string; maxAge: number } {
  return {
    name: APP_HUB_COOKIE_NAME,
    httpOnly: true,
    secure: false, // matches session.ts -- plain HTTP on the LAN today
    sameSite: "lax",
    path: "/",
    maxAge: APP_HUB_COOKIE_MAX_AGE_SECONDS,
  };
}
