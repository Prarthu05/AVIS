# AVIS - station middleware (badge + JabilEye + iFactory + LightGuide)

AVIS is the station program built on the CVG `BaseProgram` WinForms shell. It is the
"middleware / central integration layer" from the AVIS flow chart:

```
HN arrives at station (conveyor)
 ├─ Operator scans ID ──────► NTID ready      (badge scanner, HID or serial)
 └─ Sensor detects HN ──────► AssetID ready   (JabilEye camera, hardware-triggered)
        └──► Middleware sends NTID + AssetID to iFactory  (GET WIP, Start WIP, x-operator-override)
               └──► iFactory triggers LightGuide ─► LightGuide runs program, reports program & step
                      ├─ AVIS monitors program & step ─► drives the visual aid (VA) per step
                      └─ LightGuide triggers LJ & IV4 ─► AVIS reads the result variables,
                         logs the result, stages the IV4 image tagged with the AssetID
                             Errors? ─ yes ─► highlight issue to operator, rework, re-check (up to 5)
                                     │        5th failure ─► escalate (fault ST-01, critical)
                                     └ no ──► send station data to iFactory (attributes + Complete WIP)
                                              ─► move to next station
Middleware ─► Fault log (error code per integration point) ─► Dashboard fault feed
          ├─► Local image staging (IV4 images tagged with AssetID, on the station PC)
          └─► AVIS dashboard on the Pi (events + heartbeat up, approved VAs down)
```

## Solution layout (open `AVIS.sln` in Visual Studio 2022)

| Project | What it is |
|---|---|
| `src/Avis.App` | **Startup project.** WinForms station app (`AVIS.exe`): the BaseProgram window, background workers, Simulation mode. `net8.0-windows` |
| `src/Avis.Core` | All station logic, no UI: config + INI, iFactory client, JabilEye client, LightGuide client/monitor, station sequencer, fault log, image staging, dashboard outbox + VA cache. Plain `net8.0`, fully unit-tested |
| `src/Avis.Scanner.Hid` | Raw Input badge-scanner reader (from the JabilEye middleware, verified on real hardware) |
| `tests/Avis.Tests` | xUnit tests for Core - 172 tests, including the whole flow chart end to end, the screen switching and the dashboard sync |
| `dashboard/` | **AVIS dashboard** (Next.js) that runs on the Raspberry Pi - see below. Not part of the .NET solution |

The iFactory, JabilEye and badge-scanner code is taken from the tested
`JabilEye` middleware (`prarthu05/jabileye`), with the bug fixes listed below.

## Run it in Visual Studio (no hardware needed)

Prerequisites: Visual Studio 2022 17.8+ with the **.NET desktop development**
workload (.NET 8 SDK), and the Microsoft Edge **WebView2 Runtime** (already on
Windows 10/11).

1. Open `AVIS.sln`. `Avis.App` is the startup project.
2. In the toolbar's run drop-down pick the launch profile **AVIS (Simulation)**.
3. Press **F5**.

The station window opens maximised with a yellow **SIMULATION** badge, plus an
**AVIS Simulator** window that stands in for the hardware:

| Simulator group | What it simulates |
|---|---|
| 1. Operator scans ID | A badge scan (NTID) |
| 2. Sensor detects HN | A passing JabilEye capture with the given Asset ID ("Unknown asset" = no WIP in iFactory) |
| 3. LightGuide | Start program, go to / next step (the visual aid follows the step) |
| 4. LJ / IV4 checks | Posts OK / NG into the LightGuide result variables (IV4 also drops a generated image) |
| 5. Finish | Complete / abort the LightGuide program; "Fail next iFactory call" shows the Reset popup |
| Scripted runs | Whole flow chart: good part, 2 NG then pass, 5 NG -> escalation |

Simulation mode uses the **real** sequencer and the **real** LightGuide change
detection. Only the badge scanner, the JabilEye camera, LightGuide's Web API and
iFactory are replaced by in-process stand-ins. The simulated iFactory calls are
listed in the Simulator window. The sample station INI and visual aids are in
`src/Avis.App/sim/`. Faults, counters and staged images go to `sim/...` under
the output folder.

**Tests:** *Test > Run All Tests* (Test Explorer), or `dotnet test` from the repo root.

The other launch profile, **AVIS (Station - real hardware)**, runs against the
real devices using `appsettings.json` and needs the `IFACTORY_USERNAME` /
`IFACTORY_PASSWORD` environment variables (or a `.env` file - see `.env.example`).

