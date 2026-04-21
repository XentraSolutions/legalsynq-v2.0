# MON-INT-STATUS-PROD-FIX-001 — Production Status Page Availability Bars Fix

> Report created FIRST before any diagnosis steps, per mandatory execution rules.
> Incrementally updated through each step.

---

## 1. Task Summary

**Problem**: The public `/status` page on the deployed (production) Control Center renders the summary, status labels, incidents, and window selector correctly, but availability bars do **not** render in any window (24h / 7d / 30d) — `components: []` is returned by `/api/monitoring/uptime`.

**Goal**: Identify the production-specific root cause and apply the smallest correct fix so bars render from real uptime history data in the deployed environment. The equivalent dev fix is MON-INT-STATUS-FIX-001.

---

## 2. Runtime Diagnosis

### 2.1 Dev environment baseline

- `MONITORING_SOURCE=service` is explicitly set by `scripts/run-dev.sh` (line 22) when starting the Control Center process.
- Monitoring service runs on `:5015`, backed by MySQL on AWS RDS (`legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com`).
- Local curl confirms:
  ```
  GET http://localhost:5004/api/monitoring/uptime?window=24h
  → HTTP 200
  → {"window":"24h","totalBars":24,"components":[{"name":"Audit","uptimePercent":96.04,"buckets":[…]},...]}
  ```
  **11 components with real hourly buckets — bars render correctly in dev.**

### 2.2 Production environment

- Production deployment logs consistently show `Connection refused (localhost:5015)` within seconds of startup and continue for **49+ minutes**:
  ```
  System.Net.Http.HttpRequestException: Connection refused (localhost:5015)
  ```
- Gateway returns HTTP 502 for all monitoring-prefixed routes → BFF `fetchRollups` throws → `catch` block → returns `{components: []}`.
- **Zero monitoring service startup logs appear in production** — not even `MonitoringMigrations: applying pending EF Core migrations` — indicating the process exits silently before logging initialises.
- The [monitoring-source] fallback correctly degrades the monitoring **summary** page, but the uptime BFF has no equivalent fallback — it silently returns empty.

### 2.3 Secrets and configuration

| Config item | Dev | Production | Match? |
|---|---|---|---|
| `MONITORING_SOURCE` | `service` (set in run-dev.sh line 22) | `service` (same run-dev.sh) | ✓ |
| `ConnectionStrings__MonitoringDb` | RDS MySQL (env secret) | Same secret | ✓ |
| `ASPNETCORE_URLS` | Not globally set | Not globally set | ✓ |
| Monitoring service port | 5015 (appsettings.json) | 5015 (same file) | ✓ |
| Port conflict with other services | None (5001–5003, 5007–5012) | Same | ✓ |
| `ASPNETCORE_ENVIRONMENT` | `Development` (run-dev.sh line 51) | `Development` (same line) | ✓ |

### 2.4 Build chain analysis

The monitoring service IS part of `LegalSynq.sln` and IS built by the solution build step:
```
dotnet build LegalSynq.sln --no-restore --configuration Debug --verbosity quiet
```
followed by a redundant standalone build:
```
dotnet build Monitoring.Api.csproj --no-restore --configuration Debug --verbosity quiet
```
Both runs use `--verbosity quiet`, silencing any warnings or soft errors.

The service is then started with:
```
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project Monitoring.Api.csproj &
```
`--no-build` requires the binary to already exist and be valid. If the binary is stale, ABI-mismatched, or the runtime can't load it in the production container, the process exits immediately with no log output (crash happens before logging is configured).

---

## 3. Root Cause Analysis

### Root Cause A — Monitoring service not running in production (PRIMARY)

The monitoring service process crashes before its logging pipeline is initialised, producing zero log output. Possible sub-causes (ordered by likelihood):

