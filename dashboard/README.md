# AVIS dashboard

Runs on the Pi (`http://10.77.193.155:3230`). Same stack and sign-in as the
Pi's other apps: Next.js App Router + Tailwind, JSON files in `data/`, NTID
sign-in through App Hub's shared `app_hub_id` cookie.

```bash
npm ci
cp .env.local.example .env.local   # SESSION_SECRET, AVIS_STATION_KEY
npm run dev                        # http://localhost:3230
npm test && npm run lint && npm run build
```

| Path | |
|---|---|
| `src/app/` | pages (Overview, Stations, Analytics, Units, Faults, Visual aids, Approvals, Admin) and `api/` routes |
| `src/app/api/station/` | what the station middleware calls: `events` (POST), `va-map` (ETag), `va-file` - shared-key auth (`x-avis-station-key`) |
| `src/lib/analytics.ts` | every KPI, derived from raw events (unit tested) |
| `src/lib/vaStore.ts` | products, VA versions, step->page mapping, approval, the station VA map |
| `src/lib/eventStore.ts` | event ingest (deduplicated), daily JSONL files, station status, fault resolutions |
| `src/middleware.ts`, `src/lib/accessControl.ts` | sign-in + role checks |
| `deploy/` | systemd units and install steps for the Pi |