## The station screen

AVIS is **one window** with three screens that swap in place. It never opens a
new window for them.

| Screen | Shown |
|---|---|
| **Station** (iFactory + part status) | Automatically whenever the operator is needed: part detected / scan badge, sending to iFactory, rework (NG), escalation, part not confirmed, part confirmed - move on, idle - and whenever a fault popup appears |
| **Visual aid** | Automatically when LightGuide moves to a step that has a visual aid, and again as soon as a rework is cleared (NG -> OK) |
| **Fault feed** | From the sidebar |

How the screens switch:
- The rule lives in `ScreenSelector` and is unit-tested. It only switches on a
  change (new step, new phase, new visual aid). If the operator picks a screen
  from the sidebar, the next status update doesn't pull them away.
- `Avis:AutoShowVisualAid = false` turns automatic switching off. The sidebar
  still switches screens by hand.
- The visual aid always reuses the same browser panel and only navigates when
  the page changes.
- Links that would open a pop-up window (`target="_blank"`, `window.open`), in
  the visual aid or in iFactory, open in the same panel instead. Alt+Left goes back.
- The visual aid screen has a slim strip on top with the Asset ID, model, step
  and the current message, so the operator always knows where they are.

What each screen contains:
- **Header:** STG/PRD mode (taken from the INI), station resource, logo.
- **Station screen:** iFactory web UI (left). On the right are MODEL RUNNING,
  STATION NAME, PASS / FAIL / YIELD, then the colour-coded banner that tells the
  operator what to do now. The banner states are: scan badge (amber), in process
  (navy), rework (orange), escalate (dark red), confirmed OK - move to next
  station (green), not confirmed (red). Below the banner are the part's NTID,
  Asset ID, WIP, model, LightGuide program and step, and the latest LJ / IV4
  result per check.
- **Sidebar:** STATION / VISUAL AID / FAULTS. The current screen is highlighted.
- **Status bar:** link state of the badge scanner, JabilEye, iFactory and
  LightGuide (green / red / grey).
- **CVG button** (top left) closes AVIS after a confirmation.

PASS / FAIL / rework counters are per day and survive a restart (`data/counters-yyyyMMdd.json`).

## Configuration

### `appsettings.json` (next to `AVIS.exe`)

| Section | Purpose |
|---|---|
| `Avis` | INI path on the share + local cache, STG/PRD iFactory URLs (API + web UI), web zoom, WebView2 profile folder |
| `Station` | Station name, operator domain (`Jabil\<NTID>`), how long a badge scan stays valid before the part arrives |
| `Scanner` | Badge scanner: `Hid` (Bluetooth/USB keyboard-wedge) or `Serial` (COM port) |
| `IFactory` | Timeouts, retries, WIP statuses that are never started. The base URL comes from `Avis:Stg/Prd` |
| `JabilEyeCamera` | Camera IP (overridden per PC by the INI `[JABIL_EYE]` section), program, iFactory resource name |
| `LightGuide` | Web API URL (default `http://localhost:54274`), poll interval, status variable names, the LJ / IV4 checks |
| `Sequence` | Max failed attempts before escalation (5), sending check results to iFactory as WIP attributes, required checks |
| `ImageStaging` | IV4 drop folder, staging folder, retention |
| `FaultLog` | Local fault-log folder and an optional shared folder for a central dashboard |
| `Dashboard` | AVIS dashboard on the Pi: `Enabled` (default false), `BaseUrl` (`http://10.77.193.155:3230`), upload/heartbeat/VA-sync intervals, VA cache + outbox paths. The key comes from the `AVIS_DASHBOARD_KEY` env var |

### Station INI (`AVIPROJECT_INI.txt` on the share)

```ini
[CONFIG]
STG = TRUE                       ; TRUE = staging iFactory, FALSE = production

[RECIPE]
PN-1001 = CVG300-A               ; iFactory WIP material (part number) = model

[JABIL_EYE]
AVIS-PC-02 = 10.72.194.166       ; this station PC's hostname = its JabilEye camera IP

[VISUAL_ADD]
CVG300-A = https://.../overview  ; visual aid for every step of the model
CVG300-A:3 = \\server\va\step3.pdf  ; visual aid for LightGuide step 3 only
```

When the dashboard is enabled, a VA approved there for the product wins; the
INI `[VISUAL_ADD]` entries are the fallback for products/steps the dashboard
doesn't map yet.

