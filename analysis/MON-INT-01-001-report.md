# MON-INT-01-001 — Monitoring Read API Integration

> **Report created FIRST** — before any search, inspection, or code change.
> Created: 2026-04-20 | Status: BLOCKED — Monitoring Service source not available

---

## 1. Task Summary

Integrate the Monitoring Service into the LegalSynq platform as the authoritative backend
for monitoring data and wire the Control Center abstraction layer to consume it. This is
the first real integration step after the architectural alignment completed in
MON-INT-00-001-02.

**Outcome: BLOCKED.**  
The Monitoring Service source archive is not available anywhere in this environment.
Per the mandatory execution rules, implementation is stopped at Step 2. All findings,
prerequisite analysis, and the full integration plan are documented below so work can
resume immediately once the archive is provided.

**Platform-side readiness: COMPLETE.**  
All platform preparation work from the prior iterations is in place:
- `monitoring-source.ts` abstraction layer exists and is active
- `MONITORING_SOURCE=local|service` toggle is wired
- Summary route is a clean thin adapter
- TypeScript compiles with 0 errors
- UI is unchanged and ready

---

## 2. Monitoring Service Source Discovery

### 2.1 Search Methodology

Every plausible location in the Replit environment was searched systematically.

### 2.2 Locations Checked

| Location | Method | Result |
|----------|--------|--------|
| `/mnt/data/` | `ls /mnt/data/` | **Directory does not exist** |
| `/mnt/` | `ls /mnt/` | `cacache nix nixmodules scratch` — no monitoring content |
| `/mnt/scratch/` | `ls /mnt/scratch/` | `certs home nix nixroot project repl_pseudo_fs resolv.conf run sockets tmp` — system dirs only |
| `/mnt/scratch/project/` | `ls /mnt/scratch/project/` | `upper work` — overlay filesystem mounts, no monitoring archive |
| `/mnt/scratch/home/` | `ls /mnt/scratch/home/` | No monitoring content |
| `/tmp/` | `ls /tmp/*.zip /tmp/*.tar.gz /tmp/*.tgz` | **No archives found** |
| `attached_assets/` | Full listing | `flow-source.tar.gz`, `notifications-source.tar.gz`, two spec `.txt` files — **no monitoring archive** |
| Workspace root | `ls *.zip *.tar.gz` | Only `legalsynq-source.tar.gz` (platform snapshot) |
| `/home/runner/` | `ls | grep -i monitor` | Nothing found |
| `/uploads/`, `/shared/` | `ls` | **Directories do not exist** |
| Global `find` for `Monitoring.Api.csproj` | `find / -maxdepth 6` | **Zero results** (excluding nix store, node_modules) |
| Global `find` for `MonitoringService*` | `find / -maxdepth 6` | Only spec `.txt` files in `attached_assets/` — no source code |

### 2.3 Evidence

```
$ ls /mnt/data/
ls: cannot access '/mnt/data/': No such file or directory

$ ls /tmp/*.zip /tmp/*.tar.gz 2>&1
ls: cannot access '/tmp/*.zip': No such file or directory
ls: cannot access '/tmp/*.tar.gz': No such file or directory

$ ls attached_assets/ | grep -iE "monitor|\.zip"
Pasted-You-are-implementing-feature-MON-INT-00-001-Monitoring-_1776661028390.txt
Pasted-You-are-implementing-feature-MON-INT-01-001-Monitoring-_1776663197111.txt
(no zip, no tar, no monitoring source)

$ find / -maxdepth 6 -name "Monitoring.Api.csproj" 2>/dev/null | grep -v nix/store
(no output — zero matches)
```

### 2.4 Conclusion

**The Monitoring Service source is NOT available in this environment.**

This is the third consecutive exhaustive search confirming the same result. The archive
referenced in the original onboarding spec (`/mnt/data/MonitoringService-source.zip`) has
never been uploaded or mounted into this workspace.

> ⛔ **INTEGRATION IMPLEMENTATION STOPPED — per mandatory execution rules.**
> Steps 3–10 (intake, solution integration, config, DB, gateway, runtime, CC integration,
> validation) cannot be executed without the source.

---

## 3. Monitoring Service Intake

**BLOCKED — archive unavailable.**

When the archive is provided, intake procedure:

