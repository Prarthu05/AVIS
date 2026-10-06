# Deploying the AVIS dashboard on the Pi (jabilie, 10.77.193.155)

Same pattern as DowntimeApp: a Next.js production build run by systemd
(`next start` directly, not `npm start`, so `systemctl stop` shuts it down
cleanly), a staging copy on its own port, and the App Hub tile for SSO.

| | Path | Port | systemd unit |
|---|---|---|---|
| Production | `/opt/avis-dashboard/dashboard` | 3230 | `avis-dashboard` |
| Staging | `/opt/avis-dashboard-staging/dashboard` | 3231 | `avis-dashboard-staging` |

`/opt/avis-dashboard` is a clone of the AVIS repo; only its `dashboard/`
folder runs on the Pi (the rest is the station program for the Windows PCs).

## One-time install

```bash
# 1. Prerequisites: Node 20.9+ (already on the Pi for the other Next.js apps)
#    and poppler-utils, which renders uploaded PDF VAs to page images.
node --version
sudo apt install -y poppler-utils

# 2. Get the code
sudo mkdir -p /opt/avis-dashboard && sudo chown jabilie:jabilie /opt/avis-dashboard
git clone -b claude/modest-gauss-s52evf https://github.com/Prarthu05/AVIS.git /opt/avis-dashboard
cd /opt/avis-dashboard/dashboard

# 3. Secrets - never committed (.env.local is git-ignored)
cp .env.local.example .env.local
openssl rand -hex 32   # paste as SESSION_SECRET
openssl rand -hex 24   # paste as AVIS_STATION_KEY (stations use the same value)
nano .env.local
chmod 600 .env.local

# 4. Build and start
npm ci
npm run build
sudo cp deploy/avis-dashboard.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now avis-dashboard
sudo systemctl status avis-dashboard --no-pager
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3230/login   # 200 = running
```

Open `http://10.77.193.155:3230` from any PC on the network.

## Add the App Hub tile (so sign-in is the same as the other apps)

The dashboard signs people in with App Hub's shared `app_hub_id` cookie -
exactly like DowntimeApp, Jig Tracker etc. - so there are no new passwords.

1. Open App Hub (`http://10.77.193.155:4001`) as a Super Admin.
2. Apps -> Add app: name **AVIS**, URL `http://10.77.193.155:3230`,
   description "Visual-aid upload/approval and station analytics", icon e.g.
   `ScanEye`. Baseline on (everyone can open it; roles inside AVIS still
   decide what they can do).

Signing in with NTID **4375789** the first time makes that account Admin
(`BOOTSTRAP_ADMIN_NTID`). Anyone else who signs in becomes a Viewer and can
request a role from **Request access**; the Admin approves it under
**Admin -> People & access**.

| Role | Can |
|---|---|
| Admin | everything, incl. granting roles and station admin |
| Approver | approve / reject VA versions (not their own upload) |
| Engineer | create products, upload VAs, map steps to pages, submit for approval |
| Technician | resolve / reopen station faults (Engineers can too) |
| Viewer | read-only dashboards |

## Point the stations at it

On each station PC (Windows):

1. `appsettings.json` -> `"Dashboard": { "Enabled": true, "BaseUrl": "http://10.77.193.155:3230" }`
   (`StationName` defaults to `Station:Name`).
2. Set the shared key as a machine environment variable (same value as
   `AVIS_STATION_KEY` above), then restart AVIS:
   ```powershell
   [Environment]::SetEnvironmentVariable("AVIS_DASHBOARD_KEY", "<key>", "Machine")
   ```

The station appears under **Stations** within 30 s (heartbeat). Events
queue in `data\dashboard-outbox.jsonl` while the Pi is unreachable and upload
when it's back; approved VAs are cached in `va-cache\` so production never
waits on the dashboard.

## Updating

```bash
cd /opt/avis-dashboard-staging/dashboard   # verify in staging first (port 3231)
git pull && npm ci && npm run build && sudo systemctl restart avis-dashboard-staging

cd /opt/avis-dashboard/dashboard           # then production
git pull && npm ci && npm run build && sudo systemctl restart avis-dashboard
```

Staging is set up the same way (clone into `/opt/avis-dashboard-staging`,
its own `.env.local` with a **different** `SESSION_SECRET` and station key,
`deploy/avis-dashboard-staging.service`). Point a bench/simulation station
at port 3231, never a production one.

## Data and backups

Everything lives in `dashboard/data/` (git-ignored):

| File | What |
|---|---|
| `people.json`, `access-requests.json` | roles |
| `products.json`, `va-documents.json`, `va-files/<docId>/` | VA versions + page images |
| `events/YYYY-MM-DD.jsonl` | station events (one file per day, append-only) |
| `stations.json`, `fault-resolutions.json` | station status, fault notes |
| `audit.log` | who changed what (one JSON line per change) |

Back up the folder like the other apps' `data/` (e.g. nightly `tar` in
`crontab`). Logs: `journalctl -u avis-dashboard -f`.
