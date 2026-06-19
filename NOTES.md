# Project Notes — PV History feature, Local Run & Linux Deploy

Reference for the work done on the PV generation history view, plus how to run
and publish this project. Upstream repo: <https://github.com/dj-nitehawk/Hybrid-Inverter-Monitor>

---

## 1. Build environment

| Project | Target | SDK needed |
|---|---|---|
| `src/Shared` | net7.0 | 7/8/9 |
| `src/Client` (Blazor WASM) | net7.0 | 7/8/9 |
| `src/Server` (FastEndpoints + LiteDB) | net7.0, **`LangVersion=13`** | **.NET 9 SDK** |
| `src/InverterMonWindow` (WinForms tray) | net9.0-windows | .NET 9 SDK |

> The Server pins `LangVersion=13` and uses the FastEndpoints **source generator**,
> so it only builds with the **.NET 9 SDK** even though it targets net7.0.
> Install it with: `winget install Microsoft.DotNet.SDK.9`

Frameworks: Blazor WebAssembly + **AntDesign.Charts 0.5.6**, **FastEndpoints**
(REPR / vertical-slice), **LiteDB** (embedded, single `InverterMon.db` file).

---

## 2. PV Generation History feature (added)

A weekly / monthly / yearly history view with lifetime totals and per-day drill-down,
on top of the existing single-day `/pvgen` page.

### Routes / endpoints
| Method | Route | Purpose |
|---|---|---|
| GET | `/api/pv-log/get-pv-history/{Period}/{Offset}` | week/month/year history + stats |
| GET | `/api/pv-log/get-pv-lifetime` | all-time totals |
| GET | `/api/pv-log/get-pv-for-day/{DayNumber}` | (existing) single day, accepts `?day=` drill-down |

- `Period` = `week` | `month` | `year`
- `Offset` = how many whole periods back (0 = current).

### Pages
- `/pvgen` — existing single-day 5-minute-bucket chart. Now accepts `?day={dayNumber}`
  query param (drill-down target).
- `/pvgen/history` — new history page: Week/Month/Year toggle, prev/next navigation,
  AntDesign `<Column>` chart, summary cards (Total, Daily Average, Peak Day, Days Recorded),
  lifetime banner, and a clickable "Top Generating Days" list.

### Behavior decisions
- **Calendar-aligned periods** (not rolling "last N days"):
  - Week = **Monday → Sunday**
  - Month = 1st → last day
  - Year = Jan 1 → Dec 31 (rendered as 12 monthly buckets)
- **Date format = `YYYY.MM.DD`** everywhere (year context always visible).
  Range header: week `2026.06.15 - 2026.06.21`, month `2026.06`, year `2026`.
- **Drill-down**: AntDesign.Charts 0.5.6 has **no element/bar click callback** (only
  `OnTitleClick`), so drill-down is wired via the data — Top Days rows, the Peak Day card,
  and the lifetime best-day link navigate to `pvgen?day={dayNumber}`.
- Peak Day / Top Days are always computed from **raw daily records**, so they stay
  accurate even in the year view (which shows monthly bars).
- **Dev demo data**: in `Development` the PV endpoints fabricate random data when no real
  generation exists, so charts populate without hardware.

### Files added / changed
```
src/Shared/Models/PVHistory.cs                       (new DTO)
src/Shared/Models/PVLifetime.cs                      (new DTO)
src/Server/Endpoints/PVLog/GetPVHistory/Endpoint.cs  (new)
src/Server/Endpoints/PVLog/GetPVHistory/Request.cs   (new)
src/Server/Endpoints/PVLog/GetPVLifetime/Endpoint.cs (new)
src/Server/Persistance/Database.cs                   (+ GetPvGenForRange, GetAllPvGen)
src/Server/Endpoints/PVLog/GetPVForDay/Endpoint.cs   (DayName -> "yyyy.MM.dd dddd")
src/Client/Pages/PVGenHistory.razor                  (new page)
src/Client/Pages/PVGenForDay.razor                   (+ ?day query param)
src/Client/Shared/NavMenu.razor                      (+ PV History nav entry)
```
No persistence/schema changes — daily docs are already keyed by `DateOnly.DayNumber`.

---

## 3. Run locally on port 5000

The port comes from config key `LaunchSettings:WebPort` (default 80), which Kestrel binds
explicitly — it overrides `applicationUrl` in launchSettings. Override it via env var
(`:` → `__`):

```powershell
$env:LaunchSettings__WebPort = "5000"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/Server/InverterMon.Server.csproj
```

Then open <http://localhost:5000>.

- `Development` mode **skips the USB/BMS hosted services** and serves **demo data** — ideal
  for UI checks without hardware.
- Routes to test: `/`, `/pvgen`, `/pvgen?day=739782`, `/pvgen/history`.
- Stop with `Ctrl+C`.

---

## 4. Publish for Linux (ready-to-run, self-contained)

The Server's `Release` config already produces a single-file, self-contained, trimmed
binary (default RID `linux-arm64`). Publishing the Server also bundles the Blazor WASM
client into `wwwroot`. Target machine needs **no .NET runtime installed**.

```powershell
# arm64 (Raspberry Pi / SBC) — repo default
dotnet publish src/Server/InverterMon.Server.csproj -c Release -r linux-arm64 --self-contained true -o <output-folder>

# x64 (Linux PC / server / VM / cloud)
dotnet publish src/Server/InverterMon.Server.csproj -c Release -r linux-x64   --self-contained true -o <output-folder>

# arm (32-bit Raspberry Pi OS)
dotnet publish src/Server/InverterMon.Server.csproj -c Release -r linux-arm    --self-contained true -o <output-folder>
```

`IL2104` trim warnings during publish are expected (PublishTrimmed is on), not errors.

### Output (≈60 MB)
- `InverterMon.Server` — single-file executable (~41 MB, runtime bundled)
- `libSystem.IO.Ports.Native.so` — native serial lib; **must stay beside the executable**
- `appsettings.json` — config (serial paths, `WebPort`)
- `wwwroot/` — Blazor dashboard
- `BlazorDebugProxy/` — harmless leftover, safe to delete

### Deploy to the device
```bash
# copy the published folder to e.g. /inverter, then:
chmod +x /inverter/InverterMon.Server          # exec bit is lost when copying from Windows
cd /inverter && ./InverterMon.Server
```
- Binds to **port 80** by default → needs root, OR change `WebPort`, OR:
  `sudo setcap 'cap_net_bind_service=+ep' ./InverterMon.Server`
- Set correct serial paths in `appsettings.json` (`DeviceAddress`, `JkBmsAddress`).
- Auto-start on boot via **systemd** — see `README.md` (expects files in `/inverter`).
- Runs in **Production** mode (no `ASPNETCORE_ENVIRONMENT=Development`), so it talks to the
  real inverter/BMS over USB instead of serving demo data.

### Last local build output
`publish-linux-arm64/` (in the repo root).