```bash
mkdir -p apps/services/monitoring
cd /tmp
unzip MonitoringService-source.zip -d mon_intake/
cp -r mon_intake/ /home/runner/workspace/apps/services/monitoring/
```

Expected structure (based on platform conventions):
```
apps/services/monitoring/
  Monitoring.Api/
    Monitoring.Api.csproj
    Program.cs
    appsettings.json
    appsettings.Development.json
    Endpoints/
  Monitoring.Application/
    Monitoring.Application.csproj
    Interfaces/
    Services/
    DTOs/
  Monitoring.Domain/
    Monitoring.Domain.csproj
    Entities/
  Monitoring.Infrastructure/
    Monitoring.Infrastructure.csproj
    Persistence/       ← DbContext, migrations
    Repositories/
    DependencyInjection.cs
```

---

## 4. Solution Integration

**BLOCKED — archive unavailable.**

When available, add to `LegalSynq.sln`:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Monitoring.Api",
  "apps\services\monitoring\Monitoring.Api\Monitoring.Api.csproj", "{<new-guid>}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Monitoring.Application",
  "apps\services\monitoring\Monitoring.Application\Monitoring.Application.csproj", "{<new-guid>}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Monitoring.Domain",
  "apps\services\monitoring\Monitoring.Domain\Monitoring.Domain.csproj", "{<new-guid>}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Monitoring.Infrastructure",
  "apps\services\monitoring\Monitoring.Infrastructure\Monitoring.Infrastructure.csproj", "{<new-guid>}"