1. **Binary produced by solution build is invalid for production runtime** — the `dotnet run --no-build` flag requires a pre-built binary that is correct for the current host. If the binary was produced by the solution build but `dotnet run --no-build` for the standalone project cannot resolve it (different intermediate output path, missing publish-time files), the process aborts before `WebApplication.CreateBuilder` runs.
2. **DI startup exception swallowed as background process** — an uncaught `InvalidOperationException` during `AddInfrastructure` or `AddMonitoringAuthentication` (e.g., missing config key in the production environment) crashes the process. Since the process is started with `&`, its stderr is not captured with any prefix that makes it identifiable in the combined log stream.
3. **Runtime library mismatch** — production container may use a slightly different .NET 8 patch version or native library set that makes the pre-compiled binary unloadable.

### Root Cause B — Uptime BFF silent empty fallback (SECONDARY)

Even if the monitoring service were running, the uptime BFF at `apps/control-center/src/app/api/monitoring/uptime/route.ts` swallows errors silently:
```ts
} catch {
  const empty: PublicUptimeResponse = { window, totalBars: bars, components: [] };
  return NextResponse.json(empty, { headers: NO_STORE });
}
```
There is no log output, no `monitoringUnavailable` flag, and no indication to the status page UI that the service is down rather than having zero monitored components.

---

## 4. Fixes Applied

### Fix 1 — `scripts/run-dev.sh`: Reliable monitoring service startup

**Changes:**
- Removed the redundant standalone `dotnet restore` + `dotnet build` for monitoring (already in LegalSynq.sln).
- Replaced `dotnet run --no-build --project Monitoring.Api.csproj` with `dotnet run --project Monitoring.Api.csproj` — the runtime-driven build eliminates the binary-not-found class of failure and ensures the binary is always built from source at start time.
- Added output prefixing via `sed` so all monitoring service stdout/stderr appears in the combined log stream as `[monitoring] …`, making crashes immediately identifiable.
- Wrapped in a restart loop (2 attempts, 15-second gap) so transient startup failures (e.g., DB connection spike during cold start) auto-recover.

### Fix 2 — `apps/control-center/src/app/api/monitoring/uptime/route.ts`: Logged error + unavailability flag

**Changes:**
- The `catch` block now logs the error to `console.error` so the Next.js log stream captures why the fetch failed.
- Response includes `monitoringUnavailable: true` when the monitoring service cannot be reached, allowing the status page UI to show a meaningful "Monitoring data temporarily unavailable" state rather than silently rendering nothing.
- The `PublicUptimeResponse` interface is extended with the optional `monitoringUnavailable?: boolean` field.

---

## 5. Verification

### Dev verification (post-fix)

```
GET http://localhost:5004/api/monitoring/uptime?window=24h
→ {"window":"24h","totalBars":24,"components":[…11 components with real buckets…]}
```
Monitoring service startup visible in workflow logs as `[monitoring] …` prefix entries.

### Production verification (expected after redeploy)

After redeploy:
- `[monitoring] …` prefixed log lines will appear in production logs, revealing the exact crash message.
- If Fix 1 resolves the startup issue, `GET /api/monitoring/uptime` will return real components.
- If monitoring still fails to start, the `monitoringUnavailable: true` flag will be visible in the API response for further diagnosis.

### Dev re-verification after fix (confirmed ✓)

After applying fixes and restarting:
```
GET http://localhost:5004/api/monitoring/uptime?window=24h
→ components: 11 | first: Audit | buckets: 17 | monitoringUnavailable: undefined
```
- `[monitoring]` prefix appears in workflow logs confirming restart wrapper is active.
- Monitoring service starts on `:5015` with `dotnet run` (no `--no-build`).
- Status page `/status` returns HTTP 200 with real bar data.
- `monitoringUnavailable` is absent when monitoring is healthy (optional field, correct behaviour).

---

## 6. Files Changed

| File | Change |
|---|---|
| `scripts/run-dev.sh` | Removed redundant monitoring build; changed `dotnet run --no-build` → `dotnet run`; added log prefix + restart wrapper |
| `apps/control-center/src/app/api/monitoring/uptime/route.ts` | Added `monitoringUnavailable` field; logged catch error; extended interface |
