// crypto.randomUUID() requires a secure context (https or localhost) --
// this app is served over plain http on a LAN IP, so it's unavailable in
// the browser. These ids are only ever used as React keys / JSON object
// keys, never for anything security-sensitive, so a short random string is
// plenty.
export function randomId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}