Section names are case-insensitive and values may contain `=`. The old
`[JABI_LEYE]` spelling is still accepted. If the share is unreachable AVIS uses
the last good local copy (`config/AVIPROJECT_INI.cache.txt`) and records fault
CFG-01.

## LightGuide setup (Web API)

AVIS reads LightGuide through its **Web API**
(https://wiki.lightguidesys.com/Web_API, LightGuide 4.3+). It polls
`GET /Variable/{name1},{name2},...`.

1. **System Settings -> Devices:** add the **Web API** device on port `54274`.
   Run its URL reservation once as administrator:
   `netsh http add urlacl url=http://+:54274/ user=Everyone`.
2. **System Settings -> Variables -> New -> Standard...:** add `WI_Name`,
   `StepNumberMain`, `StepCommentMain`, `WI_Running`, `WI_Complete` and `LGSState`.
3. For each check, create two variables and set them from the LightGuide program
   that triggers the sensor:
   - `AVIS_LJ_Result` / `AVIS_IV4_Result`: the result (`OK` / `NG`).
   - `AVIS_LJ_Count` / `AVIS_IV4_Count`: incremented **after** the result is
     written. The counter is how AVIS tells two NGs in a row apart.

   The names, the pass values and any measurement variables are set in
   `LightGuide:Checks`.

## IV4 images

Set the IV4's image output (FTP/SMB) to the station PC folder
`ImageStaging:DropFolder` (default `C:\AVIS\IV4Drop`). After each IV4 result,
AVIS takes the newest image from that folder and files it as:

```
images\2026-10-05\HN24A0001873\HN24A0001873_20261005-094631-208_IV4_A5_NG.bmp
images\2026-10-05\HN24A0001873\HN24A0001873_20261005-094631-208_IV4_A5_NG.json   <- AssetID, NTID, WIP, program, step, attempt, result
```

Images stay on the station PC (no SharePoint upload). They are deleted after
`ImageStaging:RetentionDays`.

## Fault codes

| Code | Integration point | Meaning |
|---|---|---|
| CFG-01 | Config | Station INI unreachable - using the local copy / defaults |
| SCN-01 | Badge scanner | No badge scan for the part |
| JE-01 / 02 / 03 | JabilEye | Camera unreachable / capture not Pass / Asset ID unreadable |
| IF-01 / 02 / 03 / 04 / 05 | iFactory | Unreachable / WIP not found / Start WIP rejected / Complete WIP rejected / WIP attribute rejected |
| LG-01 / 02 / 03 | LightGuide | Web API unreachable / result with no active part / program aborted |
| LJ-01, IV-01, CHK-01 | Checks | LJ / IV4 / other check NG |
| IMG-01 / 02 | Image staging | No new IV4 image / staging failed |
| ST-01 / 02 / 03 | Station | Rework limit reached (escalate) / part abandoned / part not confirmed |

Faults are written to `faults\faults-yyyyMMdd.jsonl`, one JSON object per line.
If `FaultLog:SharedDirectory` is set they are also written to
`<share>\<station>-faults-yyyyMMdd.jsonl`, for the techs'/engineers' dashboard.

## AVIS dashboard (Raspberry Pi)

`dashboard/` is a Next.js app built like the Pi's other apps (DowntimeApp
stack, App Hub SSO). Anyone on the network opens `http://10.77.193.155:3230`.

- **Sign-in:** the same as the other Pi apps - App Hub's shared NTID sign-in,
  no new passwords. NTID 4375789 is the bootstrap Admin; everyone else starts
  as Viewer and requests a role, which the Admin approves (Admin, Approver,
  Engineer, Technician, Viewer).
- **Visual aids per product:** an Engineer creates a product (name + iFactory
  part numbers), uploads a PDF (rendered to page images) or images, and maps
  each LightGuide step to its pages (plus default pages). It's submitted, and an
  Approver - a second person - approves it. Only approved versions reach the
  stations; the previous version is kept as "superseded".
