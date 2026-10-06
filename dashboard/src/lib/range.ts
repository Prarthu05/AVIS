// Shared ?range=today|24h|7d|30d|custom (&from=&to=) parsing for the analytics APIs and pages.
export type RangeKey = "today" | "24h" | "7d" | "30d" | "custom";

export function parseRange(params: URLSearchParams, now = new Date()): { from: Date; to: Date; key: RangeKey } {
  const key = (params.get("range") as RangeKey) || "today";
  const to = params.get("to") ? new Date(params.get("to")!) : now;
  switch (key) {
    case "24h":
      return { key, from: new Date(to.getTime() - 24 * 3600_000), to };
    case "7d":
      return { key, from: startOfDay(new Date(to.getTime() - 6 * 86_400_000)), to };
    case "30d":
      return { key, from: startOfDay(new Date(to.getTime() - 29 * 86_400_000)), to };
    case "custom": {
      const from = params.get("from") ? new Date(params.get("from")!) : startOfDay(now);
      return { key, from: Number.isNaN(from.getTime()) ? startOfDay(now) : from, to: Number.isNaN(to.getTime()) ? now : to };
    }
    default:
      return { key: "today", from: startOfDay(now), to: now };
  }
}

// The Pi runs on plant local time, so "today" is the server's local day.
function startOfDay(d: Date): Date {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  return x;
}