EndProject
```

Validate: `dotnet build LegalSynq.sln --no-restore`

---

## 5. Configuration & Environment Setup

**BLOCKED — archive unavailable. Template ready.**

Expected `apps/services/monitoring/Monitoring.Api/appsettings.json`:

```json
{
  "Urls": "http://0.0.0.0:5013",
  "ConnectionStrings": {
    "MonitoringDb": "server=legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com;port=3306;database=monitoring_db;user=admin;password=REPLACE_VIA_SECRET"
  },
  "Jwt": {
    "Issuer": "legalsynq-identity",
    "Audience": "legalsynq-platform",
    "SigningKey": "REPLACE_VIA_SECRET_minimum_32_characters_long"
  },
  "AuditClient": {
    "BaseUrl": "http://localhost:5007",
    "ServiceToken": "",
    "SourceSystem": "monitoring-service",
    "SourceService": "monitoring-api",
    "TimeoutSeconds": 5
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Secrets to provision (Replit):**

| Secret Key | Purpose |
|-----------|---------|
| `ConnectionStrings__MonitoringDb` | MySQL connection to `monitoring_db` on RDS |

Port **5013** is the next available port after Flow (5012). Confirm no conflict.

---

## 6. Database Integration (monitoring_db)

**BLOCKED — cannot inspect migrations without archive.**

### Known State (from previous iterations)

- `monitoring_db` was created on the RDS instance per spec
- MySQL 8.0 on `legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com:3306`
- No `ConnectionStrings__MonitoringDb` secret is provisioned
- The Control Center's `system-health-store.ts` also uses `monitoring_db` for its service
  registry (via `SYSTEM_HEALTH_DB_*` env vars — also not provisioned)

### Integration Procedure (when archive available)

1. Inspect `Monitoring.Infrastructure/Persistence/` for EF Core migrations
2. If migrations present:
   ```bash
   dotnet ef database update \
     --project apps/services/monitoring/Monitoring.Infrastructure \
     --startup-project apps/services/monitoring/Monitoring.Api
   ```
3. If no migrations, inspect entity classes and generate:
   ```bash
   dotnet ef migrations add InitialCreate \
     --project apps/services/monitoring/Monitoring.Infrastructure \
     --startup-project apps/services/monitoring/Monitoring.Api
   ```
4. Verify: `SHOW TABLES` on `monitoring_db` to check for schema conflicts with CC store
   (which may have already created `system_health_services` table)

### Schema Conflict Risk

The Control Center's `system-health-store.ts` auto-creates a `system_health_services`
table in `monitoring_db` on first use. The Monitoring Service may expect to own that
schema. **This must be resolved before running migrations** — see Section 12.

---

## 7. Gateway (YARP) Integration

**BLOCKED — not adding routes for a service that does not exist.**  
Adding YARP routes for an unavailable service would create placeholder configuration
that forwards to nothing and could confuse health checks.

### What will be added (when service is ready)

File: `apps/gateway/Gateway.Api/appsettings.json`

Following the exact pattern of every existing service:

**Routes to add (inside `ReverseProxy.Routes`):**

```json
"monitoring-service-health": {
  "ClusterId": "monitoring-cluster",
  "AuthorizationPolicy": "Anonymous",
  "Order": 50,
  "Match": { "Path": "/monitoring/health" },
  "Transforms": [{ "PathRemovePrefix": "/monitoring" }]
},
"monitoring-service-info": {
  "ClusterId": "monitoring-cluster",
  "AuthorizationPolicy": "Anonymous",
  "Order": 51,
  "Match": { "Path": "/monitoring/info" },
  "Transforms": [{ "PathRemovePrefix": "/monitoring" }]
},
"monitoring-protected": {
  "ClusterId": "monitoring-cluster",
  "Order": 150,
  "Match": { "Path": "/monitoring/{**catch-all}" },
  "Transforms": [{ "PathRemovePrefix": "/monitoring" }]
}
```

**Cluster to add (inside `ReverseProxy.Clusters`):**

```json
"monitoring-cluster": {
  "Destinations": {
    "monitoring-primary": {
      "Address": "http://localhost:5013"
    }
  }
}
```

Route order 50/51/150 follows the existing sequence (fund=10/11/110, careconnect=20/21/120,
audit=30/31/130, liens, reports, comms, flow — next gap is monitoring at 50/51/150).

---

## 8. Runtime Integration

**BLOCKED — cannot start a service that doesn't exist.**

### What will be added to `scripts/run-dev.sh` (when archive is integrated)

After the existing `.NET` block (around line 42), inside the background subshell:

```bash
dotnet restore "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj" --verbosity quiet
dotnet build "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj" --no-restore --configuration Debug --verbosity quiet
dotnet run --no-build --project "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj" &
```

The startup script already follows this pattern for every other service. Monitoring slots
in without structural changes.

---

## 9. Control Center Integration (Abstraction Layer)

**BLOCKED — implementing the 'service' branch requires a running Monitoring Service endpoint.**

### Current State (`monitoring-source.ts`)

The abstraction layer is complete and active:
- `MONITORING_SOURCE=local` (default) → existing probe behavior, fully working
- `MONITORING_SOURCE=service` → throws explicit `NOT IMPLEMENTED` error

### What will change in `monitoring-source.ts` (when service is running)

Replace the `NOT IMPLEMENTED` throw with a real gateway call:

```typescript
if (MONITORING_SOURCE === 'service') {
  const gatewayBase = process.env.GATEWAY_URL ?? 'http://localhost:5010';
  const res = await fetch(`${gatewayBase}/monitoring/api/monitoring/summary`, {
    cache:   'no-store',
    headers: { 'Accept': 'application/json' },
  });
  if (!res.ok) {
    throw new Error(
      `[monitoring-source] Monitoring Service returned ${res.status}. ` +
      `Is MONITORING_SOURCE=service correct and is the service running?`
    );
  }
  return res.json() as Promise<MonitoringSummary>;
}
```

**No other files change at cutover.** The route, UI, types, and components are all already
wired through this single function. The `GATEWAY_URL` env var is already set in
`run-dev.sh` for both `web` and `control-center` processes.

### End-state request path

```
Browser → /monitoring
         → CC monitoring/page.tsx
           → fetch /api/monitoring/summary (self)
             → api/monitoring/summary/route.ts
               → getMonitoringSummary() [monitoring-source.ts]
                 → fetch http://localhost:5010/monitoring/api/monitoring/summary
                   → YARP Gateway (5010)
                     → monitoring-cluster → http://localhost:5013
                       → Monitoring.Api → DB → response
```

---

## 10. End-to-End Validation

**BLOCKED — cannot validate an unintegrated service.**

### Validation plan (for when integration completes)

1. **Monitoring Service health:**
   ```bash
   curl http://localhost:5013/health
   # Expected: HTTP 200, body contains "Healthy" or service JSON
   ```

2. **Gateway routing:**
   ```bash
   curl http://localhost:5010/monitoring/health
   # Expected: proxied response from Monitoring Service
   ```

3. **Summary endpoint (local mode — regression test):**
   ```bash
   curl http://localhost:5004/api/monitoring/summary
   # Expected: MonitoringSummary JSON with system/integrations/alerts
   ```

4. **Summary endpoint (service mode):**
   ```bash
   MONITORING_SOURCE=service curl http://localhost:5004/api/monitoring/summary
   # Expected: same shape, data from Monitoring Service
   ```

5. **UI validation:**
   - Navigate to `/monitoring` in Control Center
   - Verify SystemHealthCard, IntegrationStatusTable, AlertsPanel render without errors
   - Confirm no UI code changes were needed

---

## 11. Files Changed

### Created

| File | Purpose |
|------|---------|
| `/analysis/MON-INT-01-001-report.md` | This report — created FIRST |

### Modified

*None — integration blocked. No code changes made.*

### Would be changed (when archive is available)

| File | Change |
|------|--------|
| `apps/services/monitoring/` | New directory — Monitoring Service source |
| `LegalSynq.sln` | Add 4 Monitoring project references |
| `apps/services/monitoring/Monitoring.Api/appsettings.json` | DB + JWT + AuditClient config |
| `apps/gateway/Gateway.Api/appsettings.json` | Add monitoring-cluster and 3 routes |
| `scripts/run-dev.sh` | Add Monitoring Service build + start |
| `apps/control-center/src/lib/monitoring-source.ts` | Implement `service` branch fetch |
| Replit Secrets | Add `ConnectionStrings__MonitoringDb` |

---

## 12. Known Gaps / Blockers

### Hard Blockers

| # | Blocker | Impact | Resolution |
|---|---------|--------|-----------|
| **B1** | `MonitoringService-source.zip` not available in this environment | All of Steps 3–10 blocked — full integration cannot proceed | Upload archive to workspace |
| **B2** | `ConnectionStrings__MonitoringDb` secret not provisioned | Monitoring Service cannot connect to DB on startup | Add secret to Replit with RDS MySQL 8.0 credentials |
| **B3** | `SYSTEM_HEALTH_DB_*` env vars not provisioned | CC service registry (monitoring-source local mode) cannot connect to `monitoring_db` | Provision env vars; or alternatively ensure `monitoring_db` schema doesn't conflict with the Monitoring Service's own schema |

### Schema Conflict Risk (B4)

The Control Center's `system-health-store.ts` has an `ensureSchemaAndSeed()` that
auto-creates a `system_health_services` table in `monitoring_db`. If the Monitoring
Service uses `monitoring_db` and expects to own all tables there, there may be a conflict.

**Resolution path:**
- Run `SHOW TABLES` on `monitoring_db` after provisioning B2
- Determine whether CC's `system_health_services` table belongs in `monitoring_db` or a
  separate schema
- If separate: move CC store to a different DB or table prefix before running Monitoring
  Service migrations

### Known Unknowns (resolved only from archive)

| # | Unknown | Impact |
|---|---------|--------|
| U1 | Exact port used by Monitoring Service | Recommend 5013 — confirm from `appsettings.json` |
| U2 | EF Core migration presence and state | Determines DB init procedure |
| U3 | Exact summary endpoint route (`/api/monitoring/summary` assumed) | Determines CC fetch URL |
| U4 | Auth requirements on summary endpoint | Determines whether CC fetch needs a JWT or service token |
| U5 | Response schema matches `MonitoringSummary` type | If mismatch, a mapper may be needed in `monitoring-source.ts` |

---

## 13. Recommended Next Feature

### MON-INT-01-001-01 — Monitoring Source Intake Resolution

**Justification:**  
The Monitoring Service archive is the single hard blocker for this entire integration
stream. All platform-side work (abstraction layer, type contracts, route wiring, gateway
config template) is complete and waiting. Until the archive is available, no further
integration progress is possible beyond documentation.

**What MON-INT-01-001-01 should accomplish:**
1. Locate or receive the Monitoring Service archive
2. Upload it to the workspace (e.g., `attached_assets/MonitoringService-source.zip`)
3. Provision `ConnectionStrings__MonitoringDb` in Replit Secrets
4. Resolve the `monitoring_db` schema ownership question (CC store vs. Monitoring Service)
5. Return here to complete MON-INT-01-001 with all blockers cleared

**Once B1 is resolved:**  
MON-INT-01-001 can resume immediately at Step 3 (intake). All subsequent steps
(Steps 4–10) have detailed, ready-to-execute plans documented above.
The Control Center integration (Step 9) requires only implementing one `fetch` call
in a single function in `monitoring-source.ts`.