- **Middleware link:** every station posts its events (unit started, step
  changes, LJ/IV4 results with attempts and measurements, rework, escalation,
  confirmed / not confirmed, faults) and a 30 s heartbeat, and downloads the
  approved VA map + pages into `va-cache\`. Events are queued in
  `data\dashboard-outbox.jsonl` first, so a Pi outage loses nothing and never
  stops production.
- **Analytics:** live station status, FPY, yield, rework, escalations, cycle
  time, throughput by hour/day, LJ/IV4 first-try NG rate, time per step
  (bottlenecks), fault Pareto by code / integration point / station, splits by
  station / product / operator, and a per-unit timeline (by Asset ID).
- **Fault feed:** every station fault with its code; Technicians resolve them
  with a note.

Turn it on per station: `Dashboard:Enabled=true` and the `AVIS_DASHBOARD_KEY`
environment variable. Install on the Pi: `dashboard/deploy/README.md`.

Run it locally: `cd dashboard && npm ci && cp .env.local.example .env.local`
(fill in `SESSION_SECRET` and `AVIS_STATION_KEY`), then `npm run dev` and open
`http://localhost:3230`. Point a Simulation-mode station at it with
`Dashboard:Enabled=true` in `appsettings.Simulation.json`.

## Deploying to a station PC

```powershell
dotnet publish src/Avis.App/Avis.App.csproj -c Release -r win-x64 --self-contained true -o C:\AVIS
```

Set `IFACTORY_USERNAME` / `IFACTORY_PASSWORD` (and `AVIS_DASHBOARD_KEY` if the
dashboard is enabled) as Windows environment variables.
Start `C:\AVIS\AVIS.exe` at operator logon with Task Scheduler. The JabilEye
middleware README explains why it must not be a Windows Service: a service runs
in Session 0, where HID badge scans never arrive. AVIS is single-instance.

## Bugs fixed

From **BaseProgram**:
- The app crashed at startup when the INI share was unreachable. It now uses a
  local cache, then defaults.
- INI values containing `=` (URLs with query strings) were silently dropped.
- `bool.Parse` crashed on `1`/`yes`.
- The `[JABI_LEYE]` typo is accepted alongside `[JABIL_EYE]`.
- `async void` lambdas could crash the app. WebView2 errors are now caught and
  shown in the iFactory panel.
- The logger factory was disposed in the constructor, and console logging went
  nowhere in a WinExe. AVIS now logs to `logs\avis-*.log`.
- `ShowhMainPageLeft` modified `Controls` while enumerating it.
- The scanner was never stopped on close.
- Clicking the logo closed the app without confirmation.
- `_env` was always null. WebView2 now uses an explicit, always-writable profile
  folder.
- Placeholder controls, `Text = "Form1"` and hard-coded counters were replaced.

From the **JabilEye middleware**:
- An iFactory or camera HTTP **timeout** (`TaskCanceledException`) looked like a
  shutdown and silently stopped the worker loop.
- A token-endpoint failure surfaced as a raw `HttpRequestException` and was
  reported as "Camera Unreachable".
- On startup, the capture already on the camera was re-processed, which could
  Start WIP again for an old part.
- The error dialog's dock order put the message under the red banner.

## Open items / to confirm

- **LightGuide `GET /Variable` response format:** the wiki documents the request
  but not the body. The parser accepts every likely shape (`[{Name,Value}]`,
  `{name: value}`, plain text), but please send one real response so it can be
  pinned down.
- **Escalation to authority** after the 5th failure: on hold as agreed. Today
  AVIS records critical fault ST-01, shows the escalation banner and does not
  complete the WIP. A supervisor-badge lock and an iFactory WIP hold are not built.
- **iFactory PRD API URL:** `prd.ifactory.ken.corp.jabil.org:60200` follows the
  STG pattern - confirm it.
- **WIP attributes:** AVIS sends `AVIS_LJ`, `AVIS_LJ_Attempts`, ... as type
  `String`. Confirm they exist in iFactory, or turn them off with
  `Sequence:SendCheckResultsToIFactory=false`.
- **HID badge scanner + focus:** Raw Input reads the scanner but does not swallow
  its keystrokes. If an iFactory text field has focus, the badge number is typed
  into it too. Serial (COM) mode avoids this - see the JabilEye README for
  switching the scanner.
- **Images:** BaseProgram's `Home` / `Setup` / `CVG` / `JABIL` resources weren't
  included. Drop `cvg.png`, `home.png` and `jabil.png` into `src/Avis.App/Assets/`
  and they're picked up automatically. Until then the buttons show text.
- **Pi-Dashboards repo:** it contains `jig-tracker/first_run_admin_password.txt`,
  App Hub's `db.json` and other apps' data files. Remove them from the repo and
  rotate that password.
- Not run on real Windows hardware yet. Everything compiles and the Core logic
  is unit-tested; the WinForms UI and WebView2 need a first run in Visual Studio.
